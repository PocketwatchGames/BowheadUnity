// Copyright (c) 2018 Pocketwatch Games LLC.
#define PACKET_COMBINE
//#define INTEGRITY_CHECK

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Reflection;
using System.Collections.Generic;

public enum ERPCDomain {
	Server,
	Owner,
	Multicast,
	MulticastExcludeOwner
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class RPC : Attribute {
	ERPCDomain _domain;
	
	public RPC(ERPCDomain domain) {
		_domain = domain;
	}

	public ERPCDomain Domain {
		get {
			return _domain;
		}
	}

	public bool CheckRelevancy {
		get;
		set;
	}
}


[ReplicatedUsing(typeof(QuantizedFloatFieldSerializer))]
public struct QuantizedFloatField {

	public struct FixedPoint {
		public FixedPoint(int whole, int frac) {
			this.whole = whole+1; // +1 bit for sign
			this.frac = frac;
		}
		
		public readonly int whole;
		public readonly int frac;
	}

	public struct QFF {

		public QFF(FixedPoint fp) : this(fp.whole, fp.frac) {}

		public QFF(int whole, int frac) {
			whole = whole - 1;
			NUM_BITS = whole+frac;
			Assert.IsTrue(NUM_BITS <= 32);
			MAX_VALUE = Mathf.Pow(2, whole) - float.Epsilon;
			MUL_SHIFT = Mathf.Pow(2, frac);
		}

		public readonly float MAX_VALUE;
		public readonly float MUL_SHIFT;
		public readonly int NUM_BITS;

		public int Get(float x) {
			return (int)(x * MUL_SHIFT);
		}

		public float SetChecked(int x) {
			var z = x / MUL_SHIFT;
#if UNITY_EDITOR
			Assert.IsTrue(Mathf.Abs(z) <= MAX_VALUE);
#endif
			return z;
		}

		public float SetClamped(float x) {
			return Mathf.Clamp(x, -MAX_VALUE, MAX_VALUE);
		}

		public float SetChecked(float x) {
#if UNITY_EDITOR
			Assert.IsTrue(Mathf.Abs(x) <= MAX_VALUE);
#endif
			return SetClamped(x);
		}
	}

	public readonly QFF F;

	float _value;

	public QuantizedFloatField(float value, FixedPoint fp) {
		F = new QFF(fp);
		_value = F.SetChecked(value);
	}

	public QuantizedFloatField(QuantizedFloatField q) {
		F = q.F;
		_value = q._value;
	}

	public int quantizedValue {
		get {
			return F.Get(_value);
		}
		set {
			_value = F.SetChecked(value);
		}
	}

	public float value {
		get {
			return _value;
		}
		set {
			_value = F.SetChecked(value);
		}
	}
}

public class QuantizedFloatFieldSerializer : SerializableObjectNonReferenceFieldSerializer<QuantizedFloatFieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		QuantizedFloatField value = (QuantizedFloatField)field;

		if (archive.isLoading) {
			value.quantizedValue = archive.ReadSignedBits(value.F.NUM_BITS);
		} else {
			archive.WriteSignedBits(value.quantizedValue, value.F.NUM_BITS);
		}

		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((QuantizedFloatField)a).quantizedValue == ((QuantizedFloatField)b).quantizedValue;
	}

	public override object Copy(object toCopy) {
		return new QuantizedFloatField((QuantizedFloatField)toCopy);
	}
}

[ReplicatedUsing(typeof(QuantizedVector2FieldSerializer))]
public struct QuantizedVector2Field {

	public readonly QuantizedFloatField.QFF FX;
	public readonly QuantizedFloatField.QFF FY;

	Vector2 _value;

	public QuantizedVector2Field(Vector2 value, QuantizedFloatField.FixedPoint fp) : this(value, fp, fp) {}

	public QuantizedVector2Field(Vector2 value, QuantizedFloatField.FixedPoint xFP, QuantizedFloatField.FixedPoint yFP) {
		FX = new QuantizedFloatField.QFF(xFP);
		FY = new QuantizedFloatField.QFF(yFP);
		_value.x = FX.SetChecked(value.x);
		_value.y = FY.SetChecked(value.y);
	}

	public QuantizedVector2Field(QuantizedVector2Field q) {
		_value = q._value;
		FX = q.FX;
		FY = q.FY;
	}

	public IntMath.Vector2i quantizedValue {
		get {
			return new IntMath.Vector2i(FX.Get(_value.x), FY.Get(_value.y));
		}
		set {
			_value.x = FX.SetChecked(value.x);
			_value.y = FY.SetChecked(value.y);
		}
	}

	public Vector2 value {
		get {
			return _value;
		}
		set {
			_value.x = FX.SetChecked(value.x);
			_value.y = FY.SetChecked(value.y);
		}
	}
}

public class QuantizedVector2FieldSerializer : SerializableObjectNonReferenceFieldSerializer<QuantizedVector2FieldSerializer> {

	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		QuantizedVector2Field value = (QuantizedVector2Field)field;

		if (archive.isLoading) {
			IntMath.Vector2i q = new IntMath.Vector2i(
				archive.ReadSignedBits(value.FX.NUM_BITS),
				archive.ReadSignedBits(value.FY.NUM_BITS));
			value.quantizedValue = q;
		} else {
			IntMath.Vector2i q = value.quantizedValue;
			archive.WriteSignedBits(q.x, value.FX.NUM_BITS);
			archive.WriteSignedBits(q.y, value.FY.NUM_BITS);
		}

		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((QuantizedVector2Field)a).quantizedValue == ((QuantizedVector2Field)b).quantizedValue;
	}

	public override object Copy(object toCopy) {
		return new QuantizedVector2Field((QuantizedVector2Field)toCopy);
	}
}

[ReplicatedUsing(typeof(QuantizedVector3FieldSerializer))]
public struct QuantizedVector3Field {
	
	public readonly QuantizedFloatField.QFF FX;
	public readonly QuantizedFloatField.QFF FY;
	public readonly QuantizedFloatField.QFF FZ;

	Vector3 _value;

	public QuantizedVector3Field(Vector3 value, QuantizedFloatField.FixedPoint fp) : this(value, fp, fp, fp) { }

	public QuantizedVector3Field(Vector3 value, QuantizedFloatField.FixedPoint xFP, QuantizedFloatField.FixedPoint yFP, QuantizedFloatField.FixedPoint zFP) {
		FX = new QuantizedFloatField.QFF(xFP);
		FY = new QuantizedFloatField.QFF(yFP);
		FZ = new QuantizedFloatField.QFF(zFP);
		_value.x = FX.SetChecked(value.x);
		_value.y = FY.SetChecked(value.y);
		_value.z = FZ.SetChecked(value.z);
	}

