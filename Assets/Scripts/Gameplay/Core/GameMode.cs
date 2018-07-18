// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bowhead.Actors;

namespace Bowhead {
	public interface MatchStateEventReceiver {
		void OnMatchWaitingForPlayers();
		void OnMatchCountdown();
		void OnStartUnitTrading();
		void OnMatchStart();
		void OnMatchOvertime();
		void OnMatchComplete();
		void OnMatchFreeze();
		void OnMatchExit();
	}

	public struct GameTime {
		public const int SECONDS_PER_MINUTE = 60;
		public const int SECONDS_PER_HOUR = SECONDS_PER_MINUTE*60;
		public const int SECONDS_PER_DAY = SECONDS_PER_HOUR*24;
		
		public int seconds;
		public int minutes;
		public int hours;
		public int days;

		public bool isNight => hours >= 7;
		public bool isDay => !isNight;

		public float timeOfDay { get { return (float)hours / 24 + (float)minutes / (24 * 60) + (float)seconds / (24 * 60 * 60); } }

		public static GameTime FromSeconds(int seconds) {
			var gt = default(GameTime);

			gt.days = seconds / SECONDS_PER_DAY;
			seconds -= gt.days*SECONDS_PER_DAY;
			gt.hours = seconds / SECONDS_PER_HOUR;
			seconds -= gt.hours*SECONDS_PER_HOUR;
			gt.minutes = seconds / SECONDS_PER_MINUTE;
			gt.seconds = seconds - (gt.minutes*SECONDS_PER_MINUTE);

			return gt;
		}

	};
}

namespace Bowhead.Server {
	using ServerPlayerController = Actors.ServerPlayerController;
	using ServerPlayerState = Actors.ServerPlayerState;

	public abstract class GameMode<T> : GameMode where T : GameState {

		protected GameMode(ServerWorld world) : base(world) { }

		protected override Type gameStateType {
			get {
				return typeof(T);
			}
		}
	}

	public abstract class GameMode : IDisposable {
		const float DELAY_UNTIL_MATCH_FREEZE = 15;
		const float DELAY_UNTIL_EXIT = 20;
		const float SECONDS_IN_24_GAMETIME_HOURS = 10*60;
		const float SECONDS_IN_24_REALTIME_HOURS = 60*60*24;
		const float TIMESCALE = SECONDS_IN_24_REALTIME_HOURS / SECONDS_IN_24_GAMETIME_HOURS;
		const float GAMETIME_START = 8*60*60;

		public enum EMatchState {
			Traveling,
			WaitingForPlayers,
			MatchInProgress,
			MatchOvertime,
			MatchComplete,
			MatchFrozen,
			MatchExit
		}

		ServerWorld _world;
		int _playerNameIndex;
		int _nextTeamNumber;
		EMatchState _matchState;
		GameState _gameState;
		float _timer;
		double _elapsedTime;
		double _elapsedGameTime;
		GameTime _gameTime;
		int _timerAsInt;
		bool _oldOvertimeEnabled;
		bool _endMatch;
		bool _readyForConnect;
		List<ServerPlayerController> _players = new List<ServerPlayerController>();
		List<Actors.ServerTeam> _teams = new List<Actors.ServerTeam>();

		ReadOnlyCollection<Actors.ServerTeam> _roTeams;
		ReadOnlyCollection<ServerPlayerController> _roPlayers;

		protected GameMode(ServerWorld world) {
			_world = world;
			_matchState = EMatchState.Traveling;
			_roTeams = new ReadOnlyCollection<Actors.ServerTeam>(_teams);
			_roPlayers = new ReadOnlyCollection<ServerPlayerController>(_players);
			world.worldStreaming.onChunkLoaded += OnChunkLoaded;
			world.worldStreaming.onChunkUnloaded += OnChunkUnloaded;
			_elapsedGameTime = GAMETIME_START;
		}

		public void Dispose() {
			if (worldStreaming != null) {
				worldStreaming.Dispose();
				worldStreaming = null;
			}
			_world.worldStreaming.onChunkLoaded -= OnChunkLoaded;
			_world.worldStreaming.onChunkUnloaded -= OnChunkUnloaded;
		}

		protected abstract void OnChunkLoaded(World.Streaming.IChunk chunk);
		protected abstract void OnChunkUnloaded(World.Streaming.IChunk chunk);

