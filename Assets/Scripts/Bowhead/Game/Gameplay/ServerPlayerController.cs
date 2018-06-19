// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System;
using Bowhead.Actors;
using Bowhead.Actors.Spells;

namespace Bowhead.Server.Actors {
	public sealed class ServerPlayerController : PlayerController {
		const int MAX_APS = 14;
		const float MAX_IDLE_TIME = 60*10;

		ServerPlayerState _playerState;
		bool _clientHasLoaded;
		bool _didFlushCommandRate;
		float _lastCFuncTime;
		int _cfuncExecCount;
		double _lastCmdTime;
		double _idleTime;
		int _cmdCount;
		GameMode _gameMode;
		ServerWorld _world;
		float _lastMemCounterTime = -1;
		float _lastFpsCounterTime = -1;
		List<Ability> _abilities = new List<Ability>();

		readonly ActorRPC<PlayerState> rpc_Owner_SetPlayerState;
		readonly ActorRPC<Vector3, float> rpc_Owner_SetStartingPositionAndRotation;
		readonly ActorRPC<int> rpc_Owner_SetNumPossessedUnits;
		readonly ActorRPC<byte, string> rpc_Owner_ConsolePrint;
		readonly ActorRPC<ExplosiveForce> rpc_Owner_Explosion;
		readonly ActorRPC<List<Vector3>, float> rpc_Owner_FormationFeedback;
		readonly ActorRPC<List<Vector3>, List<float>> rpc_Owner_FormationFeedback2;
		readonly ActorRPC<int> rpc_Owner_SetNumTeamUnits;
		readonly ActorRPC<PlayerState, string> rpc_Owner_Say;
		readonly ActorRPC<PlayerState, string> rpc_Owner_SayTeam;
		readonly ActorRPC<string, float> rpc_Owner_HUDDisplaySubtitle;
		readonly ActorRPC rpc_Owner_VO_WaveComplete;
		readonly ActorRPC rpc_Owner_VO_AssassinsSpawned;
		readonly ActorRPC<int, int> rpc_Owner_ServerGrantedItem;

		public ServerPlayerController() {
			rpc_Owner_SetPlayerState = BindRPC<PlayerState>(Owner_SetPlayerState);
			rpc_Owner_SetStartingPositionAndRotation = BindRPC<Vector3, float>(Owner_SetStartingPositionAndRotation);
			rpc_Owner_ConsolePrint = BindRPC<byte, string>(Owner_ConsolePrint);
			rpc_Owner_Explosion = BindRPC<ExplosiveForce>(Owner_Explosion);
			rpc_Owner_Say = BindRPC<PlayerState, string>(Owner_Say);
			rpc_Owner_SayTeam = BindRPC<PlayerState, string>(Owner_SayTeam);
			rpc_Owner_HUDDisplaySubtitle = BindRPC<string, float>(base.Owner_HUDDisplaySubtitle);
			rpc_Owner_VO_WaveComplete = BindRPC(base.Owner_VO_WaveComplete);
			rpc_Owner_VO_AssassinsSpawned = BindRPC(base.Owner_VO_AssassinsSpawned);
			rpc_Owner_ServerGrantedItem = BindRPC<int, int>(base.Owner_ServerGrantedItem);
		}

		public new ServerTeam team {
			get {
				return (_playerState != null) ? _playerState.team : null;
			}
		}

		public MetaGame.PlayerInventorySkills inventorySkills {
			get;
			set;
		}

		public new ServerPlayerState playerState {
			get {
				return _playerState;
			}
			set {
				_playerState = value;
				Owner_SetPlayerState(value);
				rpc_Owner_SetPlayerState.Invoke(value);
			}
		}

		new public void Owner_ServerGrantedItem(int id, int count) {
			rpc_Owner_ServerGrantedItem.Invoke(id, count);
		}

		public void SetStartingPositionAndRotation(Vector3 pos, float rot) {
			Owner_SetStartingPositionAndRotation(pos, rot);
			rpc_Owner_SetStartingPositionAndRotation.Invoke(pos, rot);
		}