	public QuantizedVector3Field(QuantizedVector3Field q) {
		_value = q._value;
		FX = q.FX;
		FY = q.FY;
		FZ = q.FZ;
	}

	public IntMath.Vector3i quantizedValue {
		get {
			return new IntMath.Vector3i(FX.Get(_value.x), FY.Get(_value.y), FZ.Get(_value.z));
		}
		set {
			_value.x = FX.SetChecked(value.x);
			_value.y = FY.SetChecked(value.y);
			_value.z = FZ.SetChecked(value.z);
		}
	}

	public Vector3 value {
		get {
			return _value;
		}
		set {
			_value.x = FX.SetChecked(value.x);
			_value.y = FY.SetChecked(value.y);
			_value.z = FZ.SetChecked(value.z);
		}
	}
}

public class QuantizedVector3FieldSerializer : SerializableObjectNonReferenceFieldSerializer<QuantizedVector3FieldSerializer> {
	
	public override bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		QuantizedVector3Field value = (QuantizedVector3Field)field;

		if (archive.isLoading) {
			IntMath.Vector3i q = new IntMath.Vector3i(
				archive.ReadSignedBits(value.FX.NUM_BITS),
				archive.ReadSignedBits(value.FY.NUM_BITS),
				archive.ReadSignedBits(value.FZ.NUM_BITS));
			value.quantizedValue = q;
		} else {
			IntMath.Vector3i q = value.quantizedValue;
			archive.WriteSignedBits(q.x, value.FX.NUM_BITS);
			archive.WriteSignedBits(q.y, value.FY.NUM_BITS);
			archive.WriteSignedBits(q.z, value.FZ.NUM_BITS);
		}

		field = value;
		return archive.isLoading;
	}

	public override bool FieldsAreEqual(object a, object b) {
		return (a != null) && (b != null) && ((QuantizedVector3Field)a).quantizedValue == ((QuantizedVector3Field)b).quantizedValue;
	}

	public override object Copy(object toCopy) {
		return new QuantizedVector3Field((QuantizedVector3Field)toCopy);
	}
}

public class ActorReplicationException : Exception {
	public ActorReplicationException(string message) : base(message) { }
}

public class ActorReplicationChannel : SerializableObjectSubobjectSerializer, SerializableObjectReferenceCollector {
	public const float PONG_TIMEOUT = 60*3;
	const float PING_RATE = 5f;
	const int PING_TABLE_SIZE = 10;
	
	NetConnection _connection;
	HashSetList<int> objectRefs = new HashSetList<int>();
	HashSetList<int> objectRefs2 = new HashSetList<int>();
	HashSetList<int> garbage;
	IntHashtable<IntHashtableList<ObjectReplicator>> pendingRefs = new IntHashtable<IntHashtableList<ObjectReplicator>>();

	float nextPingTime = 0f;
	float pongTimeout = 0f;
	float maxPongTimeout = 0f;
	int[] pings = new int[PING_TABLE_SIZE];
	int nextPing;
	bool pingFull;

	bool isSending;
	public bool didHandshake;
	public string clientLevel;
	public float handshakeTime;
	public Actor owningPlayer;
	public bool isTraveling;
	public bool pendingConnect;
	public bool levelStarted;

	public ulong uuid;
#if BACKEND_SERVER
	public ulong challenge;
#endif

	readonly bool isServer;

	public int ping {
		get;
		private set;
	}

	public ActorReplicationChannel(NetConnection connection) {
		_connection = connection;
		pongTimeout = PONG_TIMEOUT;
		maxPongTimeout = PONG_TIMEOUT;

        if (connection.world is Server.ServerWorld) {
			garbage = new HashSetList<int>();
			isServer = true;
		}
	}

	public bool clientLevelLoaded {
		get {
			return (clientLevel != null) && (clientLevel == _connection.world.currentLevel);
		}
	}

	public void Flush() {
		ReplicateActors(0f);
	}

	public void ReplicateActors(float dt) {
		if (!didHandshake) {
			handshakeTime += Mathf.Min(dt, 1/3f);
			// client hasn't finished connecting
			return;
		}

		Ping(dt);

		if (!clientLevelLoaded) {
			ResetTimeoutForTravel();
			return;
		}

		Perf.Begin("ActorReplicationChannel.ReplicateActors");

		isSending = true;
		objectRefs.Clear();

		var packet = NetMsgs.ReplicatedObjectData.New();

		for (int i = 0; i < connection.world.numReplicatedActors; ++i) {
			var actor = connection.world.GetReplicatedActor(i);
			Assert.IsFalse(actor.disposed);

			var isOwner = actor.ownerConnectionID == _connection.id;
			if (!isOwner && actor.ownerOnly) {
				// actor is not replicated on this channel.
				continue;
			}

			Assert.IsFalse(actor.netTornOff);

			bool isRelevant;
			ObjectReplicator replicator;

			if (!CheckRelevancy(actor, out replicator, out isRelevant)) {
				Assert.IsFalse(actor.internal_NetTearOff);
				continue;
			}

			ReplicateObject(dt, actor, actor, replicator, actor.ownerConnectionID == _connection.id, isRelevant, ref packet);
#if !PACKET_COMBINE
			packet = packet.Flush(connection);
#endif
			if (objectRefs.Values.Count > 0) {
				objectRefs.Remove(actor.netIDHashCode);
			}
		}

		ReplicateDependencies(ref packet);
		packet.Flush(connection);

		if (isTraveling) {
			ResetTimeoutForTravel();
			isTraveling = false;
			GarbageCollect();
			connection.SendReliable(NetMsgs.ServerFinishedTravel.New());
			connection.driverConnection.blocking = false;
		}

		Perf.End();
	}

	public void Ping(float dt) {
		Perf.Begin("Ping");
		nextPingTime -= dt;
		if (nextPingTime <= 0f) {
			nextPingTime = PING_RATE;
#if UNITY_EDITOR && !PROFILING
			pongTimeout = Mathf.Max(pongTimeout, maxPongTimeout);
#else
			connection.SendReliable(NetMsgs.Ping.New(Utils.ReadMilliseconds()));
#endif
		}

		pongTimeout -= Mathf.Min(dt, 1/3f);
		Perf.End();
	}