		public virtual void Tick(float dt) {

			switch (matchState) {
				case EMatchState.Traveling:
					TickTravelPlayers();
				break;
				case EMatchState.WaitingForPlayers:
					TickTravelPlayers();
					_readyForConnect = true;
					_elapsedTime = 0;
					_elapsedGameTime = GAMETIME_START;
					if (allPlayersLoaded) {
							_timer = matchPlayTime;
							if (matchIsTimed) {
								Debug.Log("Match starting -- lasts for " + _timer + " seconds.");
							} else {
								Debug.Log("Match starting -- no time limit.");
							}
							PrepareForMatchInProgress();
							SetMatchState(EMatchState.MatchInProgress);
					} else if (_readyForConnect && !allPlayersConnected) {
#if BACKEND_SERVER
						if (!GameManager.instance.prewarm) {
							_waitTime += dt;
							if ((GameManager.instance.maxWaitTime > 0) && (_waitTime > GameManager.instance.maxWaitTime)) {
								GameManager.instance.telemetry.WaitedTooLongForPlayers();
							}
						}
#endif
					}
				break;
				case EMatchState.MatchInProgress: goto case EMatchState.MatchOvertime;
				case EMatchState.MatchOvertime:
					_elapsedTime += dt;
					_elapsedGameTime += dt*TIMESCALE;

					if (matchIsTimed) {
						_timer -= dt;
					}

					if (matchIsOverFlag) {
						_timer = delayUntilExit;

						Debug.Log("Match is over and will freeze in " + _timer + " seconds.");
						SetMatchState(EMatchState.MatchComplete);
#if BACKEND_SERVER
						if (!GameManager.instance.prewarm) {
							GameManager.instance.telemetry.GameOver();
						}
#endif
					} else if (matchIsTimed && (_timer <= 0f) && (matchState != EMatchState.MatchOvertime)) {
						Debug.Log("Match is now in overtime.");
						SetMatchState(EMatchState.MatchOvertime);
					}
				break;
				case EMatchState.MatchComplete:
					_timer -= dt;
					if (_timer <= 0f) {
						Debug.Log("Match is frozen and will exit in " + _timer + " seconds.");
						SetMatchState(EMatchState.MatchFrozen);
					}
				break;
				case EMatchState.MatchFrozen:
					_timer -= dt;
					if (_timer <= 0f) {
						_timer = 0f;
						Debug.Log("Match is exiting.");
						SetMatchState(EMatchState.MatchExit);
#if BACKEND_SERVER
						if (!GameManager.instance.prewarm) {
							GameManager.instance.telemetry.ExitGame();
						}
#endif
				}
				break;
			}

			_gameTime = GameTime.FromSeconds(matchTimer);

			if ((_oldOvertimeEnabled != overtimeFlag) || (_timerAsInt != matchTimer)) {
				ReplicateMatchState();
			}
		}

		protected void SetMatchState(EMatchState matchState) {
			if (matchState == _matchState) {
				return;
			}

			_matchState = matchState;
			switch (matchState) {
				case EMatchState.Traveling:
					OnMatchTravel();
				break;
				case EMatchState.WaitingForPlayers:
					OnMatchWaitingForPlayers();
				break;
				case EMatchState.MatchInProgress:
					OnMatchStart();
				break;
				case EMatchState.MatchOvertime:
					OnMatchOvertime();
				break;
				case EMatchState.MatchComplete:
					for (int i = 0; i < _players.Count; ++i) {
						_players[i].playerState.NetFlush();
					}
					foreach (var team in world.GetActorIterator<Team>()) {
						team.NetFlush();
					}
					OnMatchComplete();
				break;
				case EMatchState.MatchFrozen:
					OnMatchFreeze();
				break;
				default:
					OnMatchExit();
				break;
			}

			ReplicateMatchState();
			GameManager.instance.LogMemStat();
		}

		public void EndMatch() {
			if (_matchState < EMatchState.MatchComplete) {
				_endMatch = true;
			}
		}

		bool allPlayersConnected {
			get {
				return true;// GameManager.instance.teamSchedule.allPlayersConnected;
            }
		}

		bool allPlayersLoaded {
			get {
				return true;
			}
		}
		
		void ReplicateTimer() {
			if (_gameState != null) {
				var curTimer = matchTimer;
				if (_timerAsInt != curTimer) {
					ReplicateMatchState();
				}
			}
		}

		void ReplicateMatchState() {
			if (_gameState != null) {
				_oldOvertimeEnabled = overtimeFlag;
				_timerAsInt = matchTimer;
				_gameState.Server_SetMatchState(matchState, overtimeFlag, _timerAsInt);
			}
		}

		protected virtual void OnMatchTravel() { }
		protected virtual void OnMatchWaitingForPlayers() { }
		protected virtual void OnMatchStart() { }
		protected virtual void OnMatchOvertime() { }
		protected virtual void OnMatchComplete() { }
		protected virtual void OnMatchFreeze() { }
		protected virtual void OnMatchExit() { }

        public void ReturnToMenu() {
			GameManager.instance.SetPendingLevel("MainMenu", null);
		}

		public EMatchState matchState {
			get {
				return _matchState;
			}
		}

