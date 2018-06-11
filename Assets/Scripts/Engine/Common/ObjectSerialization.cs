// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

public class ObjectSerializationException : Exception {
	public ObjectSerializationException(string message) : base(message) {
	}
}

public class ObjectStaticClassMismatchException : Exception {
	public ObjectStaticClassMismatchException(string message) : base(message) { }
}

public class InvalidReplicatedObjectClassException : Exception {
	public InvalidReplicatedObjectClassException(string message) : base(message) { }
}

public enum EReplicateCondition {
	Always,
	InitialOnly,
	OwnerOnly,
	SkipOwner,
	InitialOrOwner,
	InitialOwnerOnly
}

// Field should be replicated during multiplayer.
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class Replicated : Attribute {
	string onRep;
	EReplicateCondition condition;
	Type customSerializer;
	float updateRate;

	public Replicated() {
		condition = EReplicateCondition.Always;
		updateRate = 0;
	}

	public string Notify {
		get {
			return onRep;
		}
		set {
			onRep = value;
		}
	}

	public EReplicateCondition Condition {
		get {
			return condition;
		}
		set {
			condition = value;
		}
	}

	public float UpdateRate {
		get {
			return updateRate;
		}
		set {
			updateRate = value;
		}
	}

	public Type Using {
		get {
			return customSerializer;
		}
		set {
			customSerializer = value;
		}
	}
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class ReplicatedUsing : Attribute {

	Type _type;

	public ReplicatedUsing(Type typeOfSerializer) {
		_type = typeOfSerializer;
    }

	public Type serializerType {
		get {
			return _type;
		}
	}
}

public abstract class SerializableObject {
	protected static bool activating = false;
	static Dictionary<Type, int> _staticClassIDs = new Dictionary<Type, int>();
	
	bool _replicates;

	readonly int _classID;
	int _netID;
	int _netIDHashCode;
	object _outer;
	List<ObjectReplicator> _repls;

	public SerializableObject() {
		_classID = StaticClassIDSlow(this);
	}

	public int classID {
		get {
			return _classID;
		}
	}

	public abstract Type serverType {
		get;
	}

	public abstract Type clientType {
		get;
	}

	public int netID {
		get {
			return _netID;
		}
	}

	public int netIDHashCode {
		get {
			return _netIDHashCode;
		}
	}

	public bool replicates {
		get {
			return _replicates;
		}
	}

	public object outer {
		get {
			return _outer;
		}
	}

	public ObjectReplicator internal_GetReplicator(NetConnection conn) {
		if ((_repls == null) || (conn.id >= _repls.Count)) {
			return null;
		}
		var repl = _repls[conn.id];
		if ((repl != null) && (repl.channel.connection != conn)) {
			_repls[conn.id] = null;
			repl = null;
		}
		return repl;
	}

	public void internal_SetReplicator(NetConnection conn, ObjectReplicator repl) {
		if (_repls == null) {
			_repls = new List<ObjectReplicator>();
		}
		while (_repls.Count <= conn.id) {
			_repls.Add(null);
		}
		_repls[conn.id] = repl;
	}

	public void internal_ClearReplicators() {
		_repls = null;
	}

	public void SetNetID(int netID) {
		_netID = netID;
		_netIDHashCode = netID.GetHashCode();
	}

	public virtual void SerializeSubobjects(SerializableObjectSubobjectSerializer serializer) {	}

	public virtual void SerializeCustomData(Archive archive) {	}

	public virtual void PreConstruct(object outer) {
		_outer = outer;
	}

	public virtual void Construct() { }

	public virtual void PostConstruct() { }

	public virtual void PreNetReceive() { }

	public virtual void PostNetReceive() { }

	public virtual void PreNetConstruct() { }

	public virtual void PostNetConstruct() { }

	public virtual void PostOnRepFields() { }

	public virtual void Destroy() { }
	
	public void SetReplicates(bool replicates) {
		_replicates = replicates;
	}

	public static int StaticClassID<T>() where T : SerializableObject, new() {
		return SerializableObjectStaticClass<T>.StaticClassID;
	}

	static int StaticClassIDSlow(SerializableObject obj) {
		Type t = obj.GetType();
		int id;

		if (_staticClassIDs.TryGetValue(t, out id)) {
			return id;
		}

		var typeName = t.FullName;

		if ((t != obj.serverType) && (t != obj.clientType)) {
			// unrelated class types!
			t = null;
		} else if ((obj.serverType != obj.clientType) && (obj.serverType.BaseType == obj.clientType.BaseType)) {
			t = t.BaseType;
		} else if (obj.serverType.BaseType == obj.clientType) {
			t = obj.clientType;
		} else if (obj.clientType.BaseType == obj.serverType) {
			t = obj.serverType;
		} else if (obj.serverType != obj.clientType) {
			t = null;
		}
		
		if (t == null) {
			throw new InvalidReplicatedObjectClassException("Server and Client specialized object classes must derive from a common SerializableObject class, however they cannot derive directly from SerializableObject: The common base class is used to generate the static type class for replication. Error in class " + typeName);
		}

		id = t.AssemblyQualifiedName.GetHashCode();
		_staticClassIDs[t] = id;
		return id;
	}

	public static int StaticClassIDSlow(Type t) {
		int id;
		if (_staticClassIDs.TryGetValue(t, out id)) {
			return id;
		}

		if (!typeof(SerializableObject).IsAssignableFrom(t)) {
			throw new Exception("Only SerializableObjects have static class id's!");
		}
		if (typeof(SerializableObject) == t) {
			throw new Exception("SerializableObject base class does not have a class id!");
		}

		var ctor = t.GetConstructor(System.Type.EmptyTypes);
		if (ctor == null) {
			throw new TargetInvocationException(t.FullName + " does not have a constructor!", null);
		}

		SerializableObject.activating = true;
		id = (ctor.Invoke(null) as SerializableObject).classID;
		SerializableObject.activating = false;
		return id;
	}
}

public abstract class SerializableObjectStaticClass<T> where T : SerializableObject, new() {
	static readonly int _staticClassID;

	static SerializableObjectStaticClass() {
		_staticClassID = SerializableObject.StaticClassIDSlow(typeof(T));
	}

	static public int StaticClassID {
		get {
			return _staticClassID;
		}
	}
}

public interface SerializableObjectSubobjectSerializer {
	void SerializeSubobject(SerializableObject obj);
}

public abstract class SerializableObjectFactory {

	IntHashtable<ConstructorInfo> objTypes = new IntHashtable<ConstructorInfo>();

	public SerializableObjectFactory(Assembly[] assemblies, bool forServer) {
		var types = ReflectionHelpers.GetTypesThatImplementInterfaces(assemblies, new Type[] { typeof(SerializableObject) });
		foreach (var t in types) {
			var ctor = t.GetConstructor(System.Type.EmptyTypes);
			if (ctor != null) {
				var obj = ctor.Invoke(null) as SerializableObject;
				if ((forServer && (obj.serverType == t)) || (!forServer && (obj.clientType == t))) {
					if (objTypes.Contains(obj.classID)) {
						throw new System.Exception(t.FullName + " collides with an existing type!");
					} else {
						objTypes.Add(obj.classID, ctor);
					}
				}
			} else {
				throw new TargetInvocationException(t.FullName + " does not have a constructor!", null);
			}
		}
	}

	public SerializableObject NewObject(int typeID) {
		var ctor = objTypes[typeID];
		if (ctor != null) {
			return ctor.Invoke(System.Type.EmptyTypes) as SerializableObject;
		}
		return null;
	}
}

public class ClientSerializableObjectFactory : SerializableObjectFactory {
	public ClientSerializableObjectFactory(Assembly[] assemblies) : base(assemblies, false) {
	}
}

public class ServerSerializableObjectFactory : SerializableObjectFactory {
	public ServerSerializableObjectFactory(Assembly[] assemblies) : base(assemblies, true) {
	}
}

public interface SerializableObjectFieldSerializerFactory {
	SerializableObjectFieldSerializer GetSerializerForField(SerializedObjectFields.FieldSpec field);
	SerializableObjectFieldSerializer GetSerializerForType(Type type);
}

public interface SerializableObjectReferenceCollector {
	SerializableObject AddReference(SerializableObjectFieldSerializer serializer, int id, int fieldIndex);
}

public interface SerializableObjectFieldSerializer {
	void ClearState();
	bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState);
	void ResolveReference(SerializableObject obj, int id, int fieldIndex, ref object field);
	bool FieldsAreEqual(object a, object b);
	object Copy(object toCopy);
}