	public void Pong(NetMsgs.Pong msg) {
		pongTimeout = Mathf.Max(pongTimeout, maxPongTimeout);
		
		var millis = Utils.ReadMilliseconds();
		var delta = millis - msg.time;
		pings[nextPing] = (int)delta;
		++nextPing;
		if (nextPing >= pings.Length) {
			nextPing = 0;
			pingFull = true;
		}

		if (pingFull) {
			ping = 0;

			for (int i = 0; i < pings.Length; ++i) {
				ping += pings[i];
			}

			if (ping < 0) {
				ping = 0;
			}

			ping /= pings.Length;
		}
	}

	public void ResetTimeout() {
		ResetTimeout(PONG_TIMEOUT);
	}

	public void ResetTimeoutForTravel() {
		ResetTimeout();
	}

	void ResetTimeout(float time) {
		pongTimeout = time;
		maxPongTimeout = time;
	}

	public bool timedOut {
		get {
			return pongTimeout <= 0f;
		}
	}

	public float timeSinceLastPong {
		get {
			return maxPongTimeout - pongTimeout;
		}
	}

	public bool HasReplicated(SerializableObject obj) {
		return obj.internal_GetReplicator(connection) != null;
	}

	void GarbageCollect() {
		for (int i = 0; i < garbage.Values.Count; ++i) {
			connection.SendReliable(NetMsgs.DestroyActor.New(garbage.Values[i]));
		}
		garbage.Clear();
	}

	public void ReplicateRPC(SerializableObject context, int rpcID, ObjectRPCSerializer serializer, params object[] args) {
		if (!_connection.isValid) {
			return;
		}

		Perf.Begin("ActorReplicationChannel.ReplicateRPC");

		if (isServer) {
			if (!didHandshake) {
				// client hasn't finished connecting
				Perf.End();
				return;
			}

			if ((clientLevel == null) || (clientLevel != _connection.world.currentLevel)) {
				Perf.End();
				return;
			}
		} else if (_connection.world.isTraveling) {
			Perf.End();
			return;
		}

		if (context.internal_GetReplicator(connection) == null) {
			// has not been replicated.
			Perf.End();
			return;
		}

		var actor = context as Actor;
		if ((actor != null) && isServer && serializer.rpcInfo.CheckRelevancy && !actor.IsNetRelevantFor(this)) {
			// not relevant
			Perf.End();
			return;
		}

		Assert.IsFalse((actor != null) ? actor.netTornOff : false);

		objectRefs.Clear();

		var netMsg = NetMsgs.ReplicatedObjectRPC.New(context.netID, (ushort)rpcID);
		serializer.Write(netMsg.archive, this, args);
		
		if (_connection.world is Server.ServerWorld) {
			// send objects in the argument list first
			ReplicateDependencies();
		} else {
			objectRefs.Clear();
		}

		_connection.SendReliable(netMsg);

		Perf.End();
	}

	public void NetFlush(Actor actor) {
		if (!didHandshake) {
			return;
		}

		if (!clientLevelLoaded || isTraveling) {
			return;
		}

		var isOwner = actor.ownerConnectionID == _connection.id;
		if (!isOwner && actor.ownerOnly) {
			// actor is not replicated on this channel.
			return;
		}

		bool isRelevant;
		ObjectReplicator replicator;

		bool relevancyChanged = CheckRelevancy(actor, out replicator, out isRelevant);
		if (!isRelevant) {
			if (actor.internal_NetTearOff) {
				// this actor is being torn off but is not net relevant anymore
				// so instead of flushing the object state we destroy the actor
				// on this channel.
				if (actor.internal_GetReplicator(connection) != null) {
					connection.SendReliable(NetMsgs.DestroyActor.New(actor.netID));
					actor.internal_SetReplicator(connection, null);
				}
				return;
			} else if (!relevancyChanged) {
				return;
			}
		}

		isSending = true;
		objectRefs.Clear();
		var packet = NetMsgs.ReplicatedObjectData.New();

		ReplicateObject(0f, actor, actor, replicator, actor.ownerConnectionID == _connection.id, isRelevant, ref packet);
		objectRefs.Remove(actor.netIDHashCode);
		
		ReplicateDependencies(ref packet);
		packet.Flush(connection);
	}

	public void ActorWasDestroyed(Actor actor, bool isTraveling) {
		if (actor.netID != 0) {
			if (actor.internal_GetReplicator(connection) != null) {
				actor.internal_SetReplicator(connection, null);
				Assert.IsFalse(actor.netTornOff);

				for (int i = 0; i < actor.numComponents; ++i) {
					var component = actor.GetComponent(i);
					component.internal_SetReplicator(connection, null);
				}

				if (!isTraveling && isServer) {
					if (this.isTraveling) {
						garbage.Add(actor.netID);
					} else {
						connection.SendReliable(NetMsgs.DestroyActor.New(actor.netID));
                    }
				}
			}
		}
	}

	void ReplicateDependencies() {
		var packet = NetMsgs.ReplicatedObjectData.New();
		ReplicateDependencies(ref packet);
		packet.Flush(connection);
	}

	void ReplicateDependencies(ref NetMsgs.ReplicatedObjectData packet) {
		Perf.Begin("ActorReplicationChannel.ReplicateDependencies");

		// Serialize referenced actors.
		objectRefs2.Clear();

		while (objectRefs.Values.Count > 0) {
			Utils.Swap(ref objectRefs, ref objectRefs2);

			for (int i = 0; i < objectRefs2.Values.Count; ++i) {
				int netIDHashCode = objectRefs2.Values[i];
				var obj = connection.world.GetObjectByNetIDHashCode(netIDHashCode);

				bool isOwner = true;
				bool isRelevant = true;
				var actor = obj as Actor;
				ObjectReplicator replicator = null;

				if (actor != null) {
					Assert.IsFalse(actor.disposed);
					if (actor.internal_NetTearOff) {
						continue;
					}
					isOwner = actor.ownerConnectionID == _connection.id;
					if (!isOwner && actor.ownerOnly) {
						// actor is not replicated on this channel.
						continue;
					}
					if (!CheckRelevancy(actor, out replicator, out isRelevant)) {
						// actor is not replicated on this channel.
						continue;
					}
				} else {
					var component = obj as ActorComponent;

					if (component != null) {
						isOwner = component.owner.ownerConnectionID == _connection.id;
						if (!isOwner && component.owner.ownerOnly) {
							// only replicate to owner.
							continue;
						}
					}
				}

				ReplicateObject(0f, obj, actor, replicator, isOwner, isRelevant, ref packet);
#if !PACKET_COMBINE
				packet = packet.Flush(connection);
#endif
			}

			objectRefs2.Clear();
		}

		Perf.End();
	}

