// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Server {
	public abstract partial class ServerWorld : World {

		public enum EClientConnectResult {
			Pending,
			Connected,
			Disconnected
		}

		ReadOnlyCollection<ActorReplicationChannel> _clientConnections;
		List<ActorReplicationChannel> connectionList = new List<ActorReplicationChannel>();
		bool isNetActive;
		int nextConnectionID;
		string serverName;
		string serverMessage;

		public ServerWorld(
			Streaming sharedStreaming,
			World_ChunkComponent chunkComponent,
			Transform sceneGroup,
			GameObject defaultActorPrefab,
			GetObjectPoolRootDelegate getStaticPoolRoot,
			GetObjectPoolRootDelegate getTransientPoolRoot,
			string serverName,
			string serverMessage,
			System.Reflection.Assembly[] assemblies, 
			NetDriver driver
			) : base(sharedStreaming, chunkComponent, sceneGroup, defaultActorPrefab, getStaticPoolRoot, getTransientPoolRoot, new ServerSerializableObjectFactory(assemblies), driver, new ServerNetMsgFactory(assemblies)) {
			this.serverName = serverName;
			this.serverMessage = serverMessage;
			_clientConnections = new ReadOnlyCollection<ActorReplicationChannel>(connectionList);
        }

		public void Tick(float dt, MonoBehaviour loadingContext, ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			var scaledDt = Mathf.Min(dt*Time.timeScale, 1/3f);
            UpdateTime(scaledDt, dt);
			TickWorldStreaming();

			netDriver.TickServer(Mathf.Min(dt, 1/3f), netMessageBytes, ref reliableMetrics, ref unreliableMetrics);

			if (isTraveling) {
				TickTravel(loadingContext);
				if (!isTraveling) {
					FinishTravel();
					OnLevelStart();
					GC.Collect();
				}
			} else {
				TickActors(loadingContext);
			}
        }

		public virtual void LateUpdate(float dt, ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			if (!isTraveling) {
				LateTickActors();
				ReplicateActors(dt, ref reliableMetrics, ref unreliableMetrics);
			}

			TickPendingConnections(ref reliableMetrics, ref unreliableMetrics);
		}

		void TickPendingConnections(ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			for (int i = 0; i < connectionList.Count;) {
				var channel = connectionList[i];

				channel.connection.driverConnection.GetIOMetrics(ref reliableMetrics, ref unreliableMetrics, true);

				if (channel.didHandshake) {
					if (channel.pendingConnect) {
						channel.pendingConnect = false;
						channel.ResetTimeoutForTravel();

						// tell client to travel
						if (isTraveling) {
							channel.connection.SendReliable(NetMsgs.ClientTravel.New(travelLevel, null));
						} else if (currentLevel != null) {
							channel.connection.SendReliable(NetMsgs.ClientTravel.New(currentLevel, null));
						}
					} else if (channel.owningPlayer == null) {
						var result = TickPendingConnection(channel);

						if (result == EClientConnectResult.Disconnected) {
							continue;
						}
					}
				}

				++i;
			}
		}

		void ReplicateActors(float dt, ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			for (int i = 0; i < connectionList.Count;) {
				var channel = connectionList[i];

				if (channel.owningPlayer != null) {
					channel.ReplicateActors(dt);
				}

				channel.connection.driverConnection.GetIOMetrics(ref reliableMetrics, ref unreliableMetrics, true);

#if UNITY_EDITOR
				if (!channel.didHandshake && (channel.handshakeTime > 10f)) {
#else
				if (channel.timedOut || (!channel.didHandshake && (channel.handshakeTime > 10f))) {
#endif
					DisconnectClient(channel.connection, null, EDisconnectReason.TimedOut, channel.timedOut ? channel.timeSinceLastPong + "s" : null);
				} else {
					++i;
				}				
			}
		}

		protected abstract EClientConnectResult TickPendingConnection(ActorReplicationChannel channel);

		public new bool Listen(int port, int maxConnections) {
			Assert.IsFalse(isNetActive);
			isNetActive = base.Listen(port, maxConnections);
			return isNetActive;
		}

		public override void OnConnect(NetDriverConnection connection) {
			var channel = new ActorReplicationChannel(new NetConnection(this, connection));
			channel.connection.SetID(++nextConnectionID);
			channel.connection.driverConnection.blocking = true;
			connection.outer = channel;
			connectionList.Add(channel);
			
			Debug.Log("Client connected (" + connection.address + ").");

			channel.connection.SendReliable(NetMsgs.Welcome.New(serverName, serverMessage, channel.connection.id));
		}

		public override void OnDisconnect(NetDriverConnection connection) {
			var channel = connectionList.Find(x => (x.connection.driverConnection == connection));
			if ((channel != null) && connectionList.Remove(channel)) {
				if (channel.owningPlayer != null) {
					OnPlayerDisconnected(channel.owningPlayer, null, 0, null);
				}
				Debug.Log("Client  disconnected (" + connection.address + ").");
			}
		}

		public void DisconnectClient(NetConnection connection, Exception e, EDisconnectReason reason, string message) {
			var channel = connectionList.Find(x => (x.connection == connection));
			if ((channel != null) && connectionList.Remove(channel)) {
				connection.SendReliable(NetMsgs.Disconnect.New(reason, message));
				if (channel.owningPlayer != null) {
					OnPlayerDisconnected(channel.owningPlayer, e, reason, message);
				}
				if (string.IsNullOrEmpty(message)) {
					Debug.Log("Client  disconnected (" + connection.address + ") " + reason + ".");
				} else {
					Debug.Log("Client  disconnected (" + connection.address + ") " + reason + " -- '" + message + "'.");
				}
				connection.driverConnection.Dispose();
			}
		}

		public override void OnReliableSendWouldBlock(NetDriverConnection connection) {
			var channel = connectionList.Find(x => (x.connection.driverConnection == connection));
			if (channel != null) {
				connection.blocking = true;
				DisconnectClient(channel.connection, null, EDisconnectReason.Error, "WouldBlock");
			} else {
				connection.Dispose();
			}
		}

		protected override void BeginTravel(string travelLevel, HashSetList<int> travelActorNetIDs) {
			base.BeginTravel(travelLevel, travelActorNetIDs);
						
			// tell clients to travel
			for (int i = 0; i < connectionList.Count; ++i) {
				var channel = connectionList[i];

				if (channel.owningPlayer != null) {
					if ((travelActorNetIDs == null) || !travelActorNetIDs.Contains(channel.owningPlayer.netID)) {
						channel.owningPlayer = null;
					}
				}

				if (channel.didHandshake && !channel.pendingConnect && (channel.clientLevel == currentLevel)) {
					channel.connection.driverConnection.blocking = true;
					channel.connection.SendReliable(NetMsgs.ClientTravel.New(travelLevel, travelActorNetIDs));
					channel.clientLevel = null;
					channel.levelStarted = false;
					channel.isTraveling = true;
				}
			}
		}

		protected override void SpawnActorTags() {
			foreach (var tag in spawnTags.Values) {
				if (tag.type == null) {
					Debug.LogError("ActorSpawnTag cannot load type: " + tag.typeName);
				} else if (tag.staticSpawn) {
					Spawn(tag, null, default(SpawnParameters));
				}
			}
		}

		protected override void DestroyActor(Actor actor, bool isTraveling) {
			if (actor.netID != 0) {
				for (int i = 0; i < connectionList.Count; ++i) {
					var channel = connectionList[i];
					channel.ActorWasDestroyed(actor, isTraveling);
				}
			}
			base.DestroyActor(actor, isTraveling);
		}

		protected override void OnDisconnect(ActorReplicationChannel channel, EDisconnectReason reason, string msg) {
			OnDisconnect(channel.connection.driverConnection);
		}

		public void DisconnectAllClients(EDisconnectReason reason) {
			while (connectionList.Count > 0) {
				DisconnectClient(connectionList[0].connection, null, reason, null);
			}
		}

		protected override void Dispose(bool disposing) {
			DisconnectAllClients(EDisconnectReason.User);
			base.Dispose(disposing);
		}

		public virtual void OnPlayerDisconnected(Actor player, Exception e, EDisconnectReason reason, string msg) {
			player.Destroy();
		}

		internal void NetFlush(Actor actor) {
			Assert.IsTrue(actor.replicates);
			Assert.IsTrue(actor.netID != 0);
			Assert.IsFalse(actor.netTornOff);

			for (int i = 0; i < connectionList.Count; ++i) {
				var channel = connectionList[i];
				channel.NetFlush(actor);
			}
		}

		public ReadOnlyCollection<ActorReplicationChannel> clientConnections {
			get {
				return _clientConnections;
			}
		}
	}
}