		// Level has been completely loaded.
		public virtual void OnLevelStart() {
			SetMatchState(EMatchState.WaitingForPlayers);

			//foreach (var goal in world.GetActorIterator<GoalActor>()) {
			//	if (goal.goalActorClass.HasGameMode(GetType())) {
			//		_goals.Add(goal);
			//	} else {
			//		goal.Destroy();
			//	}
			//}
		}

		public virtual void GetTravelActorNetIds(HashSetList<int> travelActorNetIDs) {
			// player controllers travel by default
			foreach (var actor in world.GetActorIterator<ServerPlayerController>()) {
				if (actor.GetType() == playerControllerType) {
					// keep compatible player controllers.
					Assert.IsTrue(actor.replicates);
					travelActorNetIDs.Add(actor.netID);
					actor.GetTravelActorNetIds(travelActorNetIDs);
				} else {
					actor.ownerConnection.owningPlayer = null;
				}
			}
		}

		public virtual void PreTravel() {}

		public virtual void BeginTravel() {

			if (matchState != EMatchState.Traveling) {
				SetMatchState(EMatchState.Traveling);
			} else {
				OnMatchTravel();
			}

			monsterTeam = null;
			_teams.Clear();
			_players.Clear();
			_readyForConnect = false;

		}

		protected abstract WorldStreaming.IWorldStreaming CreateWorldStreaming();

		public virtual void NotifySceneLoaded() {
			_gameState = (GameState)world.Spawn(gameStateType, null, default(SpawnParameters));
			_gameState.ServerSetGameMode(this);
			_gameState.Server_SetMatchState(_matchState, false, 0);

			monsterTeam = world.Spawn<Actors.ServerTeam>(null, default(SpawnParameters));
			monsterTeam.teamNumber = Team.MONSTER_TEAM_NUMBER;

			npcTeam = world.Spawn<Actors.ServerTeam>(null, default(SpawnParameters));
			npcTeam.teamNumber = Team.NPC_TEAM_NUMBER;

			worldStreaming = CreateWorldStreaming();
		}

		public virtual void FinishTravel() {}

		void TickTravelPlayers() {
			if (_gameState != null) {
				foreach (var player in world.GetActorIterator<ServerPlayerController>()) {
					if (!player.ownerConnection.isTraveling && (player.playerState == null)) {
						SpawnPlayer(player.ownerConnection);
					}
				}
			}
		}

		protected virtual bool matchIsOver {
			get {
				return false;
			}
		}

		public bool matchIsOverFlag {
			get {
				if (_endMatch) {
					return true;
				}

				if (isInOvertime) {
					if (!overtimeFlag) {
						return true;
					}
					if (_timer <= -matchOvertime) {
						return true;
					}
				} else if (matchIsTimed && !overtimeFlag) {
					if (_timer <= 0f) {
						return true;
					}
				}

				return matchIsOver;
			}
		}

		public virtual float matchPlayTime {
			get {
				return GameManager.instance.matchTime;
			}
		}

		protected virtual float matchOvertime {
			get {
				return GameManager.instance.matchOvertime;
			}
		}

		public int overtimeEnabled {
			get;
			set;
		}

		protected bool overtimeIfTied {
			get {
				return true;
			}
		}

		public virtual bool matchIsTimed {
			get {
				return false;
			}
		}

		protected bool overtimeFlag {
			get {
				return (overtimeEnabled > 0);
			}
		}

		public virtual float delayUntilMatchFreeze {
			get {
				return DELAY_UNTIL_MATCH_FREEZE;
			}
		}

		public virtual float delayUntilExit {
			get {
				return DELAY_UNTIL_EXIT;
			}
		}

		public int matchTimer {
			get {
				// NOTE: timer goes negative in overtime, so we flip
				// it to count up.
				if (_timer < 0f) {
					return Mathf.Max(Mathf.CeilToInt(matchOvertime + _timer), 0);
				} else {
					return (int)Math.Ceiling(_elapsedGameTime);
				}
			}
		}

		public bool playerCanIssueCommands {
			get {
				return matchInProgress || matchIsComplete;
			}
		}

		public virtual bool isInOvertime {
			get {
				return (matchState == EMatchState.MatchOvertime);
			}
		}

		public bool isWaitingForPlayers {
			get {
				return (matchState == EMatchState.WaitingForPlayers);
			}
		}

		public bool matchHasStarted {
			get {
				return (matchState > EMatchState.WaitingForPlayers);
			}
		}

		public bool matchInProgress {
			get {
				return (matchState == EMatchState.MatchInProgress) ||
					(matchState == EMatchState.MatchOvertime);
			}
		}

		public bool matchIsComplete {
			get {
				return (matchState == EMatchState.MatchComplete);
			}
		}

		public bool matchIsFrozen {
			get {
				return (matchState == EMatchState.MatchFrozen);
			}
		}