	bool CheckRelevancy(Actor actor, out ObjectReplicator repl, out bool isRelevant) {
		Perf.Begin("CheckRelevancy");
		isRelevant = actor.IsNetRelevantFor(this);

		repl = actor.internal_GetReplicator(connection);
		if (repl != null) {
			if (repl.wasRelevant != isRelevant) {
				Perf.End();
				return true;
			}
		}

		Perf.End();
		return isRelevant;
	}

	void ReplicateObject(float dt, SerializableObject obj, Actor actor, ObjectReplicator replicator, bool isOwner, bool isRelevant, ref NetMsgs.ReplicatedObjectData msg) {
		Perf.Begin("ActorReplicationChannel.ReplicateObject");

		Assert.IsFalse((actor != null) ? actor.netTornOff : false);
		
		bool wroteAnything = false;

		// start on a byte boundary so we can restore the position to this point if we don't write any data.
		msg.archive.Flush();
		msg = msg.MaybeSend(connection);

		var position = msg.archive.Position;
		bool relevancyChanged = true;

		CHECK_FLD(msg.archive);

		if (replicator == null) {
			replicator = obj.internal_GetReplicator(connection);
		}

		if (replicator == null) {

			replicator = new ObjectReplicator(this, obj, connection.world.InternalGetReplicatedFields(obj), isOwner);
			obj.internal_SetReplicator(connection, replicator);
			
			msg.archive.Write((ushort)obj.netID);

			CHECK_FLD(msg.archive);

			// actor has not been replicated yet
			msg.archive.WriteUnsignedBits(1, 1);
			msg.archive.WriteUnsignedBits(isRelevant ? 1 : 0, 1);
			msg.archive.WriteUnsignedBits(((actor != null) && (actor.internal_NetTearOff)) ? 1 : 0, 1);

			CHECK_FLD(msg.archive);

			msg.archive.Write((ushort)((obj.outer is SerializableObject) ? (obj.outer as SerializableObject).netID : 0));
			msg.archive.Write(obj.classID);

			CHECK_FLD(msg.archive);

			replicator.FullSerialize(dt, msg.archive);
			SerializeCustomData(obj, msg.archive);
			wroteAnything = true;

		} else {

			relevancyChanged = replicator.wasRelevant != isRelevant;

			bool replicate = true;
			if ((dt >= 0f) && (actor != null) && !actor.internal_NetTearOff && !actor.internal_NetFlush && !relevancyChanged) {
				replicator.serializeDeltaTime += dt;
				if (replicator.serializeDeltaTime < actor.replicateRate) {
					replicate = false;
				}
            }

			if (replicate) {
				msg.archive.Write((ushort)obj.netID);
				CHECK_FLD(msg.archive);
				msg.archive.WriteUnsignedBits(0, 1);
				msg.archive.WriteUnsignedBits(isRelevant ? 1 : 0, 1);
				msg.archive.WriteUnsignedBits(((actor != null) && (actor.internal_NetTearOff)) ? 1 : 0, 1);
				CHECK_FLD(msg.archive);

				wroteAnything = replicator.DeltaSerialize(dt, msg.archive, (actor != null) && (actor.internal_NetFlush || relevancyChanged));
				wroteAnything = SerializeCustomData(obj, msg.archive) || wroteAnything;
			}
		}

		if (wroteAnything || ((actor != null) && (actor.internal_NetFlush || relevancyChanged))) {
			CHECK_FLD(msg.archive);
			obj.SerializeSubobjects(this);
		} else {
			msg.archive.Position = position;
			msg.archive.Discard();
		}

		replicator.wasRelevant = isRelevant;

		if ((actor != null) && actor.internal_NetTearOff) {
			obj.internal_SetReplicator(connection, null);
        }

		Perf.End();
	}

	bool SerializeCustomData(SerializableObject obj, Archive archive) {
		var ofs = archive.Position;
		obj.SerializeCustomData(archive);
		return ofs != archive.Position;
	}

	public void SerializeSubobject(SerializableObject obj) {
		if (obj.replicates) {
			objectRefs.Add(obj.netIDHashCode);
			obj.SerializeSubobjects(this);
		}
	}

	public void HandleReplicatedObjectData(NetMsgs.ReplicatedObjectData objectData) {
#if PACKET_COMBINE
		while (true) {
			objectData.archive.Discard();
			if (objectData.archive.EOS) {
				break;
			}
#endif
			InternalHandleReplicatedObjectData(objectData);
#if PACKET_COMBINE
		}
#else
		if (!objectData.archive.EOS) {
			throw new System.IO.IOException("Replicated object data did not consume its entire payload.");
		}
#endif
	}

	void InternalHandleReplicatedObjectData(NetMsgs.ReplicatedObjectData objectData) {
#if PROFILING
		try {
			Perf.Begin("ActorReplicationChannel.InternalHandleReplicatedObjectData");
#endif
			CHECK_FLD(objectData.archive);

			var netID = objectData.archive.ReadUShort();

			CHECK_FLD(objectData.archive);

			var obj = _connection.world.GetObjectByNetID(netID);

			var create = objectData.archive.ReadUnsignedBits(1) != 0;
			var relevant = objectData.archive.ReadUnsignedBits(1) != 0;
			var tearOff = objectData.archive.ReadUnsignedBits(1) != 0;

			CHECK_FLD(objectData.archive);

			ObjectReplicator replicator;

			if (obj == null) {
				if (!create) {
					throw new ActorReplicationException("Actor replication error (no actor with id " + netID + " and create flag not set)!");
				}
				var netIDOuter = objectData.archive.ReadUShort();
				var outerObj = _connection.world.GetObjectByNetID(netIDOuter);
				var classID = objectData.archive.ReadInt();
				CHECK_FLD(objectData.archive);
				obj = _connection.world.InternalCreateReplicatedObject(classID, netID);
				replicator = new ObjectReplicator(this, obj, _connection.world.InternalGetReplicatedFields(obj), false);
				obj.internal_SetReplicator(connection, replicator);
				obj.PreConstruct(outerObj);
				obj.PreNetConstruct();
			} else {
				if (create) {
					throw new ActorReplicationException("Actor replication error (actor with id " + netID + " create flag not set)!");
				}
				replicator = obj.internal_GetReplicator(connection);
			}

			var actor = obj as Actor;

			bool wasRelevant = true;
			if (actor != null) {
				wasRelevant = actor.isNetRelevant;
				actor.internal_NetRelevant = relevant;
			}

			obj.PreNetReceive();

			isSending = false;
			replicator.DeltaSerialize(0f, objectData.archive, false);
			obj.SerializeCustomData(objectData.archive);
			CHECK_FLD(objectData.archive);

			obj.PostNetReceive();

			if (create) {
				obj.Construct();
				obj.PostNetConstruct();
				obj.PostConstruct();
			}

			replicator.OnRep();
			obj.PostOnRepFields();

			if (create && (actor != null) && !_connection.world.isTraveling) {
				actor.OnLevelStart();
			}

			IntHashtableList<ObjectReplicator> repls;
			if (pendingRefs.TryGetValue(obj.netIDHashCode, out repls)) {
				for (int i = 0; i < repls.Values.Count; ++i) {
					repls.Values[i].ResolveReference(obj, netID);
				}
				for (int i = 0; i < repls.Values.Count; ++i) {
					repls.Values[i].OnRep();
				}
				repls.Clear();
			}

			if ((actor != null) && (wasRelevant != relevant)) {
				actor.OnNetRelevancyChanged();
			}

			if ((actor != null) && tearOff) {
				obj.internal_SetReplicator(connection, null);
				actor.NetTearOff();
			}

#if PROFILING
		} finally {
			Perf.End();
		}
#endif
	}