		public override void ConsolePrint(LogType logType, string message) {
			if (!string.IsNullOrEmpty(message)) {
				if (isLocalPlayer || (message.Length < 1024)) {
					rpc_Owner_ConsolePrint.Invoke((byte)logType, message);
				}
			}
		}

		public override void BeginTravel() {
			base.BeginTravel();
			_playerState = null;
			_clientHasLoaded = false;
			_gameMode = null;
			_didFlushCommandRate = false;
			base.playerState = null;
			_abilities.Clear();
			ResetIdleTime();
		}

		public override void Tick() {
			base.Tick();

			if (_lastFpsCounterTime > 0f) {
				_lastFpsCounterTime -= world.unscaledDeltaTime;
				if (_lastFpsCounterTime <= 0f) {
					_lastFpsCounterTime = 1f;
					ConsolePrint(LogType.Log, GameManager.instance.fpsStat);
				}
			}

			if (_lastMemCounterTime > 0f) {
				_lastMemCounterTime -= world.unscaledDeltaTime;
				if (_lastMemCounterTime <= 0f) {
					_lastMemCounterTime = 1f;
					ConsolePrint(LogType.Log, GameManager.instance.memStat);
				}
			}

			if (inventorySkills != null) {
				inventorySkills.Tick(world.unscaledDeltaTime);
			}

#if !UNITY_EDITOR
			if (gameMode.matchInProgress && (playerState.health > 0)) {
				_idleTime += world.unscaledDeltaTime;
				if (_idleTime >= MAX_IDLE_TIME) {
					svWorld.DisconnectClient(ownerConnection.connection, null, EDisconnectReason.AFK, null);
				}
			} else {
				_idleTime = 0;
			}
#endif
		}

		void ResetIdleTime() {
			_idleTime = 0;
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			for (int i = 0; i < _abilities.Count; ++i) {
				_abilities[i].Destroy();
			}
			_abilities.Clear();
			if (ownerConnection != null) {
				Assert.IsTrue(ownerConnection.owningPlayer == this);
				ownerConnection.owningPlayer = null;
			}
			if (inventorySkills != null) {
				inventorySkills.Dispose();
				inventorySkills = null;
			}
		}

		public void NotifyPlayerDisconnected(bool midGame) {
			// Be careful not to invoke RPC's on the owning connection since
			// it's been disconnected.
			if (playerState != null) {
				//foreach (var grave in world.GetActorIterator<UnitGravestoneActor>()) {
				//	if (grave.owner == playerState) {
				//		grave.Destroy();
				//	}
				//}
				if (playerState.team != null) {
					playerState.team.RemovePlayerFromTeam(playerState);
				}
				playerState.Destroy();
			}

			/*
			if (midGame) {
				KillAllUnits();
			} else {
				UnpossesAllUnits();
			}*/
		}

		public void NotifyDamageGiven(Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor, ImmutableActorPropertyInstance property, float damage) { }

		public void NotifyDamageReceived(ServerPlayerController instigatingPlayer, Actor instigatingActor, DamageableActor targetActor, ImmutableActorPropertyInstance property, float damage) { }

		public void ScoreTeamDamage(Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor, ImmutableActorPropertyInstance property, float damage) { }

		public void PossessUnits() {
			//playerState.soulStonePoints = gameMode.startingSoulStoneCount * gameMode.GetTeamSoulStonePointScale(team);

			_clientHasLoaded = false;
			playerState.loaded = false;

			//var gear = inventorySkills.ready ? inventorySkills.GetItemStats(playerState.min_ilvl, playerState.max_ilvl) : null;

			//foreach (var unit in world.GetActorIterator<Unit>()) {
			//	if (unit.spawnTag.owningPlayer != null) {
			//		if (ReferenceEquals(unit.spawnTag.owningPlayer.playerController, this)) {
			//			if (isCOOP) {
			//				unit.ServerInitActorLevel(playerState.scaledLevel);
			//				if (gear != null) {
			//					unit.ServerInitActorGear(gear);
			//				}
			//			} else {
			//				unit.ServerInitActorLevel(1);
			//			}
			//			unit.ServerClampPropertyValues();
			//			unit.ServerPossessByPlayer(this);
			//		}
			//	}
			//}

			//Owner_SetNumPossessedUnits(unitsControlledByPlayer.Count);
			//rpc_Owner_SetNumPossessedUnits.Invoke(unitsControlledByPlayer.Count);

			ownerConnection.ResetTimeoutForTravel();
		}

