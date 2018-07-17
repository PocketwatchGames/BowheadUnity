// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

public interface IGameInstance {
	bool isDedicatedServer { get; }
	bool isServer { get; }
	bool isClient { get; }
	Server.ServerWorld serverWorld { get; }
	bool fixedUpdateDidRun { get; }
}

public abstract partial class World : INetDriverCallbacks, IDisposable {
	public const int MAX_RELIABLE_MESSAGE_SIZE = 32*1024;
	public const int MAX_UNRELIABLE_MESSAGE_SIZE = 4*1024;
	
	public delegate Transform GetObjectPoolRootDelegate();

	public interface IAsyncSceneLoad {
		bool isDone { get; }
		float progress { get; }
	}

	bool _disposed;
	INetDriver _netDriver;
	IntHashtable<MethodInfo> _netMsgDispatchHash = new IntHashtable<MethodInfo>();
	object[] _dispatchArgs = new object[2];
	NetMsgFactory _netMsgFactory;
	NetMsgArchive _netArchive;
	SerializableObjectFactory _objectFactory;
	byte[] _netArchiveBytes;
	int _nextNetID;
	double _time;
	double _unscaledTime;
	float _deltaTime;
	float _unscaledDeltaTime;
	bool _bIsLoading;
	bool _sceneLoaded;
	bool _didLevelStart;
	string _travelLevel;
	string _currentLevel;
	Transform _sceneGroup;
	IAsyncSceneLoad _asyncLoad;
	CoTaskQueue _loadingQueue = new CoTaskQueue(20);
	CoTaskQueue _taskQueue = new CoTaskQueue(2);
	List<Actor> _actors = new List<Actor>();
	List<Actor> _travelActors;
	IntHashtableList<Actor> _replicatedActors = new IntHashtableList<Actor>();
	IntHashtableList<SerializableObject> _replicatedObjects = new IntHashtableList<SerializableObject>();
	IntHashtable<SerializedObjectFields> _serializedFields = new IntHashtable<SerializedObjectFields>();
	RPCObjectReferenceCollector _rpcReferenceCollector;
	GetObjectPoolRootDelegate _getStaticPoolRoot;
	GetObjectPoolRootDelegate _getTransientPoolRoot;
	GameObject _defaultActorPrefab;
	Streaming _worldStreaming;
	bool _sharedWorldStreaming;

	public World(
		IGameInstance gameInstance,
		Streaming sharedStreaming,
		World_ChunkComponent chunkComponent,
		Transform sceneGroup,
		GameObject defaultActorPrefab,
		GetObjectPoolRootDelegate getStaticPoolRoot,
		GetObjectPoolRootDelegate getTransientPoolRoot,
		SerializableObjectFactory objectFactory, 
		INetDriver netDriver, 
		NetMsgFactory netMsgFactory
	) {
		this.gameInstance = gameInstance;
		_sceneGroup = sceneGroup;
		_netDriver = netDriver;
		_rpcReferenceCollector = new RPCObjectReferenceCollector(this);
		_objectFactory = objectFactory;
		_netMsgFactory = netMsgFactory;
		_getStaticPoolRoot = getStaticPoolRoot;
		_getTransientPoolRoot = getTransientPoolRoot;
		_defaultActorPrefab = defaultActorPrefab;

		CacheNetworkDispatchMethods();

		_netArchiveBytes = new byte[MAX_RELIABLE_MESSAGE_SIZE];
		_netArchive = new NetMsgArchive(_netArchiveBytes);
		_nextNetID = 0;

		_worldStreaming = sharedStreaming ?? new Streaming(chunkComponent, CreateGenVoxelsJob, MMapChunkData, WriteChunkData);
		_sharedWorldStreaming = sharedStreaming != null;
	}

	public IGameInstance gameInstance {
		get;
		private set;
	}

	public Transform staticObjectPoolRoot {
		get {
			return _getStaticPoolRoot();
		}
	}

	public Transform transientObjectPoolRoot {
		get {
			return _getTransientPoolRoot();
		}
	}

	public GameObject defaultActorPrefab {
		get {
			return _defaultActorPrefab;
		}
	}

	public Streaming worldStreaming {
		get {
			return _worldStreaming;
		}
	}

	public Transform sceneGroup {
		get {
			return _sceneGroup;
		}
	}

