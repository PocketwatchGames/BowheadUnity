// Copyright (c) 2018 Pocketwatch Games LLC.
#if !DEDICATED_SERVER
#define LEAK_TRACKING
#endif

using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public struct SpawnParameters {
	public Action<Actor> preConstruct;
}

public enum ERemoteRole {
	// Not replicated
	None,
	// Server side object
	Authority,
	// Client owned object
	AutonomousProxy,
	// Server controlled object on client
	SimulatedProxy
}

public enum EActorGameObjectPoolingMode {
	None,
	Static,
	Transient
}

public abstract class Actor<T> : Actor where T : Actor<T> {

	public override Type clientType {
		get {
			return typeof(T);
		}
	}

	public override Type serverType {
		get {
			return typeof(T);
		}
	}

}

#if UNITY_EDITOR
[HideInInspector] // Hide from FlowCanvas inspector
#endif
public abstract class Actor : ActorRPCObject {
	
	float _replicateRate = 1/30f;
	World _world;
	ERemoteRole _role = ERemoteRole.None;

	[Replicated(Condition = EReplicateCondition.InitialOwnerOnly, Notify = "OnRep_ownerConnectionID")]
	int _ownerConnectionID;

	[Replicated(Condition = EReplicateCondition.InitialOnly)]
	ushort _spawnTagID;

	ActorReplicationChannel _ownerConnection;
	List<ActorComponent> components = new List<ActorComponent>();
	ObjectRPCTable _rpcTable;
	bool _disposed;
	bool _ownerOnly;
	bool _netTornOff;
	List<UnityEngine.Object> _destroyList;

	[SerializeField]
	bool _pendingKill;
	[SerializeField]
	float _lifetime;

	GameObject _go;
	bool externalGO;
	ActorSpawnTag _spawnTag;
	int _physicsFrames;
	
	const int POOL_SIZE = 32;
	static GameObjectPool staticPool;

#if LEAK_TRACKING
	static List<WeakReference> _refs = new List<WeakReference>();
	static int _refCount;
	int _ref;
	string _path = "<Uninitialized>";
#endif

	public Actor() {
		if (!activating) {
			_rpcTable = ObjectTypeRPCTable.instance.GetRPCTable(this);
			internal_NetRelevant = true;
		}
#if LEAK_TRACKING
		++_refCount;
		_ref = _refCount;
		_refs.Add(new WeakReference(this));
#endif
	}

	public static void DumpLeakedActors() {
#if LEAK_TRACKING
		int count = 0;
		for (int i = _refs.Count - 1; i >= 0; --i) {
			var w = _refs[i];
			Actor actor = (w != null) ? w.Target as Actor : null;
			if (actor == null) {
				_refs.RemoveAt(i);
			} else {
				++count;
			}
		}

		Debug.Log("*** BEGIN LEAKED ACTORS: Count = " + count + " ***");

		for (int i = 0; i < _refs.Count; ++i) {
			Actor actor = (Actor)_refs[i].Target;
			if (actor != null) {
				Debug.Log("*** [" + actor._ref + "] : " + actor._path + " ***");
				Debug.Log("Class: " + actor.GetType().FullName);
				Debug.Log("NetID: " + actor.netID);
				Debug.Log("Disposed = " + actor.disposed);
				Debug.Log("Authority = " + actor.hasAuthority);
				Debug.Log("OwnerConnection " + ((actor.ownerConnection != null) ? "!= null" : "== null"));
				actor.DumpLeakedActor();
			}
		}

		Debug.Log("*** END LEAKED ACTORS ***");
#endif
	}

	protected virtual void DumpLeakedActor() {}

	public override World world {
		get {
			return _world;
		}
	}

	public float replicateRate {
		get {
			return _replicateRate;
		}
	}
	
	public ERemoteRole role {
		get {
			return _role;
		}
	}

	public int ownerConnectionID {
		get {
			return _ownerConnectionID;
		}
	}

	public bool ownerOnly {
		get {
			return _ownerOnly;
		}
	}

	public sealed override ActorReplicationChannel ownerConnection {
		get {
			return _ownerConnection;
		}
	}

	public sealed override ObjectRPCTable rpcTable {
		get {
			return _rpcTable;
		}
	}