		protected override void Server_PickupItem(ItemPickupActor target) {
			var item = (ItemPickupActorServer)target;
			if ((item != null) && !item.pendingKill) {
				if (!item.mousePickup) {
					throw new Exception("Item is not a mouse pickup item but client is trying to pick it up with an RPC");
				}
				item.ServerPickup(this);
			}
		}
		
		protected override void Server_Say(string text) {
			CheckCommandRate();

			if (text.Length > 256) {
				throw new Exception("Server_Say: Message too long.");
			}

			for (int i = 0; i < gameMode.players.Count; ++i) {
				var player = gameMode.players[i];
				player.rpc_Owner_Say.Invoke(playerState, text);
			}
		}

		protected override void Server_SayTeam(string text) {
			CheckCommandRate();

			if (text.Length > 256) {
				throw new Exception("Server_SayTeam: Message too long.");
			}

			for (int i = 0; i < gameMode.players.Count; ++i) {
				var player = gameMode.players[i];
				if (player.team == team) {
					player.rpc_Owner_SayTeam.Invoke(playerState, text);
				}
			}
		}

		public override void GlobalCooldown(Ability instigator) {
			for (int i = 0; i < _abilities.Count; ++i) {
				var ability = _abilities[i];
				if (ability != instigator) {
					ability.GlobalCooldown();
				}
			}
		}

		public bool HasInventorySpell(AbilityClass spell, int ilvl) {
			return inventorySkills.HasInventorySpell(spell, ilvl);
		}

		public void GlobalAdvanceCooldown(float dt) {
			for (int i = 0; i < _abilities.Count; ++i) {
				var ability = _abilities[i];
				ability.AdvanceCooldown(dt);
			}
		}

		public void ReplicateTeamState() {
			GameState.ReplicateTeamState(world, this);
		}

		public new void Owner_VO_WaveComplete() {
			rpc_Owner_VO_WaveComplete.Invoke();
		}