	protected CoTaskQueue taskQueue {
		get {
			return _taskQueue;
		}
	}
	
	public CoTask EnqueueLoadingTask(System.Func<bool> action) {
		return EnqueueLoadingTask<CoTask>(action);
	}

	public T EnqueueLoadingTask<T>(System.Func<bool> action) where T : CoTask, new() {
		return _loadingQueue.AddTask<T>(action);
    }

	public CoTask EnqueueCoTask(System.Func<bool> action) {
		return EnqueueCoTask<CoTask>(action);
	}

	public T EnqueueCoTask<T>(System.Func<bool> action) where T : CoTask, new() {
		return _taskQueue.AddTask<T>(action);
	}

	public bool disposed {
		get {
			return _disposed;
		}
	}

	public void Dispose() {
		if (!_disposed) {
			_disposed = true;
			Dispose(true);
		}
	}

	protected virtual void Dispose(bool disposing) {
		_netArchive.Dispose();
		_netArchive = null;
		_taskQueue.Clear();
		_loadingQueue.Clear();

		for (int i = 0; i < _actors.Count; ++i) {
			var actor = _actors[i];
			actor.Dispose();
			actor.internal_ClearReplicators();
		}

		_actors.Clear();
		_replicatedActors.Clear();
		_replicatedObjects.Clear();
		_travelActors = null;
		spawnTags = null;

		if (!_sharedWorldStreaming) {
			_worldStreaming.Dispose();
			_worldStreaming = null;
		}
	}

	protected void UpdateTime(float dt, float unscaledDt) {
		_deltaTime = dt;
		_unscaledDeltaTime = unscaledDt;
		if (!isTraveling) {
			_time += dt;
			_unscaledTime += unscaledDt;
		}
	}

	protected virtual void TickWorldStreaming() {
		if (!_sharedWorldStreaming) {
			_worldStreaming.Tick();
		}
	}

	protected virtual void TickActors(MonoBehaviour loadingContext) {
		Perf.Begin("World.TickActors");

		_loadingQueue.Run(loadingContext);
		_taskQueue.Run(loadingContext);

		for (int i = 0; i < _actors.Count;) {
			var actor = _actors[i];

			if (!actor.pendingKill) {
				actor.Tick();
				actor.TickComponents();
				actor.TickLifetime();
			}

			if (actor.pendingKill) {
				DestroyActor(actor, false);
				_actors.RemoveAt(i);
			} else {
				++i;
			}
		}

		Perf.End();
	}

	protected virtual void FixedUpdateActors(float dt) {
		Perf.Begin("World.FixedUpdate");

		for (int i = 0; i < _actors.Count; ++i) {
			var actor = _actors[i];

			if (!actor.pendingKill) {
				actor.FixedUpdate(dt);
			}
		}

		Perf.End();
	}

	protected virtual void LateTickActors() {
		Perf.Begin("World.LateTickActors");
		for (int i = 0; i < _actors.Count;) {
			var actor = _actors[i];

			if (!actor.pendingKill) {
				actor.LateTick();
			}

			if (actor.pendingKill) {
				DestroyActor(actor, false);
				_actors.RemoveAt(i);
			} else {
				++i;
			}
		}
		Perf.End();
	}

	protected virtual void DestroyActor(Actor actor, bool isTraveling) {
		if (actor.netID != 0) {
			_replicatedActors.Remove(actor.netIDHashCode);
			_replicatedObjects.Remove(actor.netIDHashCode);

			for (int i = 0; i < actor.numComponents; ++i) {
				var component = actor.GetComponent(i);
				if (component.netID != 0) {
					_replicatedObjects.Remove(component.netIDHashCode);
				}
			}
		}
		actor.Dispose();
	}

	protected void DestroyAndRemoveActor(Actor actor) {
		DestroyActor(actor, false);
		_actors.Remove(actor);
	}

	protected bool Connect(string address, int port) {
		return _netDriver.Connect(address, port, this);
	}

	protected bool Listen(int port, int maxConnections) {
		return _netDriver.Listen(port, maxConnections, this);
	}

