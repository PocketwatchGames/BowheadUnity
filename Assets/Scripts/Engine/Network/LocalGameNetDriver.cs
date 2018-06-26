// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections;

// Buffers network traffic and passes to the server/client.

public class LocalGameNetDriver : INetDriver {
	const int MAX_SEND_BUFFER_SIZE = 1024*96*10;

	LocalGameNetDriverConnection serverSend;
	LocalGameNetDriverConnection clientSend;

	byte[] serverSendBuffer = new byte[MAX_SEND_BUFFER_SIZE];
	byte[] clientSendBuffer = new byte[MAX_SEND_BUFFER_SIZE];
	int serverSendOfs;
	int clientSendOfs;
	

	public void TickServer(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		Perf.Begin("LocalGameNetDriver.TickServer");

		if (serverSend != null) {
			if (serverSend.connectPending) {
				serverSend.connectPending = false;
				Assert.IsNotNull(clientSend);
				clientSend.connectPending = true;
				serverSend.callbacks.OnConnect(serverSend);
			}

			if ((serverSend != null) && (clientSendOfs > 0)) {
				RecvMessages(clientSendBuffer, clientSendOfs, recvBuffer, serverSend, ref reliableChannelMetrics, ref unreliableChannelMetrics);
				clientSendOfs = 0;
			}

			if ((serverSend != null) && (serverSend.disconnectPending)) {
				serverSend.Dispose();
				serverSend = null;
			}
		}

		Perf.End();
	}

	public void TickClient(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		Perf.Begin("LocalGameNetDriver.TickClient");
		if (clientSend != null) {
			if (clientSend.connectPending) {
				clientSend.connectPending = false;
				clientSend.callbacks.OnConnect(clientSend);
			}

			if ((clientSend != null) && (serverSendOfs > 0)) {
				RecvMessages(serverSendBuffer, serverSendOfs, recvBuffer, clientSend, ref reliableChannelMetrics, ref unreliableChannelMetrics);
				serverSendOfs = 0;
			}

			if ((clientSend != null) && clientSend.disconnectPending) {
				clientSend.Dispose();
				clientSend = null;
			}
		}
		Perf.End();
	}

	public bool Listen(int port, int maxConnections, INetDriverCallbacks callbacks) {
		Assert.IsNull(clientSend);
		Assert.IsNull(serverSend);
		serverSend = new LocalGameNetDriverConnection(this, callbacks);
		return true;
	}

	public bool Connect(string address, int port, INetDriverCallbacks callbacks) {
		Assert.IsNotNull(serverSend);
		Assert.IsNull(clientSend);
		clientSend = new LocalGameNetDriverConnection(this, callbacks);
		serverSend.connectPending = true;
		return true;
	}

	public void Dispose() {
		if (clientSend != null) {
			clientSend.Dispose();
			clientSend = null;
		}
		if (serverSend != null) {
			serverSend.Dispose();
			serverSend = null;
		}
	}

	void RecvMessages(byte[] src, int srcLen, byte[] dst, LocalGameNetDriverConnection conn, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {
		Perf.Begin("LocalGameNetDriver.RecvMessages");

		for (int i = 0; i < srcLen-4;) {
			if (!conn.isValid) {
				break;
			}

			int msgLen = System.BitConverter.ToInt32(src, i);
			i += 4;

			Assert.IsTrue(msgLen > 0);

			if (msgLen > 0) {
				System.Array.Copy(src, i, dst, 0, msgLen);
				try {
					Perf.Begin("LocalGameNetDriver.RecvMessages.Callbacks.OnMessageReceived");
					conn.callbacks.OnMessageReceived(conn, dst, msgLen);
					Perf.End();
				} catch (System.Exception e) {
					Debug.LogException(e);
				}
			} else {
				conn.callbacks.OnInvalidMessageReceived(conn);
			}

			i += msgLen; // increment before callback in-case there is an exception

			reliableChannelMetrics.bytesRecv += msgLen;
			++reliableChannelMetrics.numPacketsRecv;
		}

		Perf.End();
	}

	void WriteMessage(byte[] src, int srcLen, byte[] dst, ref int dstOfs) {
		dst[dstOfs+0] = (byte)(srcLen & 0xff);
		dst[dstOfs+1] = (byte)((srcLen >>  8) & 0xff);
		dst[dstOfs+2] = (byte)((srcLen >> 16) & 0xff);
		dst[dstOfs+3] = (byte)((srcLen >> 24) & 0xff);
		dstOfs += 4;

		System.Array.Copy(src, 0, dst, dstOfs, srcLen);
		dstOfs += srcLen;
	}

	internal void SendUnreliable(LocalGameNetDriverConnection conn, byte[] buffer, int numBytes) {
		SendReliable(conn, buffer, numBytes);
	}

	internal void SendReliable(LocalGameNetDriverConnection conn, byte[] buffer, int numBytes) {
		Perf.Begin("LocalGameNetDriver.SendReliable");
		if (conn == serverSend) {
			WriteMessage(buffer, numBytes, serverSendBuffer, ref serverSendOfs);
		} else {
			Assert.IsNotNull(clientSend);
			WriteMessage(buffer, numBytes, clientSendBuffer, ref clientSendOfs);
		}
		Perf.End();
	}

	internal void Disconnect(LocalGameNetDriverConnection conn) {
		if (conn == serverSend) {
			serverSend = null;
			if (clientSend != null) {
				clientSend.disconnectPending = true;
			}
		} else if (conn == clientSend) {
			clientSend = null;
			if (serverSend != null) {
				serverSend.disconnectPending = true;
			}
		}
	}
}

public class LocalGameNetDriverConnection : NetDriverConnection {

	ActorReplicationChannel _outer;
	LocalGameNetDriver netDriver;
	NetIOMetrics reliableMetrics;
	internal INetDriverCallbacks callbacks;
	internal bool connectPending;
	internal bool disconnectPending;

	internal LocalGameNetDriverConnection(LocalGameNetDriver netDriver, INetDriverCallbacks callbacks) {
		this.netDriver = netDriver;
		this.callbacks = callbacks;
	}

	public void SendUnreliable(byte[] buffer, int numBytes) {
		Perf.Begin("LocalGameNetDriverConnection.SendUnreliable");
		netDriver.SendUnreliable(this, buffer, numBytes);
		reliableMetrics.bytesSent += numBytes;
		++reliableMetrics.numPacketsSent;
		Perf.End();
	}

	public void SendReliable(byte[] buffer, int numBytes) {
		Perf.Begin("LocalGameNetDriverConnection.SendReliable");
		netDriver.SendReliable(this, buffer, numBytes);
		reliableMetrics.bytesSent += numBytes;
		++reliableMetrics.numPacketsSent;
		Perf.End();
	}

	public void Dispose() {
		if (callbacks != null) {
			callbacks.OnDisconnect(this);
			callbacks = null;
			netDriver.Disconnect(this);
		}
	}

	public void GetIOMetrics(ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics, bool clear) {
		reliableMetrics += this.reliableMetrics;
		unreliableMetrics += new NetIOMetrics();

		if (clear) {
			this.reliableMetrics = new NetIOMetrics();
		}
	}

	public string address {
		get {
			return "localgamenet";
		}
	}

	public bool isValid {
		get {
			return callbacks != null;
		}
	}

	public ActorReplicationChannel outer {
		get {
			return _outer;
		}

		set {
			_outer = value;
		}
	}

	public bool blocking {
		get;
		set;
	}
}