	public SerializableObject AddObjectReferenceToSerialize(ObjectReplicator replicator, int id) {
		int hashCode = id.GetHashCode();

		if (isSending) {
			var obj = connection.world.GetObjectByNetIDHashCode(hashCode);
			if ((obj != null) && (obj.internal_GetReplicator(connection) == null)) {
				objectRefs.Add(hashCode);
			}
		} else {
			// do we have this object?
			var obj = connection.world.GetObjectByNetIDHashCode(hashCode);
			if (obj != null) {
				return obj;
			}

			IntHashtableList<ObjectReplicator> repls;

			if (!pendingRefs.TryGetValue(hashCode, out repls)) {
				repls = new IntHashtableList<ObjectReplicator>();
				pendingRefs[hashCode] = repls;
			}

			if (!repls.Contains(replicator.obj.netIDHashCode)) {
				repls.Add(replicator.obj.netIDHashCode, replicator);
			}
        }

		return null;
	}

	public SerializableObject AddReference(SerializableObjectFieldSerializer serializer, int id, int fieldIndex) {
		int hashCode = id.GetHashCode();

		var obj = connection.world.GetObjectByNetIDHashCode(hashCode);

		if ((obj != null) && (obj.internal_GetReplicator(connection) == null)) {
			objectRefs.Add(hashCode);
		}

		return null;
	}

	public NetConnection connection {
		get {
			return _connection;
		}
	}

	[System.Diagnostics.Conditional("INTEGRITY_CHECK")]
	static internal void CHECK_FLD(Archive ar) {
		if (ar.isLoading) {
			if (ar.ReadUInt() != 0xF1DD1FACU) {
				throw new Exception("ActorReplication CHECK_FLD - Integrity check failed!");
			}
		} else {
			ar.Write(0xF1DD1FACU);
		}
	}
	
}

public class ObjectReplicator : SerializableObjectReferenceCollector {

	SerializableObject _object;
	IntHashtableList<ReplicatedObjectFieldState> fieldStates;
	public readonly ActorReplicationChannel channel;
	IntHashtable<IntHashtableList<ReplicatedObjectFieldState>> objectReferencedFieldStates;
	ReplicatedObjectFieldState curFieldState;
	static List<FieldReplicateInfo> fieldsToReplicate = new List<FieldReplicateInfo>();
	int numOnReps;
	bool isLoading;
	bool hasReplicated;
	readonly bool isOwner;
	public float serializeDeltaTime;
	public bool wasRelevant;

	struct FieldReplicateInfo {

		public FieldReplicateInfo(int index, object fieldVal, bool deltaField) {
			this.index = index;
			this.fieldVal = fieldVal;
			this.deltaField = deltaField;
		}

		public int index;
		public object fieldVal;
		public bool deltaField;
	}

	public ObjectReplicator(ActorReplicationChannel channel, SerializableObject obj, SerializedObjectFields fields, bool isOwner) {
		this.channel = channel;
		_object = obj;

		wasRelevant = true;
		this.isOwner = isOwner;

        fieldStates = new IntHashtableList<ReplicatedObjectFieldState>();
		for (int i = 0; i < fields.serializedFields.Values.Count; ++i) {
			SerializedObjectFields.FieldSpec fieldSpec = fields.serializedFields.Values[i];
			fieldStates.Add(fieldSpec.fieldID, new ReplicatedObjectFieldState(fieldSpec));
		}
	}

	public SerializableObject AddReference(SerializableObjectFieldSerializer serializer, int id, int fieldIndex) {
		var obj = channel.AddObjectReferenceToSerialize(this, id);
		if ((obj != null) || !isLoading) {
			return obj;
		}

		int idHash = id.GetHashCode();

		if (objectReferencedFieldStates == null) {
			objectReferencedFieldStates = new IntHashtable<IntHashtableList<ReplicatedObjectFieldState>>();
		}
		IntHashtableList<ReplicatedObjectFieldState> fields;
		if (!objectReferencedFieldStates.TryGetValue(idHash, out fields)) {
			fields = new IntHashtableList<ReplicatedObjectFieldState>();
			objectReferencedFieldStates[idHash] = fields;
		}
		if (!fields.Contains(curFieldState.fieldSpec.fieldID)) {
			fields.Add(curFieldState.fieldSpec.fieldID, curFieldState);
		}
		curFieldState.AddReference(id, fieldIndex);
		return null;
	}

	public void ResolveReference(SerializableObject resolved, int id) {
		
		IntHashtableList<ReplicatedObjectFieldState> fields;
		if (objectReferencedFieldStates.TryGetValue(resolved.netIDHashCode, out fields)) {
			for (int i = 0; i < fields.Values.Count; ++i) {
				fields.Values[i].ResolveReference(_object, resolved, id);
				if (fields.Values[i].needsRep) {
					++numOnReps;
				}
			}
			fields.Clear();
		}
	}

	public void OnRep() {
		if (numOnReps > 0) {
			for (int i = 0; i < fieldStates.Values.Count; ++i) {
				var field = fieldStates.Values[i];
				if (field.needsRep) {
					field.fieldSpec.onRep.Invoke(_object, null);
					field.needsRep = false;
					--numOnReps;

					if (numOnReps == 0) {
						return;
					}
				}
			}
		}
	}