	protected virtual void BeginTravel(string travelLevel, HashSetList<int> travelActorNetIDs) {

		_travelLevel = travelLevel;
		_bIsLoading = true;
		_travelActors = new List<Actor>();
		_time = 0f;
		_unscaledTime = 0f;
		_deltaTime = 0f;
		_unscaledDeltaTime = 0f;
		_nextNetID = 0;
		_loadingQueue.Clear();
		_taskQueue.Clear();
		_sceneLoaded = false;
		_didLevelStart = false;
		spawnTags = null;

		for (int i = 0; i < _actors.Count;) {
			var actor = _actors[i];
			if ((travelActorNetIDs != null) && ActorWillTravel(actor, travelActorNetIDs)) {
				actor.SetSpawnTagID(0);
				actor.BeginTravel();
				_travelActors.Add(actor);
				++i;
			} else {
				DestroyActor(actor, true);
				_actors.RemoveAt(i);
			}
		}

		if (!_sharedWorldStreaming) {
			_worldStreaming.BeginTravel();
		}

	}

	public void SetAsyncLoadOperation(IAsyncSceneLoad asyncLoad) {
		_asyncLoad = asyncLoad;
	}

	protected virtual void FinishTravel() {
		// level was loaded, finish actors that traveled.
		if (!_sharedWorldStreaming) {
			_worldStreaming.FinishTravel();
		}

		for (int i = 0; i < _travelActors.Count; ++i) {
			var actor = _travelActors[i];
			actor.FinishTravel();
		}

		_travelActors = null;
		_currentLevel = travelLevel;
	}

	public virtual void NotifySceneLoaded() {
		_asyncLoad = null;
		_currentLevel = _travelLevel;
		_sceneLoaded = true;
		GatherSpawnTags();
		if (spawnTags != null) {
			SpawnActorTags();
		}
	}

	protected abstract Unity.Jobs.JobHandle CreateGenVoxelsJob(WorldChunkPos_t pos, PinnedChunkData_t chunk);
	protected abstract Streaming.IMMappedChunkData MMapChunkData(Streaming.IChunk chunk);
	protected abstract void WriteChunkData(Streaming.IChunkIO chunk);
	protected abstract void SpawnActorTags();

	protected void TickTravel(MonoBehaviour loadingContext) {
		_loadingQueue.Run(loadingContext);
		_bIsLoading = !_sceneLoaded || (_asyncLoad != null) || _loadingQueue.running;

		for (int i = 0; i < _actors.Count;) {
			var actor = _actors[i];

			if (actor.pendingKill) {
				DestroyActor(actor, _asyncLoad != null);
				_actors.RemoveAt(i);
			} else {
				++i;
			}
		}
	}

	protected virtual void OnLevelStart() {
		if (!_didLevelStart) {
			_didLevelStart = true;
			for (int i = 0; i < _actors.Count; ++i) {
				_actors[i].OnLevelStart();
			}
		}
	}

	void GatherSpawnTags() {
		spawnTags = new Dictionary<int, ActorSpawnTag>();
		ActorSpawnTag[] tags = GameObject.FindObjectsOfType<ActorSpawnTag>();

		bool canDisable = gameInstance.isDedicatedServer || (this is Client.ClientWorld);

		foreach (var tag in tags) {
			if (!tag.clone || !tag.isInstance) {
				spawnTags.Add(tag.spawnID, tag);
				if (canDisable && tag.clone) {
					tag.gameObject.SetActive(false);
				}
			}
		}

		if (spawnTags.Count < 1) {
			spawnTags = null;
		}
	}

	protected Dictionary<int, ActorSpawnTag> spawnTags {
		get;
		set;
	}

	public ActorSpawnTag GetActorSpawnTag(Actor actor) {
		ActorSpawnTag tag;

		if ((actor.spawnTagID != 0) && (spawnTags != null) && spawnTags.TryGetValue(actor.spawnTagID, out tag)) {
			return tag;
		}

		return null;
	}

	bool ActorWillTravel(Actor actor, HashSetList<int> travelActorNetIds) {
		return !actor.pendingKill && (((actor.netID == 0) || !actor.replicates) && actor.nonReplicatedActorShouldTravel) || ((actor.netID != 0) && travelActorNetIds.Contains(actor.netID));
    }

	public string travelLevel {
		get {
			return _travelLevel;
		}
	}

	public string currentLevel {
		get {
			return _currentLevel;
		}
	}
	
