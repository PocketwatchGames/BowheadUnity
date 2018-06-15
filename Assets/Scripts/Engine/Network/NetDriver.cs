// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Assertions;
using System.Collections.Generic;

public enum ENetRole {
	None,
	Simulated,
	Autonomous,
	Authority
}

public struct NetIOMetrics {
	public int numPacketsSent;
	public int numPacketsRecv;
	public int bytesSent;
	public int bytesRecv;

	public static NetIOMetrics operator +(NetIOMetrics a, NetIOMetrics b) {
		var r = new NetIOMetrics();
		r.bytesRecv = a.bytesRecv + b.bytesRecv;
		r.bytesSent = a.bytesSent + b.bytesSent;
		r.numPacketsRecv = a.numPacketsRecv + b.numPacketsRecv;
		r.numPacketsSent = a.numPacketsSent + b.numPacketsSent;
		return r;
	}

	public static readonly NetIOMetrics zero = new NetIOMetrics();

	public override string ToString() {
		return string.Format("{0:0.00}/{1:0.00} bytes/sec (out/in) | {2}/{3} packets(out/in)", bytesSent, bytesRecv, numPacketsSent, numPacketsRecv);
	}
}

public interface NetDriverCallbacks {
	void OnConnect(NetDriverConnection connection);
	void OnDisconnect(NetDriverConnection connection);
	void OnMessageReceived(NetDriverConnection connection, byte[] data, int size);
	void OnInvalidMessageReceived(NetDriverConnection connection);
	void OnReliableSendWouldBlock(NetDriverConnection connection);
}

public interface NetDriver : System.IDisposable {

	void TickServer(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics);
	void TickClient(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics);

	bool Listen(int port, int maxConnections, NetDriverCallbacks callbacks);
	bool Connect(string address, int port, NetDriverCallbacks callbacks);
}

public interface NetDriverConnection : System.IDisposable {

	string address {
		get;
	}

	bool isValid {
		get;
	}

	bool blocking {
		get;
		set;
	}

	ActorReplicationChannel outer {
		get;
		set;
	}

	void SendReliable(byte[] buffer, int numBytes);
	void GetIOMetrics(ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics, bool clear);
}
