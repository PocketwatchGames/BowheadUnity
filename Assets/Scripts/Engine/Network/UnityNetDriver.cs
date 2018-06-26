// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Assertions;
using System.Collections.Generic;

public class UnityNetDriver : INetDriver {
	int hostSocketID = -1;
	int hostReliableChannelID;
	int hostUnreliableChannelID;
	int clientSocketID = -1;
	int clientReliableChannelID;
	int clientUnreliableChannelID;
	INetDriverCallbacks hostCallbacks;
	INetDriverCallbacks clientCallbacks;

	IntHashtable<UnityNetDriverConnection> hostConnections = new IntHashtable<UnityNetDriverConnection>();
	IntHashtable<UnityNetDriverConnection> clientConnections = new IntHashtable<UnityNetDriverConnection>();
	
	public UnityNetDriver() {
		var config = new GlobalConfig();
		config.ThreadAwakeTimeout = 1;
		config.ReactorMaximumReceivedMessages = ushort.MaxValue;
		config.ReactorMaximumSentMessages = ushort.MaxValue;
		NetworkTransport.Init(config);

		Debug.Log("UNET initialized.");
	}

	public void TickServer(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		if (hostSocketID != -1) {
			TickSocket(hostConnections, hostCallbacks, hostSocketID, hostReliableChannelID, hostUnreliableChannelID, recvBuffer, ref reliableChannelMetrics, ref unreliableChannelMetrics);
		}
	}

	public void TickClient(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		if (clientSocketID != -1) {
			TickSocket(clientConnections, clientCallbacks, clientSocketID, clientReliableChannelID, clientUnreliableChannelID, recvBuffer, ref reliableChannelMetrics, ref unreliableChannelMetrics);
		}
	}

	public bool Listen(int port, int maxConnections, INetDriverCallbacks callbacks) {
		Assert.IsTrue(hostSocketID == -1);

		hostCallbacks = callbacks;

		ConnectionConfig config = new ConnectionConfig();
		config.DisconnectTimeout = 4000;
		config.PacketSize = 1372;
		config.MaxSentMessageQueueSize = ushort.MaxValue;
		config.MaxCombinedReliableMessageCount = ushort.MaxValue;
		config.MaxCombinedReliableMessageSize = 1024;
		config.AcksType = ConnectionAcksType.Acks64;

		hostReliableChannelID = config.AddChannel(QosType.ReliableSequenced);
		hostUnreliableChannelID = config.AddChannel(QosType.Unreliable);
		hostSocketID = NetworkTransport.AddHost(new HostTopology(config, maxConnections), port);
		if (hostSocketID == -1) {
			hostUnreliableChannelID = 0;
			hostReliableChannelID = 0;
		} else {
			Debug.Log("UNET: started server.");
		}
		return hostSocketID != -1;
	}

	public bool Connect(string address, int port, INetDriverCallbacks callbacks) {
		Assert.IsTrue(clientSocketID == -1);

		if (address == "localhost") {
			address = "127.0.0.1";
		}

		clientCallbacks = callbacks;

		ConnectionConfig config = new ConnectionConfig();
		config.DisconnectTimeout = 4000;
		config.PacketSize = 1372;
		config.MaxSentMessageQueueSize = ushort.MaxValue;
		config.MaxCombinedReliableMessageCount = ushort.MaxValue;
		config.MaxCombinedReliableMessageSize = 1024;
		config.AcksType = ConnectionAcksType.Acks64;

		clientReliableChannelID = config.AddChannel(QosType.ReliableSequenced);
		clientUnreliableChannelID = config.AddChannel(QosType.Unreliable);
		clientSocketID = NetworkTransport.AddHost(new HostTopology(config, 1));

		byte error;
		NetworkTransport.Connect(clientSocketID, address, port, 0, out error);
		if (error == (byte)NetworkError.Ok) {
			Debug.Log("UNET: connecting to server (" + address + ":" + port + ")");
		} else {
			Debug.Log("UNET: failed to connect to server (" + address + ":" + port + "), error (" + ((NetworkError)error).ToString() + ")");
		}

		return error == (byte)NetworkError.Ok;
	}

	public void Dispose() {
		var tempConnections = hostConnections;
		hostConnections = new IntHashtable<UnityNetDriverConnection>();

		foreach (var conn in tempConnections.Values) {
			conn.connections = null;
			conn.Dispose();
		}

		tempConnections = clientConnections;
		clientConnections = new IntHashtable<UnityNetDriverConnection>();

		foreach (var conn in tempConnections.Values) {
			conn.connections = null;
			conn.Dispose();
		}

		byte error;

		if (hostSocketID != -1) {
			NetworkTransport.DisconnectNetworkHost(hostSocketID, out error);
			hostSocketID = -1;
			hostReliableChannelID = 0;
			hostUnreliableChannelID = 0;
		}
		if (clientSocketID != -1) {
			NetworkTransport.DisconnectNetworkHost(clientSocketID, out error);
			clientSocketID = -1;
			clientReliableChannelID = 0;
			clientUnreliableChannelID = 0;
		}

		hostCallbacks = null;
		clientCallbacks = null;
	}