	public virtual bool isTraveling {
		get {
			return _bIsLoading;
		}
	}

	protected bool isLoading {
		get {
			return _bIsLoading;
		}
	}

	public float travelProgress {
		get {
			float progress = 0.5f;
			if ((_asyncLoad != null) && !_asyncLoad.isDone) {
				progress = _asyncLoad.progress / 2f;
			}
			if (_loadingQueue.running) {
				progress += _loadingQueue.progress / 2f;
			}
			return progress;
		}
	}

	protected INetDriver netDriver {
		get {
			return _netDriver;
		}
	}

	void RawNetRecv(ActorReplicationChannel channel, int size) {
#if PROFILING
		try {
			Perf.Begin("World.RawNetRecv");
#endif
			_netArchive.BeginRead(size);
			int typeID = _netArchive.ReadInt();
			NetMsg msg = _netMsgFactory.GetNetMsg(typeID);
			if (msg != null) {
				msg.Serialize(channel.connection, _netArchive);
				if (_netArchive.hasUnreadBytes) {
					throw new System.IO.IOException(msg.GetType().FullName + " did not consume its entire payload.");
				}
				DynamicDispatchNetMsg(msg, channel);
			} else {
				throw new System.IO.IOException("Unrecognized net message type " + typeID);
			}
#if PROFILING
		} finally {
			Perf.End();
		}
#endif
	}

	public Archive GetNetMessageArchive() {
		_netArchive.BeginWrite();
		return _netArchive;
	}

	public byte[] GetNetMessageBytes(out int numBytes) {
		numBytes = _netArchive.archiveSize;
		return _netArchiveBytes;
	}

	protected byte[] netMessageBytes {
		get {
			return _netArchiveBytes;
		}
	}

	public T Spawn<T>(object outer, SpawnParameters spawnArgs) where T : Actor, new() {
		int classID = SerializableObject.StaticClassID<T>();
		return (T)InternalSpawn(null, typeof(T), classID, outer, spawnArgs);
	}

	public Actor Spawn(Type type, object outer, SpawnParameters spawnArgs) {
		if (!typeof(Actor).IsAssignableFrom(type)) {
			throw new Exception("Can only spawn actors!");
		}
		int classID = SerializableObject.StaticClassIDSlow(type);
		return InternalSpawn(null, type, classID, outer, spawnArgs);
	}

	public Actor Spawn(ActorSpawnTag tag, object outer, SpawnParameters spawnArgs) {
		if (tag.type == null) {
			throw new Exception("ActorSpawnTag cannot load type: " + tag.typeName);
		} else {
			int classID = SerializableObject.StaticClassIDSlow(tag.type);
			return InternalSpawn(tag, tag.type, classID, outer, spawnArgs);
		}
	}

	int GetNextNetID() {
		for (int i = 0; i < ushort.MaxValue; ++i) {
			int id = (_nextNetID + i) & ushort.MaxValue;
			if ((id != 0) && !_replicatedActors.Contains(id.GetHashCode())) {
				_nextNetID = (id + 1) & ushort.MaxValue;
				return id;
			}
		}

		throw new System.Exception("Out of replicated actor ids!");
	}

	Actor InternalSpawn(ActorSpawnTag tag, Type type, int classID, object outer, SpawnParameters spawnArgs) {
		var actor = _objectFactory.NewObject(classID) as Actor;
		if ((actor == null) || !type.IsAssignableFrom(actor.GetType())) {
			throw new ObjectStaticClassMismatchException(type.FullName + " has a bad static class id!");
		}
		if (tag != null) {
			actor.SetReplicates(tag.replicates);
			actor.SetSpawnTagID((ushort)tag.spawnID);
		}
		actor.SetWorld(this);
		if (actor.replicates && (this is Server.ServerWorld)) {
			actor.SetNetID(GetNextNetID());
			actor.SetRemoteRole(ERemoteRole.Authority);
			_replicatedActors.Add(actor.netIDHashCode, actor);
			_replicatedObjects.Add(actor.netIDHashCode, actor);
		}

		_actors.Add(actor);

		if (spawnArgs.preConstruct != null) {
			spawnArgs.preConstruct(actor);
		}

		actor.PreConstruct(outer);
		actor.Construct();
		actor.PostConstruct();

		if (_didLevelStart) {
			actor.OnLevelStart();
		}

		return actor;
	}