	public bool hasAuthority {
		get {
			return _role < ERemoteRole.AutonomousProxy;
		}
	}

	public sealed override bool netTornOff {
		get {
			return _netTornOff;
		}
	}

	public bool internal_NetTearOff {
		get; private set;
	}

	public bool internal_NetFlush {
		get; private set;
	}

	public bool internal_NetRelevant {
		get; set;
	}

	public bool isNetRelevant {
		get {
			return internal_NetRelevant;
		}
	}

	public bool justSpawnedPhysics {
		get {
			return _physicsFrames < 2;
		}
	}

	public virtual void OnNetRelevancyChanged() { }

	public void NetTearOff() {
		if (hasAuthority) {
			internal_NetTearOff = true;
			NetFlush();
			_netTornOff = true;
			world.NetTornOff(this);
			OnNetTornOff();
		} else if (!_netTornOff) {
			_netTornOff = true;
			OnNetTornOff();
		}
	}

	protected virtual void OnNetTornOff() {}

	public void SetWorld(World world) {
		_world = world;
	}

	public void SetRemoteRole(ERemoteRole role) {
		_role = role;
	}

	public void SetOwningConnection(ActorReplicationChannel channel) {
		_ownerConnection = channel;
		_ownerConnectionID = channel.connection.id;
	}

	public void SetOwnerOnly(bool ownerOnly) {
		_ownerOnly = ownerOnly;
	}

	public void SetSpawnTagID(ushort spawnID) {
		_spawnTagID = spawnID;
	}

	public ushort spawnTagID {
		get {
			return _spawnTagID;
		}
	}

	public virtual void BeginTravel() {
		for (int i = 0; i < components.Count; ++i) {
			components[i].BeginTravel();
		}
	}

	public virtual void FinishTravel() {
		for (int i = 0; i < components.Count; ++i) {
			components[i].FinishTravel();
		}
	}

	public virtual void Tick() {
		if (Bowhead.GameManager.instance.fixedUpdateDidRun) {
			++_physicsFrames;
		}
	}

	public virtual void ClientFixedUpdate(float dt) { }

	public virtual void LateTick() { }

	public virtual void TickComponents() {
		for (int i = 0; i < components.Count; ++i) {
			components[i].Tick();
		}
	}

	public void TickLifetime() {
		if (_lifetime > 0f) {
			_lifetime -= world.deltaTime;
			if (_lifetime <= 0f) {
				Destroy();
				_lifetime = 0f;
			}
		}
	}

	public virtual bool nonReplicatedActorShouldTravel {
		get {
			return false;
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			for (int i = 0; i < components.Count; ++i) {
				components[i].Dispose();
			}
			components = null;

			DestroyGameObject();

			GCObjects();
			_destroyList = null;
			_world = null;
			_ownerConnection = null;
			_rpcTable = null;
		}
	}

	public Vector3 GetVectorTo(Actor other) {
		return other.go.transform.position - go.transform.position;
	}

	public float GetSqDistanceTo(Actor other) {
		return GetVectorTo(other).sqrMagnitude;
	}

	public float GetDistanceTo(Actor other) {
		return GetVectorTo(other).magnitude;
	}

	protected T AddGC<T>(T obj) where T : UnityEngine.Object {
		if (_destroyList == null) {
			_destroyList = new List<UnityEngine.Object>();
		}
		_destroyList.Add(obj);
		return obj;
	}

	protected void RemoveGC(UnityEngine.Object obj) {
		if (_destroyList != null) {
			_destroyList.Remove(obj);
		}
	}

	protected void GCObjects() {
		if (_destroyList != null) {
			for (int i = 0; i < _destroyList.Count; ++i) {
				var gc = _destroyList[i];
				if (gc != null) {
					UnityEngine.Object.Destroy(gc);
				}
			}
			_destroyList.Clear();
		}
	}

