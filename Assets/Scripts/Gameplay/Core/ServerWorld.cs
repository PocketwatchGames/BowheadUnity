// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using NetMsgs;
using System;
using Server;
using Unity.Jobs;

namespace Bowhead.Server {
	public class ServerWorld : global::Server.ServerWorld {

		GameMode _gameMode;
		
		public ServerWorld(
			IGameInstance gameInstance,
			WorldChunkComponent chunkComponent,
			Transform sceneGroup,
			string serverName,
			string serverMessage,
			System.Reflection.Assembly[] assemblies,
			INetDriver driver
		) : base(gameInstance, null, chunkComponent, sceneGroup, GameManager.instance.staticData.defaultActorPrefab, () => GameManager.instance.staticObjectPoolRoot, () => GameManager.instance.transientObjectPoolRoot, serverName, serverMessage, assemblies, driver) {
        }

		protected override void TickActors(MonoBehaviour loadingContext) {
			if (_gameMode != null) {
				_gameMode.Tick(deltaTime);
			}
			base.TickActors(loadingContext);
		}

		public void BeginTravel(string travelLevel, Type gameMode) {
			if ((gameMode == null) && (_gameMode != null)) {
				gameMode = _gameMode.GetType();
			}

			if (_gameMode != null) {
				_gameMode.Dispose();
				_gameMode = null;
			}

			if ((gameMode == null) || !typeof(GameMode).IsAssignableFrom(gameMode) || gameMode.IsAbstract) {
				throw new System.Exception("Invalid game mode class!");
			}

			var constructor = gameMode.GetConstructor(new[] { typeof(ServerWorld) });
			if (constructor == null) {
				throw new System.Exception("Invalid game mode class!");
			}

			_gameMode = (GameMode)constructor.Invoke(new[] { this });
			_gameMode.PreTravel();

			HashSetList<int> travelActorNetIDs = new HashSetList<int>();
			_gameMode.GetTravelActorNetIds(travelActorNetIDs);
			
			base.BeginTravel(travelLevel, travelActorNetIDs);

			if (GameManager.instance.clientWorld != null) {
				GameManager.instance.clientWorld.InternalServer_BeginTravel(travelLevel, travelActorNetIDs);
			}

			_gameMode.BeginTravel();

		}

		public override void NotifySceneLoaded() {
			_gameMode.NotifySceneLoaded();
			base.NotifySceneLoaded();
		}

		protected override void FinishTravel() {
			base.FinishTravel();
			_gameMode.FinishTravel();
		}

		protected override void OnLevelStart() {
			_gameMode.OnLevelStart();
			base.OnLevelStart();
			Debug.Log("Server -- level start.");
			GameManager.instance.LogMemStat();
		}

		protected override void Dispose(bool disposing) {
			if (_gameMode != null) {
				_gameMode.Dispose();
				_gameMode = null;
			}
			base.Dispose(disposing);
		}

		public override void OnConnect(NetDriverConnection connection) {
			base.OnConnect(connection);

			if (gameMode == null) {
				DisconnectClient(connection.outer.connection, null, EDisconnectReason.Error, "Error.Networking.NotReady");
				return;
            }

			gameMode.AcceptConnection(connection.outer);
		}

		protected override EClientConnectResult TickPendingConnection(ActorReplicationChannel channel) {
			if (_gameMode == null) {
				return EClientConnectResult.Pending;
			}
			return _gameMode.TickPendingConnection(channel);
		}

		public override void OnPlayerDisconnected(Actor player, Exception e, EDisconnectReason reason, string msg) {
			if ((_gameMode != null) && (player != null)) {
				_gameMode.NotifyPlayerDisconnected((Actors.ServerPlayerController)player, e, reason, msg);
			}
			base.OnPlayerDisconnected(player, e, reason, msg);
		}

		public override void OnRawNetRecvError(NetDriverConnection connection, Exception e) {
			if (e != null) {
				Debug.LogException(e);

				var msg = "Server Reported Error: ";

				if (e.InnerException != null) {
					msg += e.InnerException.Message + "\n" + e.InnerException.StackTrace + "\n\n" + e.Message + "\n" + e.StackTrace;
				} else {
					msg += e.Message + "\n" + e.StackTrace;
				}

				DisconnectClient(connection.outer.connection, e, EDisconnectReason.Error, msg);
			} else {
				DisconnectClient(connection.outer.connection, null, EDisconnectReason.Error, null);
            }
		}

		protected override JobHandle CreateGenVoxelsJob(WorldChunkPos_t pos, PinnedChunkData_t chunk) {
			return _gameMode.worldStreaming.ScheduleChunkGenerationJob(pos, chunk);
		}

		protected override Streaming.IMMappedChunkData MMapChunkData(Streaming.IChunk chunk) {
			return _gameMode.worldStreaming.MMapChunkData(chunk);
		}

		protected override void WriteChunkData(Streaming.IChunkIO chunk) {
			_gameMode.worldStreaming.WriteChunkData(chunk);
		}

#if BACKEND_SERVER
		public override void LateUpdate(float dt, ref NetIOMetrics reliableMetrics, ref NetIOMetrics unreliableMetrics) {
			try {
				base.LateUpdate(dt, ref reliableMetrics, ref unreliableMetrics);
			} catch (Exception e) {
				Debug.LogException(e);
				Debug.LogError("Exception occurred in Server::LateUpdate() terminating game server.");
				if (!GameManager.instance.prewarm) {
					GameManager.instance.telemetry.ExitGame();
				}
			}
		}
#endif
		public GameMode gameMode {
			get {
				return _gameMode;
			}
		}
	}
}