	public SerializableObject InternalCreateReplicatedObject(int classID, int netID) {
		var obj = _objectFactory.NewObject(classID);
		if (obj == null) {
			throw new ObjectStaticClassMismatchException("There is no actor with static class " + classID + "!");
		}
		obj.SetNetID(netID);
		_replicatedObjects.Add(obj.netIDHashCode, obj);

		var actor = obj as Actor;
		if (actor != null) {
			actor.SetReplicates(true);
			actor.SetWorld(this);
			actor.SetRemoteRole(ERemoteRole.SimulatedProxy);
			_replicatedActors.Add(actor.netIDHashCode, actor);
			_actors.Add(actor);
		}
		
		return obj;
	}

	public SerializableObject CreateSerializableObject(object outer, int classID) {
		var obj = _objectFactory.NewObject(classID);
		if (obj == null) {
			throw new ObjectStaticClassMismatchException("There is no actor with static class " + classID + "!");
		}
		if (obj.replicates && (this is Server.ServerWorld)) {
			obj.SetNetID(GetNextNetID());
			_replicatedObjects.Add(obj.netIDHashCode, obj);
		}
		obj.PreConstruct(outer);
		obj.Construct();
		return obj;
	}

	public SerializedObjectFields InternalGetReplicatedFields(SerializableObject obj) {
		SerializedObjectFields fields;
		if (_serializedFields.TryGetValue(obj.classID, out fields)) {
			return fields;
		}

		fields = new SerializedObjectFields(obj.GetType(), ReplicatedObjectFieldSerializerFactory.instance, true);
		return fields;
	}

	internal void NetTornOff(Actor actor) {
		_replicatedActors.Remove(actor.netIDHashCode);
	}

	public IEnumerable<Actor> actors {
		get {
			return _actors;
		}
	}

	public int numActors {
		get {
			return _actors.Count;
		}
	}

	public int numReplicatedActors {
		get {
			return _replicatedActors.Values.Count;
		}
	}

	public float deltaTime {
		get {
			return _deltaTime;
		}
	}

	public float unscaledDeltaTime {
		get {
			return _unscaledDeltaTime;
		}
	}

	public double time {
		get {
			return _time;
		}
	}

	public double unscaledTime {
		get {
			return _unscaledTime;
		}
	}

	public Actor GetActor(int index) {
		return _actors[index];
	}

	public Actor GetReplicatedActor(int index) {
		return _replicatedActors.Values[index];
	}

	public SerializableObject GetObjectByNetID(int netID) {
		return GetObjectByNetIDHashCode(netID.GetHashCode());
	}

	public SerializableObject GetObjectByNetIDHashCode(int netIDHashCode) {
		SerializableObject obj;
		_replicatedObjects.TryGetValue(netIDHashCode, out obj);
		return obj;
	}

	protected byte[] netArchiveBytes {
		get {
			return _netArchiveBytes;
		}
	}

	void DynamicDispatchNetMsg(NetMsg msg, ActorReplicationChannel channel) {
		MethodInfo m;

		if (!_netMsgDispatchHash.TryGetValue(msg.msgTypeID, out m)) {
			throw new TargetInvocationException(GetType().FullName + " is not supposed to be received!", null);
		}

		_dispatchArgs[0] = msg;
		_dispatchArgs[1] = channel;

		m.Invoke(this, _dispatchArgs);

		_dispatchArgs[0] = null;
		_dispatchArgs[1] = null;
	}

	void CacheNetworkDispatchMethods() {
		foreach (var obj in _netMsgFactory.netMsgHashtable.Values) {
			var msg = obj as NetMsg;
			MethodInfo m = null;
			if (!_netMsgDispatchHash.Contains(msg.msgTypeID)) {
				m = FindDispatchMethod(msg.GetType());
				if (m == null) {
#if UNITY_EDITOR
					UnityEditor.EditorApplication.isPaused = true;
#endif
					throw new TargetInvocationException(GetType().FullName + " is missing void OnNetMsg(" + msg.GetType().FullName + " msg, ActorReplicationChannel channel).", null);
				} else {
					_netMsgDispatchHash.Add(msg.msgTypeID, m);
				}
			} else {
				throw new TargetInvocationException(GetType().FullName + " hash collision on void OnNetMsg(" + msg.GetType().FullName + " msg, ActorReplicationChannel channel).", null);
			}
		}
	}