	void DestroyGameObject() {
		bool shouldDestroyGO = true;

		if (refTag != null) {
			if (ReferenceEquals(refTag.serverActor, this)) {
				refTag.SetServerActor(null);
				shouldDestroyGO = (refTag.clientActor == null);
			} else if (ReferenceEquals(refTag.clientActor, this)) {
				refTag.SetClientActor(null);
				shouldDestroyGO = (refTag.serverActor == null);
			}
		}

		if (shouldDestroyGO) {
			if (world.disposed || (gameObjectPoolingMode == EActorGameObjectPoolingMode.Transient)) {
				// world is unloading or object pool is rooted in 
				// the transient object blocks so just clear it, 
				// when the scene unloads it'll go away.
				gameObjectPool = null;
			} else if ((_go != null) && (gameObjectPool != null) && !externalGO) {
				gameObjectPool.ReturnObject(_go);
			} else if ((_go != null) && externalGO) {
				Utils.DestroyGameObject(_go);
			}

			if (world.isTraveling && (gameObjectPool != null)) {
				// traveling, our GO will get destroyed.
				gameObjectPool.Compact(initialGameObjectPoolSize);
			}
		}

		_go = null;
		_spawnTag = null;
		refTag = null;
	}

	public void AttachExternalGameObject(GameObject go) {
		DestroyGameObject();
		_spawnTag = null;
		refTag = go.GetComponent<ActorReference>();
		if (refTag == null) {
			refTag = go.AddComponent<ActorReference>();
		}
		externalGO = true;
		_go = go;

		if (refTag != null) {
			if (world is Server.ServerWorld) {
				refTag.SetServerActor(this);
			} else {
				refTag.SetClientActor(this);
			}
		}

		OnGameObjectAttached();
	}

	public void DetachGameObject() {
		if (externalGO) {
			if (refTag != null) {
				if (ReferenceEquals(refTag.serverActor, this)) {
					refTag.SetServerActor(null);
				} else if (ReferenceEquals(refTag.clientActor, this)) {
					refTag.SetClientActor(null);
				}

				if ((refTag.serverActor == null) && (refTag.clientActor == null)) {
					Utils.DestroyComponent(refTag);
				}
			} else if (_spawnTag != null) {
				Utils.DestroyComponent(_spawnTag);
			}

			_go = null;
			refTag = null;
			_spawnTag = null;
			externalGO = false;
		}
	}

	protected virtual void OnGameObjectAttached() { }

	internal void Dispose() {
		if (!_disposed) {
			_pendingKill = true;
			_disposed = true;
			Dispose(true);
		}
	}

	protected virtual GameObject prefab {
		get {
			return world.defaultActorPrefab;
		}
	}

	protected virtual EActorGameObjectPoolingMode gameObjectPoolingMode {
		get {
			return EActorGameObjectPoolingMode.None;
		}
	}

	protected virtual GameObjectPool gameObjectPool {
		get {
			return staticPool;
		}
		set {
			staticPool = value;
		}
	}

	protected virtual int initialGameObjectPoolSize {
		get {
			return POOL_SIZE;
		}
	}

	public GameObject go {
		get {
			return _go;
		}
		protected set {
			_go = value;
		}
	}

	public ActorSpawnTag spawnTag {
		get {
			return _spawnTag;
		}
	}

	public ActorReference refTag {
		get;
		private set;
	}

	public bool disposed {
		get {
			return _disposed;
		}
	}

	public override void Construct() {
		base.Construct();
		AttachActorSpawnTag();
	}

	public virtual void OnLevelStart() { }

	public void AttachActorSpawnTag() {
		if (_go == null) {
			var tag = world.GetActorSpawnTag(this);
			if (tag != null) {
				
				// clone the tag
				if (!tag.replicates || (Bowhead.GameManager.instance.serverWorld == null) || (world is Server.ServerWorld)) {
					var newTag = tag.clone ? GameObject.Instantiate(tag) : tag;
					newTag.isInstance = true;
					newTag.transform.SetParent(tag.transform.parent, false);
					newTag.original = tag;
					tag = newTag;
				} else {
					var serverActor = (Actor)Bowhead.GameManager.instance.serverWorld.GetObjectByNetID(netID);
					if (serverActor != null) {
						tag = serverActor.spawnTag;
					} else { // server instance was destroyed on the same frame it was spawn'ed so make a new one.
						tag = world.GetActorSpawnTag(this);
						var newTag = tag.clone ? GameObject.Instantiate(tag) : tag;
						newTag.isInstance = true;
						newTag.transform.SetParent(tag.transform.parent, false);
						newTag.original = tag;
						tag = newTag;
					}
				}
				
				_spawnTag = tag;
				refTag = _spawnTag;
				CreateOrAttachActorGameObject();
				externalGO = true;
#if LEAK_TRACKING
				_path = tag.gameObject.transform.GetPath();
#endif
				if (go != null) {
					go.SetActive(true);
				}
				SetReplicates(_spawnTag.replicates);
				if (world is Server.ServerWorld) {
					Assert.IsNull(tag.serverActor);
					tag.SetServerActor(this);
				} else {
					Assert.IsNull(tag.clientActor);
					tag.SetClientActor(this);
				}
				OnActorTagAttached();
				OnGameObjectAttached();
			}
		}
	}