	public bool FullSerialize(float dt, NetArchive archive) {
		var r = SerializeFields(serializeDeltaTime + dt, archive, false, true);
		serializeDeltaTime = 0f;
		return r;
	}

	public bool DeltaSerialize(float dt, NetArchive archive, bool netFlush) {
		var r = SerializeFields(serializeDeltaTime + dt, archive, true, netFlush);
		serializeDeltaTime = 0f;
		return r;
	}

	bool SerializeFields(float dt, NetArchive archive, bool deltasOnly, bool netFlush) {
		Perf.Begin("SerializeFields");

		ActorReplicationChannel.CHECK_FLD(archive);

		isLoading = archive.isLoading;
		if (isLoading) {
			byte numBits = archive.ReadByte();
			Assert.IsTrue(numBits <= SerializedObjectFields.MAX_REPLICATED_FIELDS);
			uint fieldBits = archive.ReadUnsignedBits(numBits);

			ActorReplicationChannel.CHECK_FLD(archive);

			for (int i = 0; i < numBits; ++i) {
				if (((1U << i) & fieldBits) != 0) {
					var fieldState = fieldStates.Values[i];
					
					curFieldState = fieldState;
					object obj = fieldState.fieldSpec.field.GetValue(_object);
					object orig = obj;

					ActorReplicationChannel.CHECK_FLD(archive);

					if (fieldState.fieldSpec.serializer.Serialize(archive, this, ref obj, null)) {
						fieldState.needsRep = fieldState.fieldSpec.onRep != null;

						if (fieldState.needsRep) {
							++numOnReps;
						}
					}

					ActorReplicationChannel.CHECK_FLD(archive);

					if (obj != orig) {
						fieldState.fieldSpec.field.SetValue(_object, obj);
					}
				}
			}

			curFieldState = null;
			Perf.End();
			return numBits > 0;
		} else {

			fieldsToReplicate.Clear();

			byte numBitsWritten = 0;
			uint fieldBits = 0;

			for (int i = 0; i < fieldStates.Values.Count; ++i) {
				var field = fieldStates.Values[i];

				object fieldVal;
				bool deltaField;

				if (field.IsDirty(channel, _object, dt, deltasOnly, hasReplicated, isOwner, netFlush, out fieldVal, out deltaField)) {
					numBitsWritten = (byte)(i + 1);
					fieldBits |= 1U << i;
					fieldsToReplicate.Add(new FieldReplicateInfo(i, fieldVal, deltaField));
				}
			}

			Assert.IsTrue(numBitsWritten <= SerializedObjectFields.MAX_REPLICATED_FIELDS);
			archive.Write(numBitsWritten);
			archive.WriteUnsignedBits(fieldBits, numBitsWritten);

			ActorReplicationChannel.CHECK_FLD(archive);

			for (int i = 0; i < fieldsToReplicate.Count; ++i) {
				var info = fieldsToReplicate[i];
				var field = fieldStates.Values[info.index];
				ActorReplicationChannel.CHECK_FLD(archive);
				field.Write(archive, this, info.fieldVal, info.deltaField);
				ActorReplicationChannel.CHECK_FLD(archive);
			}

			fieldsToReplicate.Clear();

			hasReplicated = true;

			Perf.End();
			return numBitsWritten > 0;
		}
	}

	public SerializableObject obj {
		get {
			return _object;
		}
	}
}

public class ReplicatedObjectFieldState {
	object lastState;
	DictionaryList<int, HashSetList<int>> objReferences;
	public SerializedObjectFields.FieldSpec fieldSpec;
	public float nextSendTime;
	public bool needsRep;

	public ReplicatedObjectFieldState(SerializedObjectFields.FieldSpec fieldSpec) {
		this.fieldSpec = fieldSpec;
	}

	public void AddReference(int id, int fieldIndex) {
		if (objReferences == null) {
			objReferences = new DictionaryList<int, HashSetList<int>>();
		}
		HashSetList<int> subFields;
		if (!objReferences.TryGetValue(id, out subFields)) {
			subFields = new HashSetList<int>();
			objReferences[id] = subFields;
		}
		subFields.Add(fieldIndex);
	}

	public void ResolveReference(SerializableObject container, SerializableObject resolved, int id) {
		HashSetList<int> subFields;
		if (objReferences.TryGetValue(id, out subFields)) {
			if (subFields != null) {
				object obj = fieldSpec.field.GetValue(container);
				object orig = obj;
				for (int i = 0; i < subFields.Values.Count; ++i) {
					fieldSpec.serializer.ResolveReference(resolved, id, subFields.Values[i], ref obj);
				}
				if (obj != orig) {
					fieldSpec.field.SetValue(container, obj);
				}
				subFields.Clear();
				needsRep = fieldSpec.onRep != null;
			}
		}
	}

	public void Write(NetArchive archive, SerializableObjectReferenceCollector collector, object fieldVal, bool deltaField) {
		Perf.Begin("FieldState.Write");

		fieldSpec.serializer.Serialize(archive, collector, ref fieldVal, deltaField ? lastState : null);

		if (deltaField) {
			lastState = fieldSpec.serializer.Copy(fieldVal);
		}

		Perf.End();
	}

	public bool IsDirty(ActorReplicationChannel channel, SerializableObject container, float dt, bool deltasOnly, bool hasReplicated, bool isOwner, bool netFlush, out object fieldValue, out bool deltaField) {
		Perf.Begin("FieldState.IsDirty");

		bool shouldReplicate = true;

		if (fieldSpec.replication.Condition != EReplicateCondition.Always) {
			switch (fieldSpec.replication.Condition) {
				case EReplicateCondition.InitialOnly:
					shouldReplicate = !hasReplicated;
				break;
				case EReplicateCondition.OwnerOnly:
					shouldReplicate = isOwner;
				break;
				case EReplicateCondition.SkipOwner:
					shouldReplicate = !isOwner;
				break;
				case EReplicateCondition.InitialOrOwner:
					shouldReplicate = !hasReplicated || isOwner;
				break;
				case EReplicateCondition.InitialOwnerOnly:
					shouldReplicate = !hasReplicated && isOwner;
				break;
			}
		}

		if (!shouldReplicate) {
			deltaField = false;
			fieldValue = null;
			Perf.End();
			return false;
		}

		if (!netFlush) {
			nextSendTime -= dt;
			if (nextSendTime > 0f) {
				deltaField = false;
				fieldValue = null;
				Perf.End();
				return false;
			}
		}

		// Update send time here: if we don't send because the value hasn't changed
		// we don't want to check again until after the UpdateRate has passed.
		nextSendTime = fieldSpec.replication.UpdateRate;
		
		fieldValue = fieldSpec.field.GetValue(container);

		// check ownerOnly object replication.
		if (fieldSpec.isObjectReference) {
			var actor = fieldValue as Actor;
			if ((actor != null) && actor.ownerOnly) {
				if (actor.ownerConnection != channel) {
					deltaField = false;
					Perf.End();
					return false;
				}
			}
		}

		deltaField = !fieldSpec.serializer.FieldsAreEqual(lastState, fieldValue);

		if (deltasOnly && !deltaField) {
			Perf.End();
			return false;
		}

		Perf.End();
		return true;
	}

