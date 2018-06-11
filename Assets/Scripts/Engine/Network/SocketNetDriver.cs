// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using System.Net;
using System.Net.Sockets;

public class SocketNetDriver : NetDriver {
	const float UDP_CONTROL_RESEND_TIMEOUT = 0.5f;
	const float UDP_CONTROL_DISCONNECT_TIMEOUT = 8f;

	enum EControlCode {
		SetChannelID,
		AckChannelID
	}

	Socket[] _serverSock = new Socket[2];
	SocketNetDriverConnection _serverConnection;
	DictionaryList<EndPoint, SocketNetDriverConnection> _tcpConnections = new DictionaryList<EndPoint, SocketNetDriverConnection>();
	DictionaryList<EndPoint, SocketNetDriverConnection> _udpConnections = new DictionaryList<EndPoint, SocketNetDriverConnection>();
	NetDriverCallbacks _serverCallbacks;
	NetDriverCallbacks _clientCallbacks;
	EndPoint _recvEndPoint = new IPEndPoint(IPAddress.Any, 0);
	int nextChannelID;
	float _sendUdpControlTimeout;

	static readonly byte[] controlCodeBytes = new byte[3];

	public void TickServer(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {

		Perf.Begin("SocketNetDriver.TickServer");

		for (;;) {
			Socket newSocket;
			try {
				newSocket = _serverSock[0].Accept();
			} catch (SocketException) {
				break;
			}

			if (_tcpConnections.ContainsKey(newSocket.RemoteEndPoint)) {
				Debug.LogError("Connection attempt from already connected client!");
				newSocket.Close();
				continue;
			}

			var clientSocks = new Socket[2];
			clientSocks[0] = newSocket;
			clientSocks[1] = _serverSock[1];

			newSocket.Blocking = false;
			//newSocket.NoDelay = true;
			newSocket.SendBufferSize = World.MAX_RELIABLE_MESSAGE_SIZE*8;
			newSocket.SendTimeout = 500;

			var newConn = new SocketNetDriverConnection(this, clientSocks, nextChannelID++);
			_tcpConnections.Add(newSocket.RemoteEndPoint, newConn);
			SendTcpControl(newConn);
        }

		for (int i = 0; i < _tcpConnections.Values.Count;) {
			var c = _tcpConnections.Values[i];

			bool wasReset = false;
			bool isDisposed = false;

			try {
				wasReset = c.sockets[0].Poll(0, SelectMode.SelectRead);
			} catch (Exception) {
				isDisposed = true;
			}

			if (isDisposed || (wasReset && (c.sockets[0].Available == 0))) {
				c.Dispose();
				continue;
			}

			Recv(c, _serverCallbacks, c.sockets[0], recvBuffer, ref reliableChannelMetrics, false);

			if (c.isValid && !c.didHandshake) {
				c.handshakeTime += dt;
				if (c.handshakeTime > UDP_CONTROL_DISCONNECT_TIMEOUT) {
					// handshake failed
					Debug.LogError("Disconnecting " + c.tcpEndpoint.ToString() + ": udp handshake timed out");
					c.Dispose();
				}
			}

			if (c.isValid) {		
				++i;
			}
		}

		RecvFrom(_serverCallbacks, _serverSock[1], recvBuffer, ref unreliableChannelMetrics);

		Perf.End();
	}

	public void TickClient(float dt, byte[] recvBuffer, ref NetIOMetrics reliableChannelMetrics, ref NetIOMetrics unreliableChannelMetrics) {

		Perf.Begin("SocketNetDriver.TickClient");

		if (_serverConnection != null) {

			bool wasReset = false;
			bool isDisposed = false;

			try {
				wasReset = _serverConnection.sockets[0].Poll(0, SelectMode.SelectRead);
			} catch (Exception) {
				isDisposed = true;
			}

			if (isDisposed || (wasReset && (_serverConnection.sockets[0].Available == 0))) {
				Debug.LogError("Server connection was reset.");
				_serverConnection.Dispose();
				// don't set _serverConnection to null so NetDriverConnection's can be cleaned up correctly.
				Perf.End();
				return;
			}

			Recv(_serverConnection, _clientCallbacks, _serverConnection.sockets[0], recvBuffer, ref reliableChannelMetrics, false);

			if (_serverConnection != null) {
				Recv(_serverConnection, _clientCallbacks, _serverConnection.sockets[1], recvBuffer, ref unreliableChannelMetrics, true);
			}

			if (_serverConnection != null) {
				if (!_serverConnection.didHandshake && (_serverConnection.channelID != -1)) {
					_sendUdpControlTimeout -= dt;
					if (_sendUdpControlTimeout <= 0f) {
						_sendUdpControlTimeout = UDP_CONTROL_RESEND_TIMEOUT;
						SendUdpControl(_serverConnection);
					}
                }
			}
		}

		Perf.End();
	}

	void SendTcpControl(SocketNetDriverConnection connection) {
		controlCodeBytes[0] = (byte)EControlCode.SetChannelID;
		controlCodeBytes[1] = (byte)(connection.channelID & 0xff);
		controlCodeBytes[2] = (byte)((connection.channelID >> 8) & 0xff);
		connection.SendReliable(controlCodeBytes, 3);
	}

	void SendUdpControl(SocketNetDriverConnection connection) {
		controlCodeBytes[0] = (byte)EControlCode.SetChannelID;
		controlCodeBytes[1] = (byte)(connection.channelID & 0xff);
		controlCodeBytes[2] = (byte)((connection.channelID >> 8) & 0xff);
		connection.SendUnreliable(controlCodeBytes, 3);
	}

	void SendTcpControlAck(SocketNetDriverConnection connection) {
		controlCodeBytes[0] = (byte)EControlCode.AckChannelID;
		connection.SendReliable(controlCodeBytes, 1);
	}

	int RecvControl(byte[] bytes, int len) {
		if (len != 3) {
			return -1;
		}
		if (bytes[0] != (byte)EControlCode.SetChannelID) {
			return -1;
		}
		int id = ((int)bytes[1]) | (((int)bytes[2]) << 8);
		return id;
	}

	void RecvFrom(NetDriverCallbacks callbacks, Socket socket, byte[] buffer, ref NetIOMetrics metrics) {
		Perf.Begin("SocketNetDriver.RecvFrom");

		while (socket.Available > 0) {
			int r;

			try {
				r = socket.ReceiveFrom(buffer, 0, World.MAX_UNRELIABLE_MESSAGE_SIZE, SocketFlags.None, ref _recvEndPoint);
			} catch (Exception e) {
				Debug.LogException(e);

				SocketNetDriverConnection conn;
				if (_udpConnections.TryGetValue(_recvEndPoint, out conn)) {
					callbacks.OnInvalidMessageReceived(conn);
				}

				continue;
			}

			if (r <= 0) {
				throw new SocketException((int)SocketError.SocketError);
			}

			metrics.bytesRecv += r;
			++metrics.numPacketsRecv;

			SocketNetDriverConnection connection;
			if (_udpConnections.TryGetValue(_recvEndPoint, out connection)) {
				if (connection.isValid) {
					if (r > 3) { 
						// currently NetMsgs are always more than 3 bytes, and this guarantees that we don't 
						// try to process a duplicated control udp control message.

						callbacks.OnMessageReceived(connection, buffer, r);
					}
				}
			} else {

				// is this a control code?
				var id = RecvControl(buffer, r);
				if (id != -1) {
					for (int i = 0; i < _tcpConnections.Values.Count; ++i) {
						var c = _tcpConnections.Values[i];
						if (c.channelID == id) {
							if (_udpConnections.Values.Contains(c)) {
								Debug.LogWarning("UDP control message received for registered channel.");
							} else {
								c.udpEndpoint = new IPEndPoint(((IPEndPoint)_recvEndPoint).Address, ((IPEndPoint)_recvEndPoint).Port);
                                _udpConnections.Add(c.udpEndpoint, c);
								SendTcpControlAck(c);
								c.didHandshake = true;
								callbacks.OnConnect(c);
								break;
							}
						}
					}
				}
			}
		}

		Perf.End();
	}

	void Recv(SocketNetDriverConnection connection, NetDriverCallbacks callbacks, Socket socket, byte[] buffer, ref NetIOMetrics metrics, bool isDatagram) {
		Perf.Begin("SocketNetDriver.Recv");

		if (isDatagram) {
			while (connection.isValid && (socket.Available > 0)) {
				int r;
				try {
					r = socket.Receive(buffer, 0, World.MAX_UNRELIABLE_MESSAGE_SIZE, SocketFlags.None);
					if (r <= 0) {
						throw new SocketException((int)SocketError.SocketError);
					}
					metrics.bytesRecv += r;
					++metrics.numPacketsRecv;
				} catch (Exception e) {
					Debug.LogException(e);
					callbacks.OnInvalidMessageReceived(connection);
					continue;
				}

				if (!connection.didHandshake) {
					// client may receive a UDP packet before receiving control ACK
					// so discard the packet until we process the ACK.
					continue;
				}

				callbacks.OnMessageReceived(connection, buffer, r);
			}
		} else {
			while (connection.isValid && (socket.Available > 0)) {

				if (connection.pendingRecvSize <= 0) {
					if (socket.Available < 2) {
						break;
					}

					// read from socket.
					if (socket.Receive(connection.pendingBytes, 0, 2, SocketFlags.None) != 2) {
						throw new SocketException((int)SocketError.SocketError);
					}

					connection.pendingRecvSize = ((int)connection.pendingBytes[0]) | (((int)connection.pendingBytes[1]) << 8);
					connection.pendingBytesReceived = 0;

					if (connection.pendingRecvSize > connection.pendingBytes.Length) {
						callbacks.OnInvalidMessageReceived(connection);
						continue;
					}
				}

				{
					// read from socket.
					var numBytesToRead = Mathf.Min(socket.Available, connection.pendingRecvSize - connection.pendingBytesReceived);
					if (numBytesToRead > 0) {
						if (socket.Receive(connection.pendingBytes, connection.pendingBytesReceived, numBytesToRead, SocketFlags.None) != numBytesToRead) {
							throw new SocketException((int)SocketError.SocketError);
						}
						connection.pendingBytesReceived += numBytesToRead;
					}
				}

				Assert.IsTrue(connection.pendingBytesReceived <= connection.pendingRecvSize);

				if (connection.pendingBytesReceived >= connection.pendingRecvSize) {

					if (!connection.didHandshake) {
						if (callbacks == _clientCallbacks) {
							if (connection.channelID == -1) {
								var id = RecvControl(connection.pendingBytes, connection.pendingRecvSize);
								if (id == -1) {
									connection.Dispose();
									break;
								}
								connection.channelID = id;
								_sendUdpControlTimeout = UDP_CONTROL_RESEND_TIMEOUT;
								SendUdpControl(connection);
							} else if (connection.pendingBytes[0] == (byte)EControlCode.AckChannelID) {
								connection.didHandshake = true;
								callbacks.OnConnect(connection);
							} else {
								// invalid response
								connection.Dispose();
								break;
							}
						} else {
							connection.Dispose();
							break;
						}

						connection.pendingRecvSize = 0;
						connection.pendingBytesReceived = 0;
						continue;
                    }

					Array.Copy(connection.pendingBytes, buffer, connection.pendingRecvSize);

					var r = connection.pendingRecvSize;

					connection.pendingBytesReceived = 0;
					connection.pendingRecvSize = 0;
					
					metrics.bytesRecv += r;
					++metrics.numPacketsRecv;
									
					callbacks.OnMessageReceived(connection, buffer, r);
					continue;
				}

				// not enough data ready
				break;
			}
		}

		Perf.End();
	}

	public bool Listen(int port, int maxConnections, NetDriverCallbacks callbacks) {

		_serverCallbacks = callbacks;

		var endPoint = new IPEndPoint(IPAddress.Any, port);

		_serverSock[0] = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		_serverSock[1] = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

		try {
			_serverSock[0].Bind(endPoint);
			_serverSock[0].Listen(maxConnections);

			_serverSock[1].Bind(endPoint);
			_serverSock[1].ReceiveBufferSize = World.MAX_UNRELIABLE_MESSAGE_SIZE*4;
			_serverSock[1].DisableICMPUnreachablePortError();
			
			_serverSock[0].Blocking = false;
			//_serverSock[1].Blocking = false;

		} catch (Exception e) {
			Debug.LogException(e);
			return false;
		}

		return true;
	}

	static IPAddress GetIPAddressFromString(string address) {
		var type = System.Uri.CheckHostName(address);
		if (type == UriHostNameType.Dns) {
			var host = Dns.GetHostEntry(address);
			return host.AddressList[0];
		}

		IPAddress addr;
		if (IPAddress.TryParse(address, out addr)) {
			return addr;
		}

		return null;
	}

	public bool Connect(string address, int port, NetDriverCallbacks callbacks) {

		if (address == "localhost") {
			address = "127.0.0.1";
		}

		var serverIP = GetIPAddressFromString(address);
		if (serverIP == null) {
			Debug.LogError("Could not resolve " + address);
			return false;
		}

		_clientCallbacks = callbacks;

		var serverEndPoint = new IPEndPoint(serverIP, port);

		var clientSocks = new Socket[2];

		clientSocks[0] = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		clientSocks[1] = new Socket(serverEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

		try {
			clientSocks[0].NoDelay = true;
			clientSocks[0].SendTimeout = 500;
			clientSocks[0].ReceiveBufferSize = World.MAX_RELIABLE_MESSAGE_SIZE;

			clientSocks[1].SendBufferSize = World.MAX_UNRELIABLE_MESSAGE_SIZE*4;
			clientSocks[1].ReceiveBufferSize = World.MAX_UNRELIABLE_MESSAGE_SIZE*4;
			clientSocks[1].DisableICMPUnreachablePortError();

			Debug.Log("Connecting to " + serverEndPoint.Address.ToString());

			clientSocks[0].Connect(serverEndPoint);
			clientSocks[1].Bind(new IPEndPoint(IPAddress.Any, 0));

			Debug.Log("Connected to " + serverEndPoint.Address.ToString());

			_serverConnection = new SocketNetDriverConnection(this, clientSocks, -1);
			_serverConnection.udpEndpoint = clientSocks[0].RemoteEndPoint;

		} catch (Exception e) {
			Debug.LogException(e);
			return false;
		}

		return true;
	}

	public void Dispose() {
		_serverCallbacks = null;
		_clientCallbacks = null;

		for (int i = 0; i < 2; ++i) {
			if (_serverSock[i] != null) {
				try {
					_serverSock[i].Close();
				} catch (Exception) { }
				_serverSock[i] = null;
			}

			var connections = new List<SocketNetDriverConnection>(_tcpConnections.Values);
			foreach (var c in connections) {
				c.Dispose();
			}

			_tcpConnections.Clear();
			_udpConnections.Clear();
		}
	}

	internal void ConnectionDisposed(SocketNetDriverConnection connection) {
		if (connection == _serverConnection) {
			try {
				connection.sockets[1].Close();
			} catch (Exception) {}

			_serverConnection = null;
			if ((_clientCallbacks != null) && connection.didHandshake) {
				_clientCallbacks.OnDisconnect(connection);
			}
		} else {
			bool r = _tcpConnections.Remove(connection.tcpEndpoint);
			Assert.IsTrue(r);

			if (connection.udpEndpoint != null) {
				r = _udpConnections.Remove(connection.udpEndpoint);
				Assert.IsTrue(r);
			}

			if ((_serverCallbacks != null) && connection.didHandshake) {
				_serverCallbacks.OnDisconnect(connection);
			}
		}

		try {
			connection.sockets[0].Close();
		} catch (Exception) { }

		connection.sockets[0] = null;
		connection.sockets[1] = null;
	}

	internal void ReliableSendWouldBlock(SocketNetDriverConnection connection) {
		if (_serverCallbacks != null) {
			_serverCallbacks.OnReliableSendWouldBlock(connection);
		}
		if (_clientCallbacks != null) {
			_clientCallbacks.OnReliableSendWouldBlock(connection);
		}
	}
}

public class SocketNetDriverConnection : NetDriverConnection {
	SocketNetDriver _driver;
	NetIOMetrics reliableMetrics;
	NetIOMetrics unreliableMetrics;
	IPEndPoint _tcpEndPoint;
	bool _disposed;
	static readonly byte[] msgTemp = new byte[World.MAX_RELIABLE_MESSAGE_SIZE+2];

	public readonly byte[] pendingBytes = new byte[World.MAX_RELIABLE_MESSAGE_SIZE];
	public int pendingRecvSize;
	public int pendingBytesReceived;

	public Socket[] sockets {
		get;
		private set;
	}

	public EndPoint udpEndpoint {
		get;
		set;
	}

	public EndPoint tcpEndpoint {
		get {
			return _tcpEndPoint;
		}
	}

	public bool didHandshake {
		get;
		set;
	}

	public float handshakeTime {
		get;
		set;
	}

	public int channelID {
		get;
		set;
	}

	public bool blocking {
		get {
			return sockets[0].Blocking;
		}
		set {
			sockets[0].Blocking = value;
		}
	}

	public SocketNetDriverConnection(SocketNetDriver driver, Socket[] sockets, int channelID) {
		_driver = driver;
		this.sockets = sockets;
		this.channelID = channelID;
		var ipEndPoint = (IPEndPoint)sockets[0].RemoteEndPoint;
		_tcpEndPoint = new IPEndPoint(ipEndPoint.Address, ipEndPoint.Port);
		address = sockets[0].RemoteEndPoint.ToString();
	}

	public void SendUnreliable(byte[] buffer, int numBytes) {
		Perf.Begin("SocketNetDriverConnection.SendUnreliable");

		try {
			var numSent = sockets[1].SendTo(buffer, 0, numBytes, SocketFlags.None, udpEndpoint);
            if (numSent != numBytes) {
				Debug.LogError("Unreliable socket send did not send all bytes (tried to send " + numBytes + " byte(s) but only sent " + numSent + ".");
				throw new SocketException((int)SocketError.SocketError);
			}
			++unreliableMetrics.numPacketsSent;
			unreliableMetrics.bytesSent += numBytes;
		} catch (Exception e) {
			Debug.LogError("Unreliable socket send failed with exception (tried to send " + numBytes + " byte(s).");
			Debug.LogException(e);
			Dispose();
		}

		Perf.End();
	}

	public void SendReliable(byte[] buffer, int numBytes) {
		Perf.Begin("SocketNetDriverConnection.SendReliable");

		Assert.IsTrue(numBytes <= ushort.MaxValue);

		msgTemp[0] = (byte)(numBytes & 0xff);
		msgTemp[1] = (byte)((numBytes >> 8) & 0xff);
		try {

			Array.Copy(buffer, 0, msgTemp, 2, numBytes);

			numBytes += 2;

			int numTries = 0;
			int numSent = 0;

			do {
				var bytesSent = sockets[0].Send(msgTemp, numSent, numBytes-numSent, SocketFlags.None);
				numSent += bytesSent;
			} while ((numSent < numBytes) && (++numTries < 15));

			if (numSent != numBytes) {
				throw new Exception("Reliable socket send did not send all bytes (tried to send " + numBytes + " byte(s) but only sent " + numSent + ".");
			}

			++reliableMetrics.numPacketsSent;
			reliableMetrics.bytesSent += numBytes;
		} catch (Exception e) {
			Debug.LogException(e);

			var socketException = e as SocketException;
			if (socketException != null) {
				if (socketException.SocketErrorCode == SocketError.WouldBlock) {
					_driver.ReliableSendWouldBlock(this);
					return;
				}
			}
			
			Dispose();
		}

		Perf.End();
	}

	public void GetIOMetrics(ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics, bool clear) {
		reliableMetrics += this.reliableMetrics;
		unreliableMetrics += this.unreliableMetrics;

		if (clear) {
			this.reliableMetrics = new NetIOMetrics();
			this.unreliableMetrics = new NetIOMetrics();
		}
	}

	public void Dispose() {
		if (!_disposed) {
			_disposed = true;
			_driver.ConnectionDisposed(this);
			sockets[0] = null;
			sockets[1] = null;
		}
	}

	public string address {
		get;
		private set;
	}

	public bool isValid {
		get {
			return !_disposed;
		}
	}

	public ActorReplicationChannel outer {
		get;
		set;
	}

}