	protected virtual void OnActorTagAttached() { }

	protected void CreateOrAttachActorGameObject() {
		if ((_go == null) && (spawnTag != null)) {
			_go = spawnTag.gameObject;
			externalGO = false;
		}
		if (_go == null) {
			_go = CreateActorGameObject();

			if (_go != null) {
				refTag = _go.GetComponent<ActorReference>();
				if (refTag != null) {
					if (world is Server.ServerWorld) {
						refTag.SetServerActor(this);
					} else {
						refTag.SetClientActor(this);
					}
				}
			}
		}
	}

	protected virtual GameObject CreateActorGameObject() {
		GameObject go = null;

		if (gameObjectPoolingMode == EActorGameObjectPoolingMode.None) {
			go = new GameObject();
			externalGO = true;
		} else {
			externalGO = false;
			if (gameObjectPool == null) {
				gameObjectPool = new GameObjectPool(
					prefab,
					(gameObjectPoolingMode == EActorGameObjectPoolingMode.Static) ? world.staticObjectPoolRoot : world.transientObjectPoolRoot,
					initialGameObjectPoolSize
				);
			}
			go = gameObjectPool.GetObject();
		}

		go.name = GetType().Name;
		go.transform.parent = world.sceneGroup;

		return go;
	}

	public virtual bool IsNetRelevantFor(ActorReplicationChannel channel) {
		return true;
	}

	public void NetFlush() {
		if (replicates && !netTornOff) {
			internal_NetFlush = true;
			var svWorld = (Server.ServerWorld)world;
			svWorld.NetFlush(this);
			internal_NetFlush = false;
		}
	}

	public float lifetime {
		get {
			return _lifetime;
		}
	}

	public bool pendingKill {
		get {
			return _pendingKill;
		}
	}

	public void SetLifetime(float time) {
		_lifetime = time;
		_pendingKill = false;
	}

	protected virtual void OnDestroy() {}

	public sealed override void Destroy() {
		if (replicates && !netTornOff && !hasAuthority) {
			if (go != null) {
				throw new Exception("(" + go.transform.GetPath() + "): A client cannot destroy an actor controlled by the server!");
			} else {
				throw new Exception("(null): A client cannot destroy an actor controlled by the server!");
			}
		}
		OnDestroy();
		_pendingKill = true;
		base.Destroy();
	}

	public T AddComponent<T>() where T : ActorComponent, new() {
		T component = world.CreateSerializableObject(this, SerializableObjectStaticClass<T>.StaticClassID) as T;
		RegisterComponent(component);
		return component;
	}

	public T GetComponent<T>() where T : ActorComponent {
		for (int i = 0; i < components.Count; ++i) {
			if (components[i] is T) {
				return components[i] as T;
			}
		}
		return null;
	}

	public T[] GetAllComponents<T>() where T : ActorComponent {
		List<T> list = new List<T>();
		for (int i = 0; i < components.Count; ++i) {
			if (components[i] is T) {
				list.Add(components[i] as T);
			}
		}
		return list.ToArray();
	}

	public void RegisterComponent(ActorComponent component) {
		if (!components.Contains(component)) {
			components.Add(component);
		}
	}

	public int numComponents {
		get {
			return components.Count;
		}
	}

	public ActorComponent GetComponent(int index) {
		return components[index];
	}

	public override void SerializeSubobjects(SerializableObjectSubobjectSerializer serializer) {
		base.SerializeSubobjects(serializer);

		for (int i = 0; i < components.Count; ++i) {
			var component = components[i];
			serializer.SerializeSubobject(component);
		}
	}
	