	public override bool Equals(object obj) {
		return obj == this;
	}

	public override int GetHashCode() {
		return fieldSpec.fieldID;
	}
}

public sealed class ReplicatedObjectReferenceFieldSerializer : SerializableObjectFieldSerializer {

	static ReplicatedObjectReferenceFieldSerializer _instance;

	static ReplicatedObjectReferenceFieldSerializer() {
		_instance = new ReplicatedObjectReferenceFieldSerializer();
	}

	public static ReplicatedObjectReferenceFieldSerializer instance {
		get {
			return _instance;
		}
	}

	public bool Serialize(Archive archive, SerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
		if (archive.isLoading) {
			int id = archive.ReadUShort();
			if (id != 0) {
				field = collector.AddReference(this, id, 0);
				if (field == null) {
					// no onrep until the reference is received.
					return false;
				}
			} else {
				bool r = field != null;
				field = null;
				return r;
			}
		} else {
			SerializableObject obj = (SerializableObject)field;
			Actor actor = obj as Actor;
			if ((obj == null) || !obj.replicates || ((actor != null) && actor.netTornOff)) {
				archive.Write((ushort)0);
			} else {
				archive.Write((ushort)obj.netID);
				collector.AddReference(this, obj.netID, 0);
			}
		}
		return archive.isLoading;
	}

	public bool FieldsAreEqual(object a, object b) {
		return a == b;
	}

	public object Copy(object toCopy) {
		return toCopy;
	}

	public void ClearState() {
		
	}

	public void ResolveReference(SerializableObject obj, int id, int fieldIndex, ref object field) {
		field = obj;
	}
}

public sealed class ReplicatedObjectFieldSerializerFactory : SerializableObjectFieldSerializerFactory {
	static ReplicatedObjectFieldSerializerFactory _instance;

	static ReplicatedObjectFieldSerializerFactory() {
		_instance = new ReplicatedObjectFieldSerializerFactory();
	}

	public static ReplicatedObjectFieldSerializerFactory instance {
		get {
			return _instance;
		}
	}

	public SerializableObjectFieldSerializer GetSerializerForField(SerializedObjectFields.FieldSpec field) {

		if (field.replication.Using != null) {
			return CreateSerializer(field.replication.Using);
		}

		return GetSerializerForType(field.field.FieldType);
	}

	SerializableObjectFieldSerializer CreateSerializer(Type type) {
		// custom serialization
		if (type == null) {
			throw new ObjectSerializationException("Serializer type is null.");
		}

		if (!typeof(SerializableObjectFieldSerializer).IsAssignableFrom(type)) {
			throw new ObjectSerializationException("Serializer must be derived from SerializableObjectFieldSerializer");
		}
		if (type.IsAbstract) {
			throw new ObjectSerializationException("Serializer is an abstract class");
		}
		var property = type.GetProperty("instance", BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy);
		if (property != null) {
			var getter = property.GetGetMethod();
			if (getter != null) {
				if (getter.ReturnType != type) {
					throw new ObjectSerializationException("Static serializer instance is wrong type!");
				}
				return (SerializableObjectFieldSerializer)getter.Invoke(null, null);
			}
		}

		var ctor = type.GetConstructor(System.Type.EmptyTypes);
		if (ctor != null) {
			return (SerializableObjectFieldSerializer)ctor.Invoke(null);
		}

		throw new ObjectSerializationException("Serializer does not have a parameterless constructor and therefore could not be instantiated!");
	}

	ReplicatedUsing GetReplicatedUsingAttribute(Type type) {
		var attrs = type.GetCustomAttributes(typeof(ReplicatedUsing), false);
		return (attrs.Length > 0) ? (ReplicatedUsing)attrs[0] : null;
	}

	public SerializableObjectFieldSerializer GetSerializerForType(Type type) {
		if (type.IsEnum) {
			return SerializableObjectEnumFieldSerializer.instance;
		} else if (type == typeof(bool)) {
			return SerializableObjectBoolFieldSerializer.instance;
		} else if (type == typeof(byte)) {
			return SerializableObjectByteFieldSerializer.instance;
		} else if (type == typeof(sbyte)) {
			return SerializableObjectSByteFieldSerializer.instance;
		} else if (type == typeof(short)) {
			return SerializableObjectInt16FieldSerializer.instance;
		} else if (type == typeof(ushort)) {
			return SerializableObjectUInt16FieldSerializer.instance;
		} else if (type == typeof(int)) {
			return SerializableObjectInt32FieldSerializer.instance;
		} else if (type == typeof(uint)) {
			return SerializableObjectUInt32FieldSerializer.instance;
		} else if (type == typeof(long)) {
			return SerializableObjectInt64FieldSerializer.instance;
		} else if (type == typeof(ulong)) {
			return SerializableObjectUInt64FieldSerializer.instance;
		} else if (type == typeof(float)) {
			return SerializableObjectFloatFieldSerializer.instance;
		} else if (type == typeof(double)) {
			return SerializableObjectDoubleFieldSerializer.instance;
		} else if (type == typeof(Vector2)) {
			return SerializableObjectVector2FieldSerializer.instance;
		} else if (type == typeof(Vector3)) {
			return SerializableObjectVector3FieldSerializer.instance;
		} else if (type == typeof(Vector4)) {
			return SerializableObjectVector4FieldSerializer.instance;
		} else if (type == typeof(Quaternion)) {
			return SerializableObjectQuaternionFieldSerializer.instance;
		} else if (type == typeof(Matrix4x4)) {
			return SerializableObjectMatrix4x4FieldSerializer.instance;
		} else if (type == typeof(Color)) {
			return SerializableObjectColorFieldSerializer.instance;
		} else if (type == typeof(Color32)) {
			return SerializableObjectColor32FieldSerializer.instance;
		} else if (type == typeof(string)) {
			return SerializableObjectStringFieldSerializer.instance;
		} else if (typeof(SerializableObject).IsAssignableFrom(type)) {
			return ReplicatedObjectReferenceFieldSerializer.instance;
		} else if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>))) {
			var baseType = typeof(SerializableObjectListFieldSerializer<>);
			var serializerType = baseType.MakeGenericType(type.GetGenericArguments()[0]);
			return Activator.CreateInstance(serializerType, new object[] { this }) as SerializableObjectFieldSerializer;
		} else if (!type.IsAbstract && typeof(SerializableObjectFieldSerializer).IsAssignableFrom(type)) {
			return Activator.CreateInstance(type, null) as SerializableObjectFieldSerializer;
		} else {
			// type may have a ReplicatedUsing attribute.
			var replicatedUsing = GetReplicatedUsingAttribute(type);
			if (replicatedUsing != null) {
				return CreateSerializer(replicatedUsing.serializerType);
			}
		}

		throw new ObjectSerializationException("Replication is not supported for " + type.FullName);
	}
}

