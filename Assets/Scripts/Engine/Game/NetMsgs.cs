// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

public enum EDisconnectReason {
	User,
	Error,
	WrongVersion,
	Kicked,
	Banned,
	TimedOut,
	DisconnectedByLoginServer,
	AFK
}

namespace NetMsgs {
	public sealed class Disconnect : UnifiedNetMsg<Disconnect> {
		byte _reason;
		string _message;

		public static Disconnect New(EDisconnectReason reason, string message) {
			instance._reason = (byte)reason;
			instance._message = message;
			return instance;
		}

		public EDisconnectReason reason {
			get {
				return (EDisconnectReason)_reason;
			}
		}

		public string message {
			get {
				return _message;
			}
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _reason);
			archive.Serialize(ref _message);
		}
	}

	public sealed class Welcome : UnifiedNetMsg<Welcome> {
		string _serverName;
		string _message;
		int _connectionID;

		public static Welcome New(string serverName, string message, int _connectionID) {
			instance._serverName = serverName;
			instance._message = message;
			instance._connectionID = _connectionID;
			return instance;
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _serverName);
			archive.Serialize(ref _message);
			archive.Serialize(ref _connectionID);
		}

		public string serverName {
			get {
				return _serverName;
			}
		}

		public string message {
			get {
				return _message;
			}
		}

		public int connectionID {
			get {
				return _connectionID;
			}
		}

		// Server never receives this message
		public override bool isServerMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ClientConnect : UnifiedNetMsg<ClientConnect> {

		ulong _uuid;
		ulong _challenge;
		string _gameVersion;

		public static ClientConnect New(ulong uuid, ulong challenge, string gameVersion) {
			instance._uuid = uuid;
			instance._challenge = challenge;
			instance._gameVersion = gameVersion;
			return instance;
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _uuid);
			archive.Serialize(ref _challenge);
			archive.Serialize(ref _gameVersion);
		}

		public ulong uuid {
			get {
				return _uuid;
			}
		}

		public ulong challenge {
			get {
				return _challenge;
			}
		}

		public string gameVersion {
			get {
				return _gameVersion;
			}
		}

		// clients never receive this message.
		public override bool isClientMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ClientTravel : UnifiedNetMsg<ClientTravel> {

		string _levelName;
		HashSetList<int> _travelActorNetIDs;

		public static ClientTravel New(string levelName, HashSetList<int> travelActorNetIDs) {
			instance._levelName = levelName;
			instance._travelActorNetIDs = travelActorNetIDs;
			return instance;
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _levelName);

			int numIds = (_travelActorNetIDs != null) ? _travelActorNetIDs.Values.Count : 0;

			archive.Serialize(ref numIds);

			if (archive.isLoading) {
				if (_travelActorNetIDs != null) {
					_travelActorNetIDs.Clear();
				} else {
					_travelActorNetIDs = new HashSetList<int>();
				}

				for (int i = 0; i < numIds; ++i) {
					_travelActorNetIDs.Add(archive.ReadInt());
				}
			} else if (numIds > 0) {
				for (int i = 0; i < numIds; ++i) {
					archive.Write(_travelActorNetIDs.Values[i]);
				}
			}
		}

		public string levelName {
			get {
				return _levelName;
			}
		}

		public HashSetList<int> travelActorNetIDs {
			get {
				return _travelActorNetIDs;
			}
		}

		// Server never receives this
		public override bool isServerMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ClientFinishedTravel : UnifiedNetMsg<ClientFinishedTravel> {

		string _levelName;

		public static ClientFinishedTravel New(string levelName) {
			instance._levelName = levelName;
			return instance;
		}

		public string levelName {
			get {
				return _levelName;
			}
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _levelName);
		}

		// client should never receive this message.
		public override bool isClientMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ClientLevelStarted : UnifiedNetMsg<ClientLevelStarted> {

		public static ClientLevelStarted New() {
			return instance;
		}

		public override void Serialize(Archive archive) { }

		// client should never receive this message.
		public override bool isClientMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ServerFinishedTravel : UnifiedNetMsg<ServerFinishedTravel> {

		public static ServerFinishedTravel New() {
			return instance;
		}

		public override void Serialize(Archive archive) {}

		// server never receives this message.
		public override bool isServerMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ReplicatedObjectData : UnifiedNetMsg<ReplicatedObjectData> {
		const int MAX_MESSAGE_SIZE = 8*1024;

		NetArchive _archive;
		System.IO.MemoryStream stream;

		public ReplicatedObjectData() {
			stream = new System.IO.MemoryStream();
			_archive = new NetArchive(stream, false, true);
		}

		public static ReplicatedObjectData New() {
			instance.stream.Position = 0;
			instance._archive.OpenWrite();
			return instance;
		}

		public override void Serialize(Archive archive) {
			_archive.Flush();
			int len = (int)stream.Position;
			archive.Serialize(ref len);

			if (archive.isLoading) {
				stream.SetLength(len);
				if (archive.Read(stream.GetBuffer(), 0, len) != len) {
					throw new System.IO.IOException("Premature end of archive stream!");
				}
				stream.Position = 0;
				_archive.OpenRead();
			} else if (len > 0) {
				archive.Write(stream.GetBuffer(), 0, len);
			}
		}

		public ReplicatedObjectData Flush(NetConnection connection) {
			_archive.Flush();
			if (_archive.Position > 0) {
				connection.SendReliable(this);
				return New();
			}
			return this;
		}

		public ReplicatedObjectData MaybeSend(NetConnection connection) {
			if (_archive.Position >= MAX_MESSAGE_SIZE) {
				_archive.Flush();
				connection.SendReliable(this);
				return New();
			}
			return this;
		}

		public NetArchive archive {
			get {
				return _archive;
			}
		}

		// Server never receives replicated object data
		// Data goes from server->client ONLY.

		public override bool isServerMsg {
			get {
				return false;
			}
		}
	}

	public sealed class ReplicatedObjectRPC : UnifiedNetMsg<ReplicatedObjectRPC> {

		int _netID;
		ushort _rpcID;
		NetArchive _archive;
		System.IO.MemoryStream stream;

		public ReplicatedObjectRPC() {
			stream = new System.IO.MemoryStream();
			_archive = new NetArchive(stream, false, true);
		}

		public static ReplicatedObjectRPC New(int netID, ushort rpcID) {
			instance._netID = netID;
			instance._rpcID = rpcID;
			instance.stream.Position = 0;
			instance._archive.OpenWrite();
			return instance;
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _netID);
			archive.Serialize(ref _rpcID);

			_archive.Flush();

			int len = (int)stream.Position;
			archive.Serialize(ref len);

			if (archive.isLoading) {
				stream.SetLength(len);
				if (archive.Read(stream.GetBuffer(), 0, len) != len) {
					throw new System.IO.IOException("Premature end of archive stream!");
				}
				stream.Position = 0;
				_archive.OpenRead();
			} else if (len > 0) {
				archive.Write(stream.GetBuffer(), 0, len);
			}
		}

		public NetArchive archive {
			get {
				return _archive;
			}
		}

		public ushort rpcID {
			get {
				return _rpcID;
			}
		}

		public int netID {
			get {
				return _netID;
			}
		}
	}

	public sealed class Ping : UnifiedNetMsg<Ping> {

		public uint _time;

		public static Ping New(uint time) {
			instance._time = time;
			return instance;
		}

		public uint time {
			get {
				return _time;
			}
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _time);
		}

	}

	public sealed class Pong : UnifiedNetMsg<Pong> {

		public uint _time;

		public static Pong New(Ping ping) {
			instance._time = ping.time;
			return instance;
		}

		public uint time {
			get {
				return _time;
			}
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _time);
		}
	}

	public sealed class DestroyActor : UnifiedNetMsg<DestroyActor> {
		int _netID;

		public static DestroyActor New(int netID) {
			instance._netID = netID;
			return instance;
		}

		public int netID {
			get {
				return _netID;
			}
		}

		// server never receives this message
		public override bool isServerMsg {
			get {
				return false;
			}
		}

		public override void Serialize(Archive archive) {
			archive.Serialize(ref _netID);
		}
	}
}