	void TickSocket(IntHashtable<UnityNetDriverConnection> connections, INetDriverCallbacks callbacks, int socketID, int reliableChannelID, int unreliableChannelID, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		int connectionID;
		int recvSize;
		int channelID;
		byte error;

		while (true) {
			NetworkEventType eventType = NetworkTransport.ReceiveFromHost(socketID, out connectionID, out channelID, recvBuffer, recvBuffer.Length, out recvSize, out error);
			switch (eventType) {
				case NetworkEventType.ConnectEvent: {
					var conn = GetConnection(connections, connectionID);
					if (conn == null) {
						conn = CreateConnection(connections, callbacks, socketID, connectionID, reliableChannelID, unreliableChannelID);
					}
					callbacks.OnConnect(conn);
				} break;
				case NetworkEventType.DisconnectEvent: {
					var conn = GetConnection(connections, connectionID);
					if (conn == null) {
						conn = CreateConnection(connections, callbacks, socketID, connectionID, reliableChannelID, unreliableChannelID);
					}
					conn.callbacks = null;
					callbacks.OnDisconnect(conn);
					RemoveConnection(connections, connectionID);
				} break;
				case NetworkEventType.DataEvent: {
					var conn = GetConnection(connections, connectionID);
					if (conn != null) {
						if (channelID == reliableChannelID) {
							reliableChannelMetrics.bytesRecv += recvSize;
							++reliableChannelMetrics.numPacketsRecv;
						} else {
							unreliableChannelMetrics.bytesRecv += recvSize;
							++unreliableChannelMetrics.numPacketsRecv;
						}
						if (error == (byte)NetworkError.MessageToLong) {
							callbacks.OnInvalidMessageReceived(conn);
						} else {
							callbacks.OnMessageReceived(conn, recvBuffer, recvSize);
						}
					}
				} break;
				default:
					return;
			}
		}
	}

	UnityNetDriverConnection GetConnection(IntHashtable<UnityNetDriverConnection> connections, int id) {
		UnityNetDriverConnection conn;
		connections.TryGetValue(id.GetHashCode(), out conn);
		return conn;
	}

	UnityNetDriverConnection CreateConnection(IntHashtable<UnityNetDriverConnection> connections, INetDriverCallbacks callbacks, int socketID, int connectionID, int reliableChannelID, int unreliableChannelID) {
		var hashCode = connectionID.GetHashCode();
		if (connections.Contains(hashCode)) {
			throw new System.IO.IOException("Duplicate connection id! " + connectionID);
		}
		var conn = new UnityNetDriverConnection(connections, this, callbacks, socketID, connectionID, reliableChannelID, unreliableChannelID);
		connections[hashCode] = conn;
		return conn;
	}

	public void RemoveConnection(IntHashtable<UnityNetDriverConnection> connections, int id) {
		connections.Remove(id.GetHashCode());
	}
}

public class UnityNetDriverConnection : NetDriverConnection {

	ActorReplicationChannel _outer;
	string _id;
	int _socketID;
	int _connectionID;
	int _unreliableChannel;
	int _reliableChannel;
	NetIOMetrics _reliableMetrics;
	NetIOMetrics _unreliableMetrics;
	internal INetDriverCallbacks callbacks;
	UnityNetDriver _netDriver;
	public IntHashtable<UnityNetDriverConnection> connections;

    public UnityNetDriverConnection(IntHashtable<UnityNetDriverConnection> connections, UnityNetDriver driver, INetDriverCallbacks callbacks, int socketId, int connectionId, int reliableChannel, int unreliableChannel) {
		_netDriver = driver;
		this.callbacks = callbacks;
		_socketID = socketId;
		_connectionID = connectionId;
		_unreliableChannel = unreliableChannel;
		_reliableChannel = reliableChannel;
		this.connections = connections;

		int port;
		ulong network;
		ushort node;
		byte error;

		NetworkTransport.GetConnectionInfo(socketId, connectionId, out port, out network, out node, out error);

		byte[] ip = System.BitConverter.GetBytes(network);
		if (network == ulong.MaxValue) { // loopback
			ip[0] = 127;
			ip[1] = 0;
			ip[2] = 0;
			ip[3] = 0;
		}
		_id = string.Format("{0}.{1}.{2}.{3}:{4}", ip[0], ip[1], ip[2], ip[3], port);
	}

	public void SendUnreliable(byte[] buffer, int numBytes) {
		byte error;
		if (NetworkTransport.Send(_socketID, _connectionID, _unreliableChannel, buffer, numBytes, out error)) {
			_unreliableMetrics.bytesSent += numBytes;
			++_unreliableMetrics.numPacketsSent;
		} else {
			throw new System.IO.IOException("NetworkTransport.Send() returned error (" + ((NetworkError)error).ToString() + ")");
		}
	}

	public void SendReliable(byte[] buffer, int numBytes) {
		byte error;
		if (NetworkTransport.Send(_socketID, _connectionID, _reliableChannel, buffer, numBytes, out error)) {
			_reliableMetrics.bytesSent += numBytes;
			++_reliableMetrics.numPacketsSent;
		} else {
			throw new System.IO.IOException("NetworkTransport.Send() returned error (" + ((NetworkError)error).ToString() + ")");
		}
	}

	public void Dispose() {
		if (callbacks != null) {
			byte error;
			NetworkTransport.Disconnect(_socketID, _connectionID, out error);
			callbacks.OnDisconnect(this);
			callbacks = null;
			if (connections != null) {
				_netDriver.RemoveConnection(connections, _connectionID);
			}
		}
	}

	public void GetIOMetrics(ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics, bool clear) {
		reliableMetrics += _reliableMetrics;
		unreliableMetrics += _unreliableMetrics;

		if (clear) {
			_reliableMetrics = new NetIOMetrics();
			_unreliableMetrics = new NetIOMetrics();
		}
	}

	public string address {
		get {
			return _id;
		}
	}

	public bool isValid {
		get {
			return callbacks != null;
		}
	}

	public bool blocking {
		get;
		set;
	}

	public ActorReplicationChannel outer {
		get {
			return _outer;
		}

		set {
			_outer = value;
		}
	}
}