public abstract class SerializableObjectNonReferenceFieldSerializer<T> : SerializableObjectFieldSerializer where T: SerializableObjectNonReferenceFieldSerializer<T>, new() {

	static T _instance = new T();

	public static T instance {
		get {
			return _instance;
		}
	}

	public void ClearState() { }

	public abstract bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState);

	public void ResolveReference(SerializableObject obj, int id, int fieldIndex, ref object field) {
		throw new NotImplementedException();
	}

	public abstract bool FieldsAreEqual(object a, object b);
	public abstract object Copy(object toCopy);
}

public class SerializableObjectBoolFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectBoolFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		var value = (bool)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((bool)a) == ((bool)b);
	}

	public override object Copy(object toCopy) {
		return (bool)toCopy;
	}
}

public class SerializableObjectColorFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectColorFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Color32 value = (Color)field;
		archive.Serialize(ref value);
		field = (Color)value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Color)a) == ((Color)b);
	}

	public override object Copy(object toCopy) {
		return (Color)toCopy;
	}
}

public class SerializableObjectColor32FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectColor32FieldSerializer> {
	
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Color32 value = (Color32)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Color32)a).Equals((Color32)b);
	}

	public override object Copy(object toCopy) {
		return (Color32)toCopy;
	}
}

public class SerializableObjectStringFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectStringFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		string value = (string)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return ReferenceEquals(a, b) || ((a != null) && (b != null) && ((string)a) == ((string)b));
	}

	public override object Copy(object toCopy) {
		return toCopy; // strings are immutable so we don't need to copy them.
	}
}