	protected virtual void OnRep_ownerConnectionID() {
		var clWorld = world as Client.ClientWorld;
		if (clWorld.serverConnection.id == _ownerConnectionID) {
			_ownerConnection = clWorld.serverChannel;
		}
	}

	public void SetReplicateRate(float replicateRate) {
		_replicateRate = replicateRate;
	}
}

public abstract class UnifiedActor<T> : Actor {

	public override Type serverType {
		get {
			return typeof(T);
		}
	}

	public override Type clientType {
		get {
			return typeof(T);
		}
	}

}

public abstract class ActorComponent : ActorRPCObject {

	Actor _owner;
	ObjectRPCTable _rpcTable;
	bool disposed;

	public ActorComponent() {
		if (!activating) {
			_rpcTable = ObjectTypeRPCTable.instance.GetRPCTable(this);
		}
	}

	~ActorComponent() {
		Assert.IsFalse(disposed);
		Dispose(false);
	}

	public override void PreConstruct(object outer) {
		base.PreConstruct(outer);
		_owner = (Actor)outer;
	}

	public override void PostNetConstruct() {
		base.PostNetConstruct();
		_owner.RegisterComponent(this);
	}

	public virtual void BeginTravel() {}

	public virtual void FinishTravel() {}

	protected virtual void Dispose(bool disposing) {}

	public virtual void Tick() {}

	internal void Dispose() {
		if (!disposed) {
			disposed = true;
			Dispose(true);
		}
	}

	public Actor owner {
		get {
			return _owner;
		}
	}

	public sealed override bool netTornOff {
		get {
			return _owner.netTornOff;
		}
	}

	public sealed override ObjectRPCTable rpcTable {
		get {
			return _rpcTable;
		}
	}

	public sealed override World world {
		get {
			return _owner.world;
		}
	}

	public sealed override ActorReplicationChannel ownerConnection {
		get {
			return _owner.ownerConnection;
		}
	}
}

public abstract class UnifiedActorComponent<T> : ActorComponent {
	public override Type clientType {
		get {
			return typeof(T);
		}
	}

	public override Type serverType {
		get {
			return typeof(T);
		}
	}
}

public abstract class ActorRPCObject : SerializableObject {

	public abstract ObjectRPCTable rpcTable {
		get;
	}

	public abstract World world {
		get;
	}

	public abstract ActorReplicationChannel ownerConnection {
		get;
	}

	public abstract bool netTornOff {
		get;
	}

	protected ActorRPC BindRPC(Action action) {
		return new ActorRPC(this, action);
	}

	protected ActorRPC<T> BindRPC<T>(Action<T> action) {
		return new ActorRPC<T>(this, action);
	}

	protected ActorRPC<T0, T1> BindRPC<T0, T1>(Action<T0, T1> action) {
		return new ActorRPC<T0, T1>(this, action);
	}