public sealed class ObjectRPCSerializer {
	List<SerializableObjectFieldSerializer> argumentSerializers = new List<SerializableObjectFieldSerializer>();
	object[] rpcArgs;
	RPC rpc;
	MethodInfo _method;
	ParameterInfo[] _parameters;

	public ObjectRPCSerializer(RPC rpcInfo, MethodInfo method) {
		rpc = rpcInfo;
		_method = method;

		_parameters = method.GetParameters();
		rpcArgs = new object[_parameters.Length];

		for (int i = 0; i < _parameters.Length; ++i) {
			var p = _parameters[i];

			argumentSerializers.Add(ReplicatedObjectFieldSerializerFactory.instance.GetSerializerForType(p.ParameterType));
		}
	}

	public void Write(NetArchive archive, SerializableObjectReferenceCollector collector, params object[] args) {
		if (args.Length != argumentSerializers.Count) {
			throw new ArgumentException("Wrong number of arguments for RPC!");
		}
		
		for (int i = 0; i < args.Length; ++i) {
			object r = args[i];
			argumentSerializers[i].Serialize(archive, collector, ref r, null);
		}
    }

	public object[] Read(NetArchive archive, SerializableObjectReferenceCollector collector) {
		
		for (int i = 0; i < argumentSerializers.Count; ++i) {
			var p = _parameters[i];

			if (p.ParameterType.IsValueType) {
				rpcArgs[i] = Activator.CreateInstance(p.ParameterType);
			} else {
				rpcArgs[i] = null;
			}

			argumentSerializers[i].Serialize(archive, collector, ref rpcArgs[i], null);
		}

		return rpcArgs;
	}

	public void Flush() {
		for (int i = 0; i < argumentSerializers.Count; ++i) {
			rpcArgs[i] = null;
		}
	}

	public RPC rpcInfo {
		get {
			return rpc;
		}
	}

	public MethodInfo method {
		get {
			return _method;
		}
	}
}

public sealed class ObjectRPCTable {
	
	IntHashtable<ObjectRPCSerializer> rpcIDToRPC = new IntHashtable<ObjectRPCSerializer>();
	Dictionary<string, int> methods = new Dictionary<string, int>();
	Type _serverType;
	Type _clientType;

	public ObjectRPCTable(Type serverType, Type clientType) {
		_serverType = serverType;
		_clientType = clientType;

		AddRPCMethods(serverType);
		if (serverType != clientType) {
			AddRPCMethods(clientType);
		}
	}

	public ObjectRPCSerializer GetObjectRPCByID(int rpcID) {
		return rpcIDToRPC[rpcID.GetHashCode()];
	}

	public int GetMethodID(MethodInfo minfo) {
		minfo = minfo.GetBaseDefinition();
		var sig = GenerateMethodSignature(minfo);
		int id;
		if (methods.TryGetValue(sig, out id)) {
			return id;
		}
		return -1;
	}

	public void Validate(SerializableObject obj) {
		if (_serverType != obj.serverType ||
			_clientType != obj.clientType) {
			throw new InvalidReplicatedObjectClassException("Replicated object classID collision! (" + _serverType.FullName + ", " + _clientType.FullName + "), (" + obj.serverType.FullName + ", " + obj.clientType.FullName + ")");
		}
	}

	static string GenerateMethodSignature(MethodInfo minfo) {
		string sig = minfo.ReturnType.ToString() + " " + minfo.DeclaringType.FullName + "." + minfo.Name + "(";

		var parameters = minfo.GetParameters();
		foreach (var p in parameters) {
			sig += p.ParameterType.FullName + ",";
		}

		sig += ")";

		return sig; 
	}
	
	void AddRPCMethods(Type t) {
		AddRPCMethods(t, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
		for (; t != typeof(object); t = t.BaseType) {
			AddRPCMethods(t, BindingFlags.NonPublic|BindingFlags.Instance);
		}
	}

	void AddRPCMethods(Type t, BindingFlags flags) {
		var typeMethods = t.GetMethods(flags);
		foreach (var minfo in typeMethods) {
			var rpcAttrs = minfo.GetCustomAttributes(typeof(RPC), false);

			if (rpcAttrs.Length == 1) {
				var actualMethod = minfo.GetBaseDefinition();
				var sig = GenerateMethodSignature(actualMethod);
				if (!methods.ContainsKey(sig)) {
					int rpcID = methods.Count;
					methods.Add(sig, rpcID);
					var actorRPC = new ObjectRPCSerializer(rpcAttrs[0] as RPC, actualMethod);
					rpcIDToRPC.Add(rpcID, actorRPC);
				}
			}
		}
	}
}

public sealed class ObjectTypeRPCTable {
	static ObjectTypeRPCTable _instance;
	IntHashtable<ObjectRPCTable> rpcTables = new IntHashtable<ObjectRPCTable>();

	static ObjectTypeRPCTable() {
		_instance = new ObjectTypeRPCTable();
	}

	public static ObjectTypeRPCTable instance {
		get {
			return _instance;
		}
	}

	public ObjectRPCTable GetRPCTable(SerializableObject obj) {
		ObjectRPCTable table;
		if (!rpcTables.TryGetValue(obj.classID, out table)) {
			table = new ObjectRPCTable(obj.serverType, obj.clientType);
			rpcTables[obj.classID] = table;
		}

		table.Validate(obj);

		return table;
	}
}