public class SerializableObjectByteFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectByteFieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		byte value = (byte)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((byte)a) == ((byte)b);
	}

	public override object Copy(object toCopy) {
		return (byte)toCopy;
	}
}

public class SerializableObjectSByteFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectSByteFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		sbyte value = (sbyte)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((sbyte)a) == ((sbyte)b);
	}

	public override object Copy(object toCopy) {
		return (sbyte)toCopy;
	}
}

public class SerializableObjectInt16FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectInt16FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		short value = (short)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((short)a) == ((short)b);
	}

	public override object Copy(object toCopy) {
		return (short)toCopy;
	}
}

public class SerializableObjectUInt16FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectUInt16FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		ushort value = (ushort)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((ushort)a) == ((ushort)b);
	}

	public override object Copy(object toCopy) {
		return (ushort)toCopy;
	}
}

public class SerializableObjectInt32FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectInt32FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		int value = (int)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((int)a) == ((int)b);
	}

	public override object Copy(object toCopy) {
		return (int)toCopy;
	}
}

public class SerializableObjectUInt32FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectUInt32FieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		uint value = (uint)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((uint)a) == ((uint)b);
	}

	public override object Copy(object toCopy) {
		return (uint)toCopy;
	}
}

public class SerializableObjectInt64FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectInt64FieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		long value = (long)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((long)a) == ((long)b);
	}

	public override object Copy(object toCopy) {
		return (long)toCopy;
	}
}

public class SerializableObjectUInt64FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectUInt64FieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		ulong value = (ulong)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((ulong)a) == ((ulong)b);
	}

	public override object Copy(object toCopy) {
		return (ulong)toCopy;
	}
}

public class SerializableObjectFloatFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectFloatFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		float value = (float)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((float)a) == ((float)b);
	}

	public override object Copy(object toCopy) {
		return (float)toCopy;
	}
}

public class SerializableObjectDoubleFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectDoubleFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		double value = (double)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((double)a) == ((double)b);
	}

	public override object Copy(object toCopy) {
		return (double)toCopy;
	}
}

public class SerializableObjectEnumFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectEnumFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		int value = Convert.ToInt32((Enum)field);
		archive.Serialize(ref value);
		field = Enum.ToObject(field.GetType(), value);
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Enum)a) == ((Enum)b);
	}

	public override object Copy(object toCopy) {
		return Enum.ToObject(toCopy.GetType(), toCopy);
	}
}

public class SerializableObjectVector2FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectVector2FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Vector2 value = (Vector2)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Vector2)a) == ((Vector2)b);
	}

	public override object Copy(object toCopy) {
		return (Vector2)toCopy;
	}
}

public class SerializableObjectVector3FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectVector3FieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Vector3 value = (Vector3)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Vector3)a) == ((Vector3)b);
	}

	public override object Copy(object toCopy) {
		return (Vector3)toCopy;
	}
}