		public bool matchHasExited {
			get {
				return (matchState == EMatchState.MatchExit);
			}
		}

		protected virtual Type playerControllerType {
			get {
				return typeof(ServerPlayerController);
			}
		}

		protected virtual Type playerStateType {
			get {
				return typeof(ServerPlayerState);
			}
		}

		protected virtual Type teamType {
			get {
				return typeof(Actors.ServerTeam);
			}
		}

		public WorldStreaming.IWorldStreaming worldStreaming {
			get;
			private set;
		}

		protected abstract Type gameStateType { get; }

		public Server.ServerWorld.EClientConnectResult TickPendingConnection(ActorReplicationChannel channel) {
			if (!(_readyForConnect && channel.clientLevelLoaded)) {
				return Server.ServerWorld.EClientConnectResult.Pending;
			}

			if (!SpawnPlayer(channel)) {
				return Server.ServerWorld.EClientConnectResult.Disconnected;
			}

			return Server.ServerWorld.EClientConnectResult.Connected;
		}

		protected virtual void PrepareForMatchInProgress() {}

		public virtual bool AcceptConnection(ActorReplicationChannel channel) {
			return true;
		}

		ServerPlayerController SpawnPlayerActorForChannel(ActorReplicationChannel channel) {
			if ((channel.owningPlayer == null) || (channel.owningPlayer.GetType() != playerControllerType)) {
				if (channel.owningPlayer != null) {
					channel.owningPlayer.Destroy();
				}
				var playerController = (ServerPlayerController)world.Spawn(playerControllerType, null, default(SpawnParameters));
				channel.owningPlayer = playerController;
				playerController.SetOwningConnection(channel);
			}

			return (ServerPlayerController)channel.owningPlayer;
		}

		ServerPlayerState SpawnPlayerStateActor(ServerPlayerController playerController) {
			return (ServerPlayerState)world.Spawn(playerStateType, null, default(SpawnParameters));
		}

		protected virtual Actors.ServerTeam GetTeamForSpawningPlayer(ServerPlayerController playerController) {
			var teamActor = world.Spawn<Actors.ServerTeam>(null, default(SpawnParameters));
			teamActor.teamNumber = _nextTeamNumber++;
			_teams.Add(teamActor);
			teamActor.NetFlush();
			return teamActor;
		}

		protected virtual void InitPlayerSpawn(ServerPlayerController playerController) {}

		bool SpawnPlayer(ActorReplicationChannel channel) {

			var playerController = SpawnPlayerActorForChannel(channel);
			Assert.IsFalse(_players.Contains(playerController));
			_players.Add(playerController);

			//playerController.inventorySkills = GameManager.instance.teamSchedule.GetPrecachedInventory(channel.uuid);

			var teamActor = GetTeamForSpawningPlayer(playerController);
			
			var playerState = SpawnPlayerStateActor(playerController);
			playerState.team = teamActor;
			playerState.playerController = playerController;
			playerState.onlineUUID = channel.uuid;
#if !(LOGIN_SERVER || BACKEND_SERVER)
			playerState.playerName = "Player" + (++_playerNameIndex);
#endif

			playerState.SetPermissionLevel(0);
			teamActor.AddPlayerToTeam(playerState);
			
			playerController.playerState = playerState;
			InitPlayerSpawn(playerController);
			channel.ResetTimeoutForTravel();
			
			return true;
		}

		public void NotifyPlayerDisconnected(ServerPlayerController playerController, Exception e, EDisconnectReason reason, string msg) {
			_players.Remove(playerController);
			
			playerController.NotifyPlayerDisconnected(matchState >= EMatchState.MatchInProgress);
			playerController.Destroy();
		}

		public virtual int initialTeamScore {
			get {
				return 0;
			}
		}

		public virtual int initialTeamScore2 {
			get {
				return 0;
			}
		}

		public virtual int initialPlayerScore {
			get {
				return 0;
			}
		}

		public virtual int initialPlayerScore2 {
			get {
				return 0;
			}
		}
		
		protected virtual Color monsterTeamColor {
			get {
				return Color.gray;
			}
		}

		protected virtual Color npcTeamColor {
			get {
				return Color.gray;
			}
		}
		
		public ServerWorld world {
			get {
				return _world;
			}
		}
		
		public GameState gameState {
			get {
				return _gameState;
			}
		}

		public ReadOnlyCollection<ServerPlayerController> players {
			get {
				return _roPlayers;
			}
		}

		public ReadOnlyCollection<Actors.ServerTeam> teams {
			get {
				return _roTeams;
			}
		}

		public Actors.ServerTeam monsterTeam {
			get;
			private set;
		}

		public Actors.ServerTeam npcTeam {
			get;
			private set;
		}

		public GameTime gameTime => _gameTime;

	}
}