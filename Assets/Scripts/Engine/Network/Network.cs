// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine.Assertions;
using System.Reflection;

public class NetConnection {

	NetDriverConnection inner;
	World _world;
	int _id;

	public NetConnection(World world, NetDriverConnection connection) {
		inner = connection;
		_world = world;
	}

	public int id {
		get {
			return _id;
		}
	}

	public string address {
		get {
			return inner.address;
		}
	}

	public bool isValid {
		get {
			return inner.isValid;
		}
	}

	public World world {
		get {
			return _world;
		}
	}

	public NetDriverConnection driverConnection {
		get {
			return inner;
		}
	}

	public void SetID(int id) {
		_id = id;
	}

	public void SendReliable(NetMsg message) {
		using (var archive = message.Serialize(this, world.GetNetMessageArchive())) {
			archive.Flush();

			int numBytes;
			var bytes = world.GetNetMessageBytes(out numBytes);
			if (numBytes > 0) {
				inner.SendReliable(bytes, numBytes);
			}
		}
	}
}

public interface NetMsg {
	void Init();
	Archive Serialize(NetConnection connection, Archive archive);
	
	int msgTypeID {
		get;
	}
	bool isServerMsg {
		get;
	}
	bool isClientMsg {
		get;
	}
	NetMsg shareInstance {
		get;
	}
}

public abstract class NetMsgBase<T> : NetMsg {
	static readonly int _typeID = typeof(T).AssemblyQualifiedName.GetHashCode();

	public abstract void Serialize(Archive archive);

	public Archive Serialize(NetConnection connection, Archive archive) {
		if (archive.isSaving) {
			archive.Write(msgTypeID);
		}
		Serialize(archive);
		return archive;
	}

	public abstract void Init();

	public int msgTypeID {
		get {
			return _typeID;
		}
	}

	public abstract bool isServerMsg {
		get;
	}

	public abstract bool isClientMsg {
		get;
	}

	public abstract NetMsg shareInstance {
		get;
	}
}

public interface NetMsgPayload {
	void Serialize(Archive archive);
}

public abstract class UnifiedNetMsg<T> : NetMsgBase<T>  where T : UnifiedNetMsg<T> {

	static T _instance;

	public static T instance {
		get {
			return _instance;
		}
	}

	public sealed override void Init() {
		Assert.IsTrue(_instance == null);
		_instance = this as T;
	}

	public override bool isServerMsg {
		get {
			return true;
		}
	}

	public override bool isClientMsg {
		get {
			return true;
		}
	}

	public override NetMsg shareInstance {
		get {
			return instance;
		}
	}

	public T payload {
		get {
			return instance;
		}
	}
}

public abstract class NetMsgFactory {
	IntHashtable<NetMsg> netMsgs = new IntHashtable<NetMsg>();

	public NetMsgFactory(Assembly[] assemblies, bool forServer) {
		var types = ReflectionHelpers.GetTypesThatImplementInterfaces(assemblies, new[] { typeof(NetMsg) });
		foreach (var t in types) {
			var ctor = t.GetConstructor(System.Type.EmptyTypes);
			if (ctor != null) {
				var netMsg = ctor.Invoke(null) as NetMsg;
				if ((netMsg.isServerMsg && forServer) || (netMsg.isClientMsg && !forServer)) {
					if (netMsg.shareInstance != null) {
						netMsg = netMsg.shareInstance;
					} else {
						netMsg.Init();
					}
					if (netMsgs.Contains(netMsg.msgTypeID)) {
						throw new System.Exception(t.FullName + " collides with an existing type!");
					} else {
						netMsgs.Add(netMsg.msgTypeID, netMsg);
					}
				} else if (netMsg.shareInstance == null) {
					netMsg.Init(); // clients need to init server net messages to send.
				}
			} else {
				throw new TargetInvocationException(t.FullName + " does not have a constructor!", null);
			}
		}
	}

	public NetMsg GetNetMsg(int typeHash) {
		return netMsgs[typeHash] as NetMsg;
	}

	public IntHashtable<NetMsg> netMsgHashtable {
		get {
			return netMsgs;
		}
	}
}

public class ClientNetMsgFactory : NetMsgFactory {
	public ClientNetMsgFactory(Assembly[] assemblies) : base(assemblies, false) {
	}
}

public class ServerNetMsgFactory : NetMsgFactory {
	public ServerNetMsgFactory(Assembly[] assemblies) : base(assemblies, true) {
	}
}