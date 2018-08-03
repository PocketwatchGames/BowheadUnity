// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;

namespace Client {
	public abstract partial class ClientWorld : World {
		
		ActorReplicationChannel _serverConnection;
		EConnectionState _connectionState = EConnectionState.Disconnected;
		bool _serverTravel;
		bool _wasTraveling;
		
		enum EConnectionState {
			Disconnected,
			Connecting,
			Joining,
			Connected
		}

		public ClientWorld(
			IGameInstance gameInstance,
			Streaming sharedStreaming,
			WorldChunkComponent chunkComponent,
			Transform sceneGroup,
			GameObject defaultActorPrefab,
			GetObjectPoolRootDelegate getStaticPoolRoot,
			GetObjectPoolRootDelegate getTransientPoolRoot,
			System.Reflection.Assembly[] assemblies,
			INetDriver driver
		) : base(gameInstance, sharedStreaming, chunkComponent, sceneGroup, defaultActorPrefab, getStaticPoolRoot, getTransientPoolRoot, new ClientSerializableObjectFactory(assemblies), driver, new ClientNetMsgFactory(assemblies)) {
			
		}

		protected override void BeginTravel(string travelLevel, HashSetList<int> travelActorNetIDs) {
			_wasTraveling = true;
			base.BeginTravel(travelLevel, travelActorNetIDs);
			
		}

		public void Tick(float dt, MonoBehaviour loadingContext, ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			if (!isTraveling) {
				UpdateTime(Mathf.Min(dt*Time.timeScale, 1/3f), dt);
			}

			TickWorldStreaming();

			netDriver.TickClient(Mathf.Min(dt, 1/3f), netMessageBytes, ref reliableMetrics, ref unreliableMetrics);

			if (_serverConnection != null) {
				if (isTraveling || _wasTraveling) {
					TickTravel(loadingContext);
					if (!isTraveling) {
						if (_serverConnection != null) {
							_serverConnection.ResetTimeout();
						}
						FinishTravel();
						OnLevelStart();
						GC.Collect();
					}

					if (_serverConnection != null) {
						_serverConnection.ResetTimeoutForTravel();
					}
				} else {
					TickActors(loadingContext);

#if !UNITY_EDITOR
					if ((_serverConnection != null) && _serverConnection.timedOut) {
						Debug.LogError("Server connection timed out");
						_serverConnection.connection.driverConnection.Dispose();
						_serverConnection = null;
					}
#endif
				}

				if (_serverConnection != null) {
					_serverConnection.Ping(dt);
					_serverConnection.connection.driverConnection.GetIOMetrics(ref reliableMetrics, ref unreliableMetrics, true);
				}

				_wasTraveling = isTraveling;
			}
		}

		public void FixedUpdate(float dt) {
			if (_serverConnection != null) {
				if (!(isTraveling || _wasTraveling)) {
					FixedUpdateActors(dt);
				}
			}
		}

		public virtual void LateUpdate() {
			if (_serverConnection != null) {
				if (!(isTraveling || _wasTraveling)) {
					LateTickActors();
				}
			}
		}

		public new bool Connect(string address, int port) {
			Assert.IsTrue(_connectionState == EConnectionState.Disconnected);
			if (base.Connect(address, port)) {
				_connectionState = EConnectionState.Connecting;
			}
			return _connectionState == EConnectionState.Connecting;
		}

		public override void OnConnect(NetDriverConnection connection) {
			Assert.IsNull(_serverConnection);
			_serverConnection = new ActorReplicationChannel(new NetConnection(this, connection));
			_serverConnection.ResetTimeoutForTravel();
			connection.outer = _serverConnection;
			Debug.Log("Connected to server (" + connection.address + ")");
		}

		public override void OnDisconnect(NetDriverConnection connection) {
			if (_connectionState == EConnectionState.Connecting) {
				Debug.Log("Cannot connect to server!");
			} else {
				Debug.Log("Disconnected from server (" + connection.address + ")");
				_connectionState = EConnectionState.Disconnected;
				_serverConnection = null;
				OnDisconnectedFromServer(EDisconnectReason.Error);
			}
		}

		protected override void OnDisconnect(ActorReplicationChannel channel, EDisconnectReason reason, string msg) {
			if (string.IsNullOrEmpty(msg)) {
				Debug.LogError("Disconnected by server " + reason);
			} else {
				Debug.LogError("Disconnected by server " + reason + " - " + msg);
			}
			_serverConnection.connection.driverConnection.Dispose();
			_serverConnection = null;
			OnDisconnectedFromServer(reason);
		}

		protected abstract void OnDisconnectedFromServer(EDisconnectReason reason);

		protected override void Dispose(bool disposing) {
			DisconnectFromServer(EDisconnectReason.User);
			base.Dispose(disposing);
		}

		public override void NotifySceneLoaded() {
			base.NotifySceneLoaded();
			if (_serverConnection != null) {
				_serverConnection.connection.SendReliable(NetMsgs.ClientFinishedTravel.New(travelLevel));
			}
		}

		protected override void OnLevelStart() {
			base.OnLevelStart();
			if (_serverConnection != null) {
				_serverConnection.connection.SendReliable(NetMsgs.ClientLevelStarted.New());
			}
			
		}

		protected sealed override void SpawnActorTags() {
			for (int i = 0; i < numActors; ++i) {
				GetActor(i).AttachActorSpawnTag();
            }
		}

		protected override void DestroyActor(Actor actor, bool isTraveling) {
			if (_serverConnection != null) {
				_serverConnection.ActorWasDestroyed(actor, isTraveling);
			}
			base.DestroyActor(actor, isTraveling);
		}

		public void DisconnectFromServer(EDisconnectReason reason) {
			if (_serverConnection != null) {
				_serverConnection.connection.SendReliable(NetMsgs.Disconnect.New(reason, null));
				_serverConnection.connection.driverConnection.Dispose();
				_serverConnection = null;
			}
		}

		public override void OnReliableSendWouldBlock(NetDriverConnection connection) {
			connection.blocking = true;
			OnDisconnect(connection.outer, EDisconnectReason.Error, "WouldBlock");
		}

		public NetConnection serverConnection {
			get {
				return _serverConnection.connection;
			}
		}

		public ActorReplicationChannel serverChannel {
			get {
				return _serverConnection;
			}
		}

		public bool isConnected {
			get {
				return _connectionState == EConnectionState.Connected;
			}
		}

		public override bool isTraveling {
			get {
				return base.isTraveling || _serverTravel;
			}
		}

		public bool isServerTraveling {
			get {
				return _serverTravel;
			}
			protected set {
				_serverTravel = value;
			}
		}

		public bool wasTraveling {
			get {
				return _wasTraveling;
			}
		}
	}
}