		public new void Owner_VO_AssassinsSpawned() {
			rpc_Owner_VO_AssassinsSpawned.Invoke();
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void SVFPS() {
			if (_lastFpsCounterTime < 0f) {
				_lastFpsCounterTime = 1f;
			} else {
				_lastFpsCounterTime = -1f;
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void SVMEM() {
			if (_lastMemCounterTime < 0f) {
				_lastMemCounterTime = 1f;
			} else {
				_lastMemCounterTime = -1f;
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void SVGC() {
			Resources.UnloadUnusedAssets();
			System.GC.Collect();
		}

#if PROFILING
		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void SVProfile(params object[] args) {
			if (args.Length == 1) {
				Profiler.logFile = (string)args[0];
				Profiler.enableBinaryLog = true;
				Profiler.enabled = true;
				ConsolePrint(LogType.Warning, "Server profiling on -> " + args[0]);
			} else {
				Profiler.enabled = false;
				ConsolePrint(LogType.Warning, "Server profiling off");
			}
		}
#endif

		protected override void Server_ClientHasLoaded() {
			_clientHasLoaded = true;
			playerState.loaded = true;
			ownerConnection.ResetTimeout();
        }

		protected override void Server_ReadyToPlay(bool ready) {
			playerState.readyToPlay = ready;
		}

		protected override void Server_ExecuteCFunc(string command) {
			if (command.Length > 1024) {
				Debug.LogWarning("Player " + playerState.playerName + " exceeded console command size, kicking player.");
				ErrorDisconnectPlayer(EDisconnectReason.Kicked);
				return;
			}

			var delta = Time.unscaledTime - _lastCFuncTime;
			if (isLocalPlayer || (delta > 1f)) {
				_cfuncExecCount = 0;
				_lastCFuncTime = Time.unscaledTime;
			} else {
				++_cfuncExecCount;
			}

			if (_cfuncExecCount > 5) {
				Debug.LogWarning("Player " + playerState.playerName + " exceeded console command rate, kicking player.");
				ErrorDisconnectPlayer(EDisconnectReason.Kicked);
			} else {
				base.Server_ExecuteCFunc(command);
				string[] args;
				var cfuncMethod = GameManager.instance.ParseConsoleCommand(command, out args);
				if (cfuncMethod != null) {
					if (cfuncMethod.cfunc.IsServer) {
						if (cfuncMethod.cfunc.PermissionLevel < playerState.permissionLevel) {
							ConsolePrint(LogType.Error, "You do not have permission to execute this function. Required permission level: " + cfuncMethod.cfunc.PermissionLevel + ". Your permission level: " + playerState.permissionLevel + ".");
						} else {
							GameManager.instance.InvokeConsoleCommand(cfuncMethod, args, command, this);
						}
					} else {
						Debug.LogWarning("Player " + playerState.playerName + " tried to execute a console command on the server that is not marked as server, kicking player");
						ErrorDisconnectPlayer(EDisconnectReason.Kicked);
					}
				} else {
					Debug.LogWarning("Player " + playerState.playerName + " tried to execute a server command that doesn't exist: " + command + ", kicking player");
					ErrorDisconnectPlayer(EDisconnectReason.Kicked);
				}
			}
		}

		public void ErrorDisconnectPlayer(EDisconnectReason reason) {
			if (!isLocalPlayer && (ownerConnection != null)) {
				(world as ServerWorld).DisconnectClient(ownerConnection.connection, null, reason, null);
			}
		}

		public new void Owner_HUDDisplaySubtitle(string key, float stayTime) {
			rpc_Owner_HUDDisplaySubtitle.Invoke(key, stayTime);
		}

		public void BroadcastConsolePrint(LogType type, string message) {
			BroadcastConsolePrint(type, message, null);
		}

		public void BroadcastConsolePrint(LogType type, string message, PlayerState ignorePlayer) {
			foreach (var player in world.GetActorIterator<PlayerController>()) {
				if (player.playerState != ignorePlayer) {
					player.ConsolePrint(type, message);
				}
			}
		}

		public void ClientRunExplosion(ExplosiveForce explosion) {
			rpc_Owner_Explosion.Invoke(explosion);
		}

		public int didUseSpell {
			get;
			set;
		}

#if !(BACKEND_SERVER || LOGIN_SERVER)
		[CFunc(IsServer = true, Shortcuts = new[] { "ka" })]
		void KillAll() {
			// kill all non-friendly units...
			//foreach (var u in world.GetActorIterator<Unit>()) {
			//	if ((u.team != null) && !u.IsFriendly(team)) {
			//		u.ServerKill(this);
			//	}
			//}
		}

		[CFunc(IsServer = true, Shortcuts = new[] { "sp" })]
		void SetPermissionLevel(int playerID, int level) {
			var player = world.GetObjectByNetID(playerID) as ServerPlayerState;
			if (player != null) {
				if ((player == playerState) || (player.permissionLevel > playerState.permissionLevel)) {
					if ((player == playerState) && (level > playerState.permissionLevel)) {
						ConsolePrint(LogType.Error, "You cannot demote yourself. Your current permission level is " + playerState.permissionLevel + ".");
					} else {
						player.SetPermissionLevel(level);
						ConsolePrint(LogType.Log, "Player " + player.playerName + " permission level changed to " + level + ".");
						if (player != playerState) {
							foreach (var pc in world.GetActorIterator<PlayerController>()) {
								if (pc.playerState == player) {
									pc.ConsolePrint(LogType.Log, playerState.playerName + " changed your permission level to " + level + ".");
									break;
								}
							}
						}
					}
				} else {
					ConsolePrint(LogType.Error, "You cannot change the permission level of a player with equal or greater permissions that your own.");
					ConsolePrint(LogType.Error, "Your permission level is: " + playerState.permissionLevel + ".");
					ConsolePrint(LogType.Error, player.playerName + "'s permission level is: " + player.permissionLevel + ".");
				}
			} else {
				ConsolePrint(LogType.Error, "There is no player with ID " + playerID);
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any, Shortcuts = new[] { "cp" })]
		void ShowCommandPermissionLevel(string command) {
			CFuncMethod cfuncMethod = null;

			if (!string.IsNullOrEmpty(command)) {
				cfuncMethod = GameManager.instance.FindConsoleCommand(command);
			}

			if (cfuncMethod != null) {
				if (cfuncMethod.cfunc.IsServer) {
					ConsolePrint(LogType.Warning, command + " requires permission level " + cfuncMethod.cfunc.PermissionLevel + ".");
				} else {
					ConsolePrint(LogType.Warning, command + " is not subject to any permission level restrictions.");
				}
			} else {
				ConsolePrint(LogType.Error, "There is no command named " + command + ".");
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Admin, Shortcuts = new[] { "scp" })]
		void SetCommandPermissionLevel(string command, int level) {
			CFuncMethod cfuncMethod = null;

			if (!string.IsNullOrEmpty(command)) {
				cfuncMethod = GameManager.instance.FindConsoleCommand(command);
			}

			if (cfuncMethod != null) {
				if (cfuncMethod.cfunc.PermissionLevel == level) {
					ConsolePrint(LogType.Error, command + " is already set to permission level " + level + ".");
				} else if (level < 0) {
					ConsolePrint(LogType.Error, "Permission levels must be >= 0.");
				} else {
					cfuncMethod.cfunc = new CFunc(cfuncMethod.cfunc);
					cfuncMethod.cfunc.PermissionLevel = level;
					ConsolePrint(LogType.Warning, command + " permission level is now " + level + ".");
					BroadcastConsolePrint(LogType.Warning, playerState.playerName + " changed " + command + " command permission level to " + level + ".", playerState);
				}
			} else {
				ConsolePrint(LogType.Error, "There is no command named " + command + ".");
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Officer)]
		void Map(string level, params object[] args) {

			Type gmType;

			if (args.Length > 1) {
				ConsolePrint(LogType.Error, "usage: map [level] {gamemode}");
				return;
			}

			if (args.Length > 0) {
				var s = (string)args[0];
				gmType = Type.GetType(s);
				if (gmType == null) {
					var x = "Bowhead.Server." + s;
					gmType = Type.GetType(x);
					if (gmType == null) {
						ConsolePrint(LogType.Error, "Gamemode '" + s + "' not found.");
						return;
					}
					s = x;
				}

				if (!typeof(GameMode).IsAssignableFrom(gmType)) {
					ConsolePrint(LogType.Error, "'" + s + "' is not a gamemode.");
					return;
				}
			} else {
				gmType = typeof(BowheadGame);
			}

			if (GameManager.instance.travelLevel == null) {
				GameManager.instance.SetPendingLevel(level, gmType);
			} else {
				ConsolePrint(LogType.Error, "Server is currently traveling.");
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Officer, Shortcuts = new[] {"rs"})]
		void Restart() {
			if (GameManager.instance.travelLevel == null) {
				GameManager.instance.SetPendingLevel(svWorld.currentLevel, svWorld.gameMode.GetType());
			} else {
				ConsolePrint(LogType.Error, "Server is currently traveling.");
			}
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Moderator)]
		void MatchTime(params object[] args) {
			if (args.Length == 1) {
				GameManager.instance.matchTime = int.Parse((string)args[0]);
			}
			BroadcastConsolePrint(LogType.Warning, "Match time is " + GameManager.instance.matchTime + " second(s).");
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Moderator)]
		void Overtime(params object[] args) {
			if (args.Length == 1) {
				GameManager.instance.matchOvertime = int.Parse((string)args[0]);
			}
			BroadcastConsolePrint(LogType.Warning, "Overtime time is " + GameManager.instance.matchOvertime + " second(s).");
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Moderator)]
		void NumPlayers(params object[] args) {
			if (args.Length == 1) {
				var numPlayers = int.Parse((string)args[0]);
				if (numPlayers < GameManager.instance.numPlayers) {
					ConsolePrint(LogType.Error, "You cannot decrease the number of players.");
					return;
				}
				GameManager.instance.numPlayers = numPlayers;
			}
			BroadcastConsolePrint(LogType.Warning, "Number of player is " + GameManager.instance.numPlayers);
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any, Shortcuts = new[] { "gm" })]
		void GodMode() {
			godMode = !godMode;
			BroadcastConsolePrint(LogType.Warning, "God mode is " + (godMode ? "on" : "off"));
		}
#endif

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void NumNPCUnits() {
			int num = 0;
			//foreach (var u in world.GetActorIterator<Unit>()) {
			//	if (!(u.pendingKill || u.dead) && u.team.isMonsterTeam) {
			//		++num;
			//	}
			//}
			BroadcastConsolePrint(LogType.Warning, "There are " + num + " npc unit(s) spawned.");
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void NumPlayerUnits() {
			int num = 0;
			//foreach (var u in world.GetActorIterator<Unit>()) {
			//	if (!(u.pendingKill || u.dead) && !u.team.isMonsterTeam) {
			//		++num;
			//	}
			//}
			BroadcastConsolePrint(LogType.Warning, "There are " + num + " player unit(s) spawned.");
		}

		[CFunc(IsServer = true, PermissionLevel = CFunc.Any)]
		void NumUnits() {
			int num = 0;
			//foreach (var u in world.GetActorIterator<Unit>()) {
			//	if (!(u.pendingKill || u.dead)) {
			//		++num;
			//	}
			//}
			BroadcastConsolePrint(LogType.Warning, "There are " + num + " unit(s) spawned.");
		}

		void CheckCommandRate() {
			ResetIdleTime();
			if (_gameMode.matchState < GameMode.EMatchState.MatchInProgress) {
				CheckCommandRate(32); // socket items can flood a bit.
			} else {
				if (!_didFlushCommandRate) {
					_didFlushCommandRate = true;
					_cmdCount = 0;
				}
				CheckCommandRate(MAX_APS);
			}
		}

		void CheckCommandRate(int maxAPS) {
			var dt = GameManager.instance.timeSinceStart - _lastCmdTime;
			if (isLocalPlayer || (dt >= 1f)) {
				_lastCmdTime = GameManager.instance.timeSinceStart;
				_cmdCount = 1;
			} else if (++_cmdCount > maxAPS) {
				throw new Exception("Error: command rate exceeded.");
			}
		}

		public bool godMode {
			get;
			private set;
		}

		public bool clientHasLoaded {
			get {
				return _clientHasLoaded && inventorySkills.ready;
			}
		}

		public bool readyToPlay {
			get {
				return playerState.readyToPlay;
			}
		}
		
		public int mobLevelBasis {
			get;
			set;
		}

		public float mobXPBasis {
			get;
			set;
		}

		public bool isLocalPlayer {
			get {
				return (Bowhead.Client.Actors.ClientPlayerController.localPlayer != null)
					&& (Bowhead.Client.Actors.ClientPlayerController.localPlayer.netID == netID);
			}
		}

		public ServerWorld svWorld {
			get {
				if (_world == null) {
					_world = world as ServerWorld;
				}
				return _world;
			}
		}

		GameMode gameMode {
			get {
				if (_gameMode == null) {
					_gameMode = svWorld.gameMode;
				}
				return _gameMode;
			}
		}
	}
}