	protected ActorRPC<T0, T1, T2> BindRPC<T0, T1, T2>(Action<T0, T1, T2> action) {
		return new ActorRPC<T0, T1, T2>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3> BindRPC<T0, T1, T2, T3>(Action<T0, T1, T2, T3> action) {
		return new ActorRPC<T0, T1, T2, T3>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4> BindRPC<T0, T1, T2, T3, T4>(Action<T0, T1, T2, T3, T4> action) {
		return new ActorRPC<T0, T1, T2, T3, T4>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4, T5> BindRPC<T0, T1, T2, T3, T4, T5>(Action<T0, T1, T2, T3, T4, T5> action) {
		return new ActorRPC<T0, T1, T2, T3, T4, T5>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4, T5, T6> BindRPC<T0, T1, T2, T3, T4, T5, T6>(Action<T0, T1, T2, T3, T4, T5, T6> action) {
		return new ActorRPC<T0, T1, T2, T3, T4, T5, T6>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7> BindRPC<T0, T1, T2, T3, T4, T5, T6, T7>(Action<T0, T1, T2, T3, T4, T5, T6, T7> action) {
		return new ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8> BindRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8>(Action<T0, T1, T2, T3, T4, T5, T6, T7, T8> action) {
		return new ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8>(this, action);
	}

	protected ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> BindRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Action<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> action) {
		return new ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(this, action);
	}

	public void InternalInvokeRPC(ref int rpcID, MethodInfo method, params object[] args) {
		if (rpcID == -1) {
			rpcID = rpcTable.GetMethodID(method);
			if (rpcID == -1) {
				throw new InvalidReplicatedObjectClassException("Method not marked for RPC " + method.DeclaringType.FullName + "." + method.Name);
			}
		}

		var rpc = rpcTable.GetObjectRPCByID(rpcID);
		var replicates = this.replicates && !netTornOff;

		switch (rpc.rpcInfo.Domain) {
			case ERPCDomain.Owner:
				if (world is Client.ClientWorld) {
					throw new ActorReplicationException("Illegal Owner RPC call initiated on client");
				} else if (replicates && (ownerConnection != null)) {
					ownerConnection.ReplicateRPC(this, rpcID, rpc, args);
				}
			break;
			case ERPCDomain.Server:
				var clientWorld = world as Client.ClientWorld;
				if (clientWorld != null) {
					if (replicates) {
						var channel = clientWorld.serverChannel;
						channel.ReplicateRPC(this, rpcID, rpc, args);
					}
				} else {
					method.Invoke(this, args);
				}
			break;
			case ERPCDomain.Multicast:
				{
					var svWorld = world as Server.ServerWorld;
					if (svWorld != null) {
						if (replicates) {
							for (int i = 0; i < svWorld.clientConnections.Count; ++i) {
								svWorld.clientConnections[i].ReplicateRPC(this, rpcID, rpc, args);
							}
						}
					} else {
						throw new ActorReplicationException("Illegal Multicast RPC call initiated on client");
					}
				}
			break;
			case ERPCDomain.MulticastExcludeOwner:
				if (world is Server.ServerWorld) {
					if (replicates) {
						var svWorld = world as Server.ServerWorld;
						for (int i = 0; i < svWorld.clientConnections.Count; ++i) {
							var channel = svWorld.clientConnections[i];
							if (channel != ownerConnection) {
								channel.ReplicateRPC(this, rpcID, rpc, args);
							}
						}
					}
				} else {
					throw new ActorReplicationException("Illegal MulticastExcludeOwner RPC call initiated on client");
				}
			break;
		}
	}
}

public abstract class ActorRPCBase {

	protected int rpcID = -1;
	protected ActorRPCObject rpcObject;
	protected MethodInfo minfo;

	public ActorRPCBase(ActorRPCObject rpcObject, MethodInfo minfo) {
		this.rpcObject = rpcObject;
		this.minfo = minfo;
	}
}

public sealed class ActorRPC : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action action) : base(rpcObject, action.Method) {
	}

	public void Invoke() {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo);
	}
}

public sealed class ActorRPC<T> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T a0) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0);
	}
}

public sealed class ActorRPC<T0, T1> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1);
	}
}

public sealed class ActorRPC<T0, T1, T2> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2);
	}
}

public sealed class ActorRPC<T0, T1, T2, T3> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3);
	}
}

public delegate void Action<T0, T1, T2, T3, T4>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4);

public sealed class ActorRPC<T0, T1, T2, T3, T4> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4);
	}
}

public delegate void Action<T0, T1, T2, T3, T4, T5>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5);

public sealed class ActorRPC<T0, T1, T2, T3, T4, T5> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4, T5> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4, a5);
	}
}

public delegate void Action<T0, T1, T2, T3, T4, T5, T6>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6);

public sealed class ActorRPC<T0, T1, T2, T3, T4, T5, T6> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4, T5, T6> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4, a5, a6);
	}
}

public delegate void Action<T0, T1, T2, T3, T4, T5, T6, T7>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7);

public sealed class ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4, T5, T6, T7> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4, a5, a6, a7);
	}
}

public delegate void Action<T0, T1, T2, T3, T4, T5, T6, T7, T8>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8);

public sealed class ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4, T5, T6, T7, T8> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4, a5, a6, a7, a8);
	}
}

public delegate void Action<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8, T9 a9);

public sealed class ActorRPC<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> : ActorRPCBase {

	public ActorRPC(ActorRPCObject rpcObject, Action<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9> action) : base(rpcObject, action.Method) {
	}

	public void Invoke(T0 a0, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6, T7 a7, T8 a8, T9 a9) {
		rpcObject.InternalInvokeRPC(ref rpcID, minfo, a0, a1, a2, a3, a4, a5, a6, a7, a8, a9);
	}
}