public class SerializableObjectVector4FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectVector4FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Vector4 value = (Vector4)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Vector4)a) == ((Vector4)b);
	}

	public override object Copy(object toCopy) {
		return (Vector4)toCopy;
	}
}

public class SerializableObjectQuaternionFieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectQuaternionFieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Quaternion value = (Quaternion)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Quaternion)a) == ((Quaternion)b);
	}

	public override object Copy(object toCopy) {
		return (Quaternion)toCopy;
	}
}

public class SerializableObjectMatrix4x4FieldSerializer : SerializableObjectNonReferenceFieldSerializer<SerializableObjectMatrix4x4FieldSerializer> {
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		Matrix4x4 value = (Matrix4x4)field;
		archive.Serialize(ref value);
		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((Matrix4x4)a) == ((Matrix4x4)b);
	}

	public override object Copy(object toCopy) {
		return (Matrix4x4)toCopy;
	}
}

public class SerializableObjectListFieldSerializer<T> : SerializableObjectFieldSerializer, SerializableObjectReferenceCollector {
	SerializableObjectFieldSerializer _itemSerializer;
	// [fieldIndex => [id => fields]]
	Dictionary<int, Dictionary<int, HashSetList<int>>> _subFieldReferences;
	SerializableObjectReferenceCollector _outerCollector;
	int _outerFieldIndex;
	readonly bool _simpleReferences;

	public void ClearState() {
		_outerCollector = null;
		_subFieldReferences = null;
	}

	public SerializableObjectListFieldSerializer(SerializableObjectFieldSerializerFactory factory) {
		_itemSerializer = factory.GetSerializerForType(typeof(T));
		_simpleReferences = !typeof(System.Collections.IList).IsAssignableFrom(typeof(T));
	}

	public bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		_outerCollector = collector;

		List<T> list = (List<T>)field;
		ushort numItems = (list != null) ? (ushort)list.Count : (ushort)0;

		archive.Serialize(ref numItems);

		bool onRep = false;

		if (archive.isLoading) {
			if (list != null) {
				list.Clear();
			} else {
				list = new List<T>(numItems);
				field = list;
			}

			onRep = numItems < 1; // make sure to rep if list goes to 0 length.

			for (int i = 0; i < numItems; ++i) {
				_outerFieldIndex = i;
				var item = default(T);
				object objItem = item;
				if (_itemSerializer.Serialize(archive, this, ref objItem, null)) {
					onRep = true;
				}
				item = (T)objItem;
				list.Add(item);
			}
		} else {
			for (int i = 0; i < numItems; ++i) {
				_outerFieldIndex = i;
				object objItem = list[i];
				_itemSerializer.Serialize(archive, this, ref objItem, null);
			}
		}

		_outerCollector = null;

		return onRep;
	}

	public bool FieldsAreEqual(object a, object b) {
		var listA = (List<T>)a;
		var listB = (List<T>)b;

		if (((listA == null) || (listA.Count == 0)) && ((listB == null) || (listB.Count == 0))) {
			return true;
		}

		if ((listA == null) || (listB == null) || (listA.Count != listB.Count)) {
			return false;
		}

		for (int i = 0; i < listA.Count; ++i) {
			if (!_itemSerializer.FieldsAreEqual(listA[i], listB[i])) {
				return false;
			}
		}

		return true;
	}

	public object Copy(object toCopy) {
		var original = (List<T>)toCopy;
		if ((original == null) || (original.Count == 0)) {
			return null;
		}

		var newList = new List<T>(original.Count);
		for (int i = 0; i < original.Count; ++i) {
			newList.Add((T)_itemSerializer.Copy(original[i]));
		}

		return newList;
	}

	public SerializableObject AddReference(SerializableObjectFieldSerializer serializer, int id, int fieldIndex) {
		var obj = _outerCollector.AddReference(this, id, _outerFieldIndex);
		if (obj != null) {
			return obj;
		}

		if (!_simpleReferences) {
			if (_subFieldReferences == null) {
				_subFieldReferences = new Dictionary<int, Dictionary<int, HashSetList<int>>>();
			}
			Dictionary<int, HashSetList<int>> idToIndexSet;
			if (!_subFieldReferences.TryGetValue(_outerFieldIndex, out idToIndexSet)) {
				idToIndexSet = new Dictionary<int, HashSetList<int>>();
				_subFieldReferences[_outerFieldIndex] = idToIndexSet;
			}
			HashSetList<int> fields;
			if (!idToIndexSet.TryGetValue(id, out fields)) {
				fields = new HashSetList<int>();
				idToIndexSet[id] = fields;
			}
			fields.Add(fieldIndex);
		}
		return null;
	}

	public void ResolveReference(SerializableObject obj, int id, int fieldIndex, ref object field) {
		if (_simpleReferences) {
			((List<T>)field)[fieldIndex] = (T)((object)obj);
		} else {
			// serialized by a sub object
			if (_subFieldReferences != null) {
				Dictionary<int, HashSetList<int>> idToIndexSet;
				if (_subFieldReferences.TryGetValue(fieldIndex, out idToIndexSet)) {
					var fieldAsList = (List<T>)field;
					object innerObj = fieldAsList[fieldIndex];
					object originalObj = innerObj;
					if (idToIndexSet != null) {
						HashSetList<int> subFields;
						if (idToIndexSet.TryGetValue(id, out subFields)) {
							if (subFields != null) {
								for (int i = 0; i < subFields.Values.Count; ++i) {
									_itemSerializer.ResolveReference(obj, id, subFields.Values[i], ref innerObj);
                                }
								subFields.Clear();
							}
						}
					}
					if (originalObj != innerObj) {
						fieldAsList[fieldIndex] = (T)innerObj;
					}
				}
			}
		}
	}
}