public abstract partial class World {
	void OnNetMsg(NetMsgs.Disconnect msg, ActorReplicationChannel channel) {
		OnDisconnect(channel, msg.reason, msg.message);
	}

	void OnNetMsg(NetMsgs.ReplicatedObjectRPC msg, ActorReplicationChannel channel) {

		bool isServer = this is Server.ServerWorld;

		if (isServer) {
			if (isTraveling || (channel.clientLevel == null) || (channel.clientLevel != currentLevel)) {
				// discard RPC's from clients if we are traveling.
				return;
			}
		}

		var obj = GetObjectByNetID(msg.netID);
		if (obj != null) {
			var rpcTarget = (ActorRPCObject)obj;
			var rpc = rpcTarget.rpcTable.GetObjectRPCByID(msg.rpcID);
			if (rpc != null) {
				if (isServer) {
					if (rpc.rpcInfo.Domain != ERPCDomain.Server) {
						throw new ActorReplicationException("Non server RPC invoked by client!");
					}
				}
				var args = rpc.Read(msg.archive, _rpcReferenceCollector);
				rpc.method.Invoke(obj, args);
				rpc.Flush();
			}
		}
	}

	void OnNetMsg(NetMsgs.Ping msg, ActorReplicationChannel channel) {
		channel.connection.SendReliable(NetMsgs.Pong.New(msg));
	}

	void OnNetMsg(NetMsgs.Pong msg, ActorReplicationChannel channel) {
		channel.Pong(msg);
	}
}