	MethodInfo FindDispatchMethod(Type netMsgType) {
		for (Type t = netMsgType; t != typeof(object); t = t.BaseType) {
			Type[] args = new Type[] { t, typeof(ActorReplicationChannel) };

			for (Type selfType = GetType(); selfType != typeof(object); selfType = selfType.BaseType) {
				var m = selfType.GetMethod("OnNetMsg", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.ExactBinding, null, args, null);
				if (m != null) {
					return m;
				}
			}
		}

		return null;
	}
			
	protected abstract void OnDisconnect(ActorReplicationChannel channel, EDisconnectReason reason, string msg);
	public abstract void OnConnect(NetDriverConnection connection);
	public abstract void OnDisconnect(NetDriverConnection channel);
	public abstract void OnReliableSendWouldBlock(NetDriverConnection connection);

	public void OnMessageReceived(NetDriverConnection connection, byte[] data, int size) {
#if !UNITY_EDITOR
		try {
#endif
			RawNetRecv(connection.outer, size);
#if !UNITY_EDITOR
		} catch (Exception e) {
			OnRawNetRecvError(connection, e);
		}
#endif
	}

	public void OnInvalidMessageReceived(NetDriverConnection connection) {
		Debug.LogError("Received bad packet from (" + connection.address + "), disconnecting.");
		connection.Dispose();
	}

	public virtual void OnRawNetRecvError(NetDriverConnection connection, Exception e) {
		Debug.LogException(e);
		OnInvalidMessageReceived(connection);
	}

	public IEnumerable<T> GetActorIterator<T>() where T : class {
		return new ActorEnumerable<T>(this);
	}

	class RPCObjectReferenceCollector : ISerializableObjectReferenceCollector {
		World world;

		public RPCObjectReferenceCollector(World world) {
			this.world = world;
		}

		public SerializableObject AddReference(ISerializableObjectFieldSerializer serializer, int id, int fieldIndex) {
			return world.GetObjectByNetID(id);
		}

	}

	class NetMsgArchive : StreamArchive {
		int internalSize;

		public NetMsgArchive(byte[] bytes) : base(new System.IO.MemoryStream(bytes, 0, bytes.Length, true), true, false) {
		}

		public void BeginRead(int size) {
			stream.Position = 0;
			internalSize = size;
			OpenRead();
		}

		public void BeginWrite() {
			stream.Position = 0;
			OpenWrite();
		}

		protected override int InternalSkipBytes(int num) {
			return base.InternalSkipBytes(CheckedReadSize(num));
		}

		protected override int InternalReadByte() {
			if (CheckedReadSize(1) == 1) {
				return base.InternalReadByte();
			}
			return -1;
		}

		protected override void Dispose(bool disposing) {}

		public int archiveSize {
			get {
				return (int)stream.Position;
			}
		}

		public bool hasUnreadBytes {
			get {
				return (stream.Position != internalSize);
			}
		}

		int CheckedReadSize(int size) {
			if (stream.Position + size > internalSize) {
				return internalSize - (int)stream.Position;
			}
			return size;
		}
	}

	struct ActorEnumerable<T> : IEnumerable<T> where T : class {

		World world;

		public ActorEnumerable(World world) {
			this.world = world;
		}

		public IEnumerator<T> GetEnumerator() {
			return new ActorEnumerator<T>(world);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return new ActorEnumerator<T>(world);
		}
	}

	struct ActorEnumerator<T> : IEnumerator<T> where T : class {

		int ofs;
		T current;
		World world;

		public ActorEnumerator(World world) {
			this.world = world;
			ofs = 0;
			current = null;
		}

		public void Dispose() {	}

		public bool MoveNext() {
			while (ofs < world.numActors) {
				var actor = world.GetActor(ofs++);
				if (!actor.pendingKill) {
					current = actor as T;
					if (current != null) {
						return true;
					}
				}
			}
			return false;
		}

		public void Reset() {
			ofs = 0;
		}

		public T Current {
			get {
				return current;
			}
		}

		object System.Collections.IEnumerator.Current {
			get {
				return current;
			}
		}
	}
}