public class SerializedObjectFields {
	public const int MAX_REPLICATED_FIELDS = 32;

	public class FieldSpec {
		public FieldInfo field;
		public SerializableObjectFieldSerializer serializer;
		public Replicated replication;
		public MethodInfo onRep;
		public ushort fieldID;
		public bool isObjectReference;
	}

	IntHashtableList<FieldSpec> _serializedFields = new IntHashtableList<FieldSpec>();
	int _nextFieldID;

	public SerializedObjectFields(Type t, SerializableObjectFieldSerializerFactory factory, bool forNetwork) {
		if (!typeof(Actor).IsAssignableFrom(t)) {
			throw new ObjectSerializationException("Type is not a serializable object!");
		}

		for (; t != typeof(object); t = t.BaseType) {
			var members = t.GetFields(BindingFlags.Public|BindingFlags.Instance|BindingFlags.NonPublic);

			foreach (var field in members) {
				if (field.DeclaringType != t) {
					continue;
				}

				var attributes = System.Attribute.GetCustomAttributes(field);

				if (forNetwork) {
					foreach (var attr in attributes) {
						var replication = attr as Replicated;
						if (replication != null) {
							var serializedField = new FieldSpec();
							serializedField.field = field;
							serializedField.fieldID = (ushort)++_nextFieldID;
                            serializedField.replication = replication;
							serializedField.isObjectReference = typeof(SerializableObject).IsAssignableFrom(field.FieldType);
							serializedField.serializer = factory.GetSerializerForField(serializedField);
							
							if (replication.Notify != null) {
								serializedField.onRep = t.GetMethod(replication.Notify, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.ExactBinding, null, System.Type.EmptyTypes, null);
								if (serializedField.onRep == null) {
									throw new MissingMemberException(t.FullName + " is missing replication notification method " + serializedField.onRep + " specified by replicated field " + field.Name);
								}
							}

							_serializedFields.Add(serializedField.fieldID, serializedField);
						}
					}
				} else {
					bool isTransient = true;
					foreach (var attr in attributes) {
						isTransient = !(attr is UnityEngine.SerializeField);
						if (!isTransient) {
							break;
						}
					}

					if (!isTransient) {
						var serializedField = new FieldSpec();
						serializedField.field = field;
						serializedField.fieldID = (ushort)++_nextFieldID;
						serializedField.serializer = factory.GetSerializerForField(serializedField);
						serializedField.isObjectReference = typeof(SerializableObject).IsAssignableFrom(field.FieldType);
						_serializedFields.Add(serializedField.fieldID, serializedField);
					}
				}
			}
		}

		if (_serializedFields.Values.Count > MAX_REPLICATED_FIELDS) {
			throw new ObjectSerializationException("Too many replicated fields!");
		}
	}

	public IntHashtableList<FieldSpec> serializedFields {
		get {
			return _serializedFields;
		}
	}
}