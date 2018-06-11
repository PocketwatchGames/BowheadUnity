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

	public abstract class GameMode {
		const float DELAY_UNTIL_UNIT_TRADING = 5;
		const float DELAY_UNTIL_MATCH_FREEZE = 15;
		const float DELAY_UNTIL_EXIT = 20;
		const double MAX_GAME_LENGTH = 60*60*6;
		public const int SOULSTONE_POINT_SCALE = 100;

		public enum EMatchState {
			Traveling,
			WaitingForPlayers,
			Countdown,
			UnitTrading,
			MatchInProgress,
			MatchOvertime,
			MatchComplete,
			MatchFrozen,
			MatchExit
		}

		ServerWorld _world;
		TeamStart[] _teamStarts;
		PlayerStart[] _playerStarts;
		int _numPlayersPerTeam;
		int _playerNameIndex;
		EMatchState _matchState;
		GameState _gameState;
		float _timer;
		float _nonReplicatedTimer;
		double _gameTime;
		int _timerAsInt;
		int _tierMiniLvl;
		int _tierMaxiLvl;
		int _tierMinLevel;
		int _tierMaxLevel;
		int _essenceScale;
		int _mobDifficultyLevel;
		bool _oldOvertimeEnabled;
		bool _perfTest;
		bool _endMatch;
		bool _readyForConnect;
		List<ServerPlayerController> _players = new List<ServerPlayerController>();
		List<int> _teamScoreIndices;
		List<int> _teamAliveCount = new List<int>();
		List<Actors.ServerTeam> _teams = new List<Actors.ServerTeam>();

		ReadOnlyCollection<Actors.ServerTeam> _roTeams;
		ReadOnlyCollection<ServerPlayerController> _roPlayers;

#if BACKEND_SERVER
		float _waitTime;
#elif !SHIP
		bool _didInitiLvl;
#endif

		protected GameMode(ServerWorld world) {
			_world = world;
			_matchState = EMatchState.Traveling;
			_roTeams = new ReadOnlyCollection<Actors.ServerTeam>(_teams);
			_roPlayers = new ReadOnlyCollection<ServerPlayerController>(_players);
		}

		public virtual void Tick(float dt) {

			/*
			if ((matchState >= EMatchState.UnitTrading) && (matchState < EMatchState.MatchFrozen)) {
				_gameTime += dt;
				if (_gameTime > MAX_GAME_LENGTH) {
					Debug.LogError("Max game time exceeded, terminating match (assuming idle play)");
					SetMatchState(EMatchState.MatchFrozen);
					_timer = 0;
				}
			}

			switch (matchState) {
				case EMatchState.Traveling:
					TickTravelPlayers();
				break;
				case EMatchState.WaitingForPlayers:
					TickTravelPlayers();
					if (!_readyForConnect && GameManager.instance.teamSchedule.loaded) {
						_readyForConnect = true;
#if BACKEND_SERVER
						InitPlayeriLvls();
						if (!GameManager.instance.prewarm) {
							GameManager.instance.telemetry.ServerReady();
						}
#endif
					}
#if !(SHIP || BACKEND_SERVER)
					if (!_didInitiLvl && allPlayersConnected) {
						bool ready = true;
						for (int i = 0; i < players.Count; ++i) {
							var player = players[i];
							if (!player.inventorySkills.ready) {
								ready = false;
								break;
							}
						}

						if (ready) {
							_didInitiLvl = true;
							InitPlayeriLvls();
						}
					}
#endif
					if (allPlayersLoaded) {
						if (unitTradingTime > 0f) {
							_timer = delayUntilUnitTrading;
							Debug.Log("Unit trading starting in " + _timer + " seconds.");
							PrepareForUnitTrading();
							SetMatchState(EMatchState.Countdown);
						} else {
							_timer = matchPlayTime;
							if (matchIsTimed) {
								Debug.Log("Match starting -- lasts for " + _timer + " seconds.");
							} else {
								Debug.Log("Match starting -- no time limit.");
							}
							PrepareForMatchInProgress();
							SetMatchState(EMatchState.MatchInProgress);
						}
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
				case EMatchState.Countdown:
					_timer -= dt;
					if (_timer <= 0f) {
						_timer = unitTradingTime;
						if (matchIsTimed) {
							Debug.Log("Unit trading started -- lasts for " + _timer + " seconds.");
						} else {
							_timer = 1f;
							Debug.Log("Unit trading started -- no time limit.");
						}
						SetMatchState(EMatchState.UnitTrading);
					}
				break;
				case EMatchState.UnitTrading:
					if (matchIsTimed) {
						_timer -= dt;
					}
					if ((_timer <= 0f) || allPlayersReadyToPlay) {
						_timer = matchPlayTime;
						if (matchIsTimed) {
							Debug.Log("Match starting -- lasts for " + _timer + " seconds.");
						} else {
							_timer = 1f;
							Debug.Log("Match starting -- no time limit.");
						}
						PrepareForMatchInProgress();
						SetMatchState(EMatchState.MatchInProgress);
					}
				break;
				case EMatchState.MatchInProgress: goto case EMatchState.MatchOvertime;
				case EMatchState.MatchOvertime:
					if (matchIsTimed) {
						_timer -= dt;
					}

					InternalScoreMatch(dt);

					if (matchIsOverFlag) {
						if (scoreRemainingTime && matchIsTimed) {
							ScoreRemainingTime(Mathf.Max(_timer, 0f));
						}
						_nonReplicatedTimer = Mathf.Min(delayUntilMatchFreeze, delayUntilExit);
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
					_nonReplicatedTimer -= dt;
					_timer -= dt;
					if (_nonReplicatedTimer <= 0f) {
						Debug.Log("Match is frozen and will exit in " + _timer + " seconds.");
						FreezeUnits();
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
			*/

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
				case EMatchState.Countdown:
					OnMatchCountdown();
				break;
				case EMatchState.UnitTrading:
					OnStartUnitTrading();
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
				return GameManager.instance.teamSchedule.allPlayersConnected;
            }
		}

		bool allPlayersLoaded {
			get {
				if (GameManager.instance.teamSchedule.allPlayersConnected) {
					for (int i = 0; i < _players.Count; ++i) {
						var player = _players[i];
						if (!player.clientHasLoaded || (player.playerState == null)) {
							return false;
						}
					}

					return (_players.Count == GameManager.instance.teamSchedule.numPlayers);
				}
				return false;
			}
		}


		bool allPlayersReadyToPlay {
			get {
				for (int i = 0; i < _players.Count; ++i) {
					var player = _players[i];
					if (!player.readyToPlay) {
						return false;
					}
				}
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
		protected virtual void OnMatchCountdown() { }
		protected virtual void OnStartUnitTrading() { }
		protected virtual void OnMatchStart() { }
		protected virtual void OnMatchOvertime() { }
		protected virtual void OnMatchComplete() { }
		protected virtual void OnMatchFreeze() { }
		protected virtual void OnMatchExit() { }

		public void NotifyDamage(ServerPlayerController instigatingPlayer, Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor, ImmutableActorPropertyInstance property, float damage) {
			if (matchState > EMatchState.MatchOvertime) {
				return;
			}

			if (instigatingPlayer != null) {
				instigatingPlayer.NotifyDamageGiven(instigatingActor, targetPlayer, targetActor, property, damage);
			}

			if (targetPlayer != null) {
				targetPlayer.NotifyDamageReceived(instigatingPlayer, instigatingActor, targetActor, property, damage);
			}

			ScoreDamage(instigatingPlayer, instigatingActor, targetPlayer, targetActor, property, damage);
		}

		public void NotifyKill(ServerPlayerController instigatingPlayer, Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor) {
			if (matchState > EMatchState.MatchOvertime) {
				return;
			}

			/*var uActor = instigatingActor as Unit;
			if ((uActor != null) && !(uActor.dead || uActor.pendingKill)) {
				var tActor = targetActor as ActorWithTeam;
				if (tActor != null) {
					if (!uActor.IsFriendly(tActor)) {
						uActor.ServerIncrementKills();
					}
				}
			}*/

			ScoreKill(instigatingPlayer, instigatingActor, targetPlayer, targetActor);
		}

		//public void GrantTeamXP(Team team, float xp, int actorLevel, MetaGame.MissionObjective objective, UnitClass killed) {
		//	var xpTable = GameManager.instance.staticData.xpTable;

		//	// everyone on team gets credit
		//	for (int i = 0; i < players.Count; ++i) {
		//		var player = players[i];
		//		if (player.team == team) {
		//			if (actorLevel >= mobLevel.mobLevel.x) {
		//				actorLevel = Mathf.Max(1, actorLevel - mobLevel.mobLevel.x + player.mobLevelBasis);
		//			}

		//			var xpScale = xpTable.GetXPScale(actorLevel);
		//			var ixp = Mathf.FloorToInt((xpScale*xp)+0.5f);
		//			if (ixp > 0) {
		//				var xpBasis = xpScale / player.mobXPBasis;
		//				var deityXP = Mathf.FloorToInt(xp*xpBasis);

		//				player.inventorySkills.GrantXP(player.playerState.primaryDeity, player.playerState.secondaryDeity, ixp, deityXP);
		//				player.Owner_XPReward(objective, killed, ixp, deityXP);
		//				player.playerState.xp = player.inventorySkills.xp;
		//			}
		//		}
		//	}
		//}

		protected virtual void ScoreDamage(ServerPlayerController instigatingPlayer, Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor, ImmutableActorPropertyInstance property, float damage) {}
		protected virtual void ScoreKill(ServerPlayerController instigatingPlayer, Actor instigatingActor, ServerPlayerController targetPlayer, DamageableActor targetActor) { }
		//protected virtual void ScorePlayerGoalPoints(ServerPlayerController player, GoalPoints points, float dt) { }
		//protected virtual void ScoreTeamGoalPoints(Actors.ServerTeam team, GoalPoints points, float dt) { }

		void InternalScoreMatch(float dt) {
			if (teamDeadWhenAllPlayersDead) {
				for (int i = 0; i < _players.Count; ++i) {
					var player = _players[i];
					if (player.playerState.health <= 0f) {
						player.playerState.score = 0f;
						player.playerState.score2 = 0f;
					}
				}

				for (int i = 0; i < _teams.Count; ++i) {
					var team = _teams[i];
					var canScore = false;
					for (int k = 0; k < _players.Count; ++k) {
						var player = _players[k];
						if ((player.team == team) && (player.playerState.health > 0f)) {
							canScore = true;
							break;
						}
					}
					if (!canScore) {
						team.score = 0f;
						team.score2 = 0f;
					}
				}
			}

			ScoreGoals(true, dt);
			ScoreMatch(dt);
		}

		protected virtual void ScoreGoals(bool shouldTick, float dt) {
			for (int i = 0; i < _players.Count; ++i) {
				var player = _players[i];
				player.playerState.goalPoints = 0f;
				player.playerState.goalPoints2 = 0f;
			}

			for (int i = 0; i < _teams.Count; ++i) {
				var team = _teams[i];
				team.goalPoints = 0f;
				team.goalPoints2 = 0f;
			}

			//for (int i = _goals.Count-1; i >= 0; --i) {
			//	var g = _goals[i];
			//	if (g.pendingKill || g.dead) {
			//		_goals.RemoveAt(i);
			//	} else {
			//		if (shouldTick) {
			//			g.ServerTickGoal();
			//		}

			//		ScoreGoal(g, dt);
			//	}
			//}
		}

		//protected virtual void ScoreGoal(GoalActor goal, float dt) {
		//	for (int i = 0; i < _players.Count; ++i) {
		//		var player = _players[i];
		//		if (!teamDeadWhenAllPlayersDead || (player.playerState.health > 0f)) {
		//			var points = goal.ServerGetPlayerGoalPoints(player);
		//			ScorePlayerGoalPoints(player, points, dt);
		//		}
		//	}

		//	for (int i = 0; i < _teams.Count; ++i) {
		//		var team = _teams[i];
		//		var canScore = true;
		//		if (teamDeadWhenAllPlayersDead) {
		//			canScore = false;
		//			for (int k = 0; k < _players.Count; ++k) {
		//				var player = _players[k];
		//				if ((player.team == team) && (player.playerState.health > 0f)) {
		//					canScore = true;
		//					break;
		//				}
		//			}
		//		}
		//		if (canScore) {
		//			var points = goal.ServerGetTeamGoalPoints(team);
		//			ScoreTeamGoalPoints(team, points, dt);
		//		}
		//	}
  //      }

		protected virtual void ScoreMatch(float dt) {
			DefaultSortScore();
		}

		void ScoreRemainingTime(float dt) {
			if (dt > 0f) {
				ScoreGoals(false, dt);
				ScoreMatch(dt);
			}
		}

		protected void DefaultSortScore() {
			tied = false;

			if (_teamScoreIndices == null) {
				_teamScoreIndices = new List<int>();
			}

			for (int i = 0; i < _players.Count; ++i) {
				var player = _players[i];
				player.playerState.winner = false;
			}

			for (int i = 0; i < _teams.Count; ++i) {
				var team = _teams[i];
				team.winning = false;

				if (teamDeadWhenAllPlayersDead) {
					bool allDead = true;

					for (int k = 0; k < team.players.Count; ++k) {
						var player = team.players[k];
						if (player.health > 0f) {
							allDead = false;
							break;
						}
					}

					if (allDead) {
						continue;
					}
				}

				_teamScoreIndices.Add(i);
			}

			_teamScoreIndices.Sort((a, b) => _teams[b].intScore.CompareTo(_teams[a].intScore));

			if (_teamScoreIndices.Count > 0) {
				int bestScore = _teams[_teamScoreIndices[0]].intScore;
				int numWinningTeams = 0;

				if ((bestScore > 0) || (_teams.Count < 2)) {
					for (int i = 0; i < _teamScoreIndices.Count; ++i) {
						if (_teams.Count == 1) {
							// COOP case
							// team wins if they are alive
							var team = _teams[_teamScoreIndices[0]];
							bool winning = false;

							for (int k = 0; k < team.players.Count; ++k) {
								var player = team.players[k];
								if (player.health > 0f) {
									winning = true;
									break;
								}
							}

							// COOP win if no mission tracker or mission tracker not-fail
							//if (winning && ((_missionTracker == null) || (_missionTracker.state != MissionTracker.EMissionState.Fail))) {

							//	// all players on team are marked as winning
							//	for (int k = 0; k < team.players.Count; ++k) {
							//		var player = (ServerPlayerState)team.players[k];
							//		player.winner = true;
							//	}

							//	team.winning = true;
							//	++numWinningTeams;
							//}
						} else {
							var team = _teams[_teamScoreIndices[i]];
							if (team.intScore >= bestScore) {

								// all players on team are marked as winning
								for (int k = 0; k < team.players.Count; ++k) {
									var player = (ServerPlayerState)team.players[k];
									player.winner = true;
								}

								team.winning = true;
								++numWinningTeams;
							}
						}
					}
				}

				tied = numWinningTeams > 1;
				_teamScoreIndices.Clear();
			}
		}

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
			_teamStarts = null;
			_playerStarts = null;
			_numPlayersPerTeam = 0;
			_teams.Clear();
			_players.Clear();
			_readyForConnect = false;
			_essenceScale = Mathf.FloorToInt(GameManager.instance.staticData.xpTable.GetEssenceScale(GameManager.instance.tier) * SOULSTONE_POINT_SCALE);

#if BACKEND_SERVER
			_waitTime = 0f;
#elif !SHIP
			_didInitiLvl = false;
#endif

			GameManager.instance.teamSchedule.Reset();
		}

		public virtual void NotifySceneLoaded() {
			{
				var sortedTeamStarts = new List<TeamStart>(GameObject.FindObjectsOfType<TeamStart>());
				sortedTeamStarts.Sort((a, b) => a.teamNumber.CompareTo(b.teamNumber));
				_teamStarts = sortedTeamStarts.ToArray();
			}

			_playerStarts = GameObject.FindObjectsOfType<PlayerStart>();
			//_mapInfo = GameObject.FindObjectOfType<MapInfo>();

			//if (_mapInfo.description != null) {
			//	_config = _mapInfo.description.FindConfig(GetType());
			//}

			//if (_config == null) {
			//	Debug.LogError("No game mode config could be found for this map and game mode");
			//}

			foreach (var playerStart in _playerStarts) {
				if (playerStart.playerTeam != null) {
					playerStart.playerTeam.unassignedPlayerStarts.Add(playerStart);
					playerStart.playerTeam.playerStarts.Add(playerStart);
				}
			}

			if (_teamStarts.Length > 0) {
				_numPlayersPerTeam = _teamStarts[0].unassignedPlayerStarts.Count;
				foreach (var teamStart in _teamStarts) {
					if (teamStart.unassignedPlayerStarts.Count != _numPlayersPerTeam) {
						throw new System.Exception("Unbalanced team starts.");
					}
				}
			}

			var xpTable = GameManager.instance.staticData.xpTable;

			mobLevel = xpTable.GetMOBLevel(GameManager.instance.ilvl);
			_mobDifficultyLevel = xpTable.GetDifficulty(GameManager.instance.difficulty).levelBoost;

			_gameState = (GameState)world.Spawn(gameStateType, null, SpawnParameters.defaultParameters);
			_gameState.ServerSetGameMode(this);
			_gameState.Server_SetMatchState(_matchState, false, 0);

			//if (isCOOPMap && (_config.missions != null)) {
			//	var mission = _config.missions.GetMissionForTier(GameManager.instance.tier);
			//	if ((mission != null) && (mission.objectives != null) && (mission.objectives.Length > 0)) {
			//		_missionTracker = world.Spawn<ServerMissionTracker>(null, SpawnParameters.defaultParameters);
			//		_missionTracker.Init(mission);
			//	}
			//}

			monsterTeam = world.Spawn<Actors.ServerTeam>(null, SpawnParameters.defaultParameters);
			monsterTeam.teamNumber = Team.MONSTER_TEAM_NUMBER;
			monsterTeam.teamColor = monsterTeamColor;

			npcTeam = world.Spawn<Actors.ServerTeam>(null, SpawnParameters.defaultParameters);
			npcTeam.teamNumber = Team.NPC_TEAM_NUMBER;
			npcTeam.teamColor = npcTeamColor;
		}

		public virtual void FinishTravel() {
			//DestroyInvalidUnits();
		}

		public int GetMOBLevel(bool elite) {
			var mobLevel = RandomMOBLevel() + _mobDifficultyLevel;
			if (elite) {
				mobLevel |= XPTable.ELITE_LEVEL_FLAG;
			}
			return mobLevel;
		}

		int RandomMOBLevel() {
			//if (_config == null) {
			//	return 1;
			//}

			//var gm = GameManager.instance;

			//return Mathf.Max(1, GameManager.instance.RandomRange(mobLevel.mobLevel.x, mobLevel.mobLevel.y+1));
			return 1;
		}

		void TickTravelPlayers() {
			if (_teamStarts != null) {
				foreach (var player in world.GetActorIterator<ServerPlayerController>()) {
					if (!player.ownerConnection.isTraveling && (player.playerState == null)) {
						SpawnPlayer(player.ownerConnection);
					}
				}
			}
		}

#if BACKEND_SERVER || !SHIP
		void InitPlayeriLvls() {

			var playerSkills = GameManager.instance.teamSchedule.GetAllPlayerInventorySkills();

			var xpTable = GameManager.instance.staticData.xpTable;

			_tierMaxiLvl = GameManager.instance.ilvl;
			_tierMiniLvl = Mathf.Max(1, _tierMaxiLvl - xpTable.GetTieriLvl(1));

			_tierMaxiLvl += xpTable.maxiLvlOverTier;

			_tierMaxLevel = Mathf.Clamp(_tierMaxiLvl / xpTable.ilvlPerLevel, 1, xpTable.maxLevel);

#if !SHIP
			if (playerSkills != null) {
#endif
				// server has pre-loaded list of all player skills.

				var maxPlayeriLvl = 0;

				for (int i = 0; i < playerSkills.Length; ++i) {
					var skills = playerSkills[i];
					maxPlayeriLvl = Mathf.Max(maxPlayeriLvl, skills.ilvl);
				}

				if (maxPlayeriLvl < _tierMiniLvl) {
					_tierMiniLvl = 1; // no one in party is at this ilvl, no lower bound
				}

				_tierMinLevel = Mathf.Clamp(_tierMiniLvl / xpTable.ilvlPerLevel, 1, xpTable.maxLevel);
#if !SHIP
			} else {
				// use connected players instead.
				// non-shipping editor/non-server path.

				var maxPlayeriLvl = 0;

				for (int i = 0; i < players.Count; ++i) {
					var player = players[i];
					maxPlayeriLvl = Mathf.Max(maxPlayeriLvl, player.inventorySkills.ilvl);
				}

				if (maxPlayeriLvl < _tierMiniLvl) {
					_tierMiniLvl = 1; // no one in party is at this ilvl, no lower bound
				}

				_tierMinLevel = Mathf.Clamp(_tierMiniLvl / xpTable.ilvlPerLevel, 1, xpTable.maxLevel);

				for (int i = 0; i < players.Count; ++i) {
					var player = players[i];
					var ps = player.playerState;
					var ilvl = player.inventorySkills.ilvl;

					ps.xp = player.inventorySkills.xp;
					ps.drop_ilvl = Mathf.Min(ilvl, _tierMaxiLvl);
					ps.min_ilvl = _tierMiniLvl;
					ps.max_ilvl = _tierMaxiLvl;
					ps.level = player.inventorySkills.level;
					ps.scaledLevel = Mathf.Clamp(ps.level, _tierMinLevel, _tierMaxLevel);

					if (ilvl < _tierMiniLvl) {
						var adjustedMOB = xpTable.GetMOBLevel(ilvl);
						player.mobLevelBasis = Mathf.FloorToInt(Mathf.Lerp(adjustedMOB.mobLevel.x, adjustedMOB.mobLevel.y, 0.5f)+0.5f);
						player.mobXPBasis = xpTable.GetXPScale(player.mobLevelBasis);
					} else {
						player.mobLevelBasis = Mathf.FloorToInt(Mathf.Lerp(mobLevel.mobLevel.x, mobLevel.mobLevel.y, 0.5f)+0.5f);
						if (ilvl > _tierMaxiLvl) {
							var adjustedMOB = xpTable.GetMOBLevel(ilvl);
							player.mobXPBasis = xpTable.GetXPScale(adjustedMOB.mobLevel.x);
						} else {
							player.mobXPBasis = xpTable.GetXPScale(mobLevel.mobLevel.x);
						}
					}

					// re-init player unit's levels
					//foreach (var unit in world.GetActorIterator<Unit>()) {
					//	if (unit.spawnTag.owningPlayer != null) {
					//		if (ReferenceEquals(unit.spawnTag.owningPlayer.playerController, player)) {
					//			unit.ServerInitActorLevel(ps.scaledLevel);
					//		}
					//	}
					//}
				}
			}
#endif
		}
#endif

		public MOBLevel mobLevel {
			get;
			private set;
		}

		public int numPlayersPerTeam {
			get {
				return _numPlayersPerTeam;
			}
		}

		public bool isFFAMap {
			get {
				return (_teamStarts.Length > 1) && (numPlayersPerTeam == 1);
			}
		}

		public bool isTeamMap {
			get {
				return (_teamStarts.Length > 1) && (numPlayersPerTeam > 1);
			}
		}

		public virtual bool isCOOPMap {
			get {
				return _teamStarts.Length == 1;
			}
		}

		public bool isMPMap {
			get {
				return !isCOOPMap;
			}
		}

		public int mobSpellLevel {
			get {
				return 1;
				//if (_config == null) {
				//	return 1;
				//}

				//return Mathf.Max(1, mobLevel.spellLevel);
			}
		}

		public float mobSpellPower {
			get {
				return GameManager.instance.staticData.xpTable.GetSpellPower(mobSpellLevel);
			}
		}

		public virtual bool liftFogOfWarAtEndOfMatch {
			get {
				return true;
			}
		}

		protected virtual int minRequiredTeamScore {
			get {
				return int.MinValue;
			}
		}

		protected int numTeamsAlive {
			get {
				// by default the match is over if only one team is alive

				for (int i = 0; i < _teamAliveCount.Count; ++i) {
					_teamAliveCount[i] = 0;
				}

				for (int i = 0; i < _players.Count; ++i) {
					var player = _players[i].playerState;
					if (player.health > 0f) {
						while (player.team.teamNumber >= _teamAliveCount.Count) {
							_teamAliveCount.Add(0);
						}

						++_teamAliveCount[player.team.teamNumber];
					}
				}

				int numAlive = 0;
				for (int i = 0; i < _teamAliveCount.Count; ++i) {
					if ((_teamAliveCount[i] > 0) && (_teams[i].intScore >= minRequiredTeamScore)) {
						++numAlive;
					}
				}

				return numAlive;
			}
		}

		protected virtual bool matchIsOver {
			get {
				//var numAlive = numTeamsAlive;

				//if (GameManager.instance.teamSchedule.numPlayers < 2) {
				//	return numAlive < 1;
				//}

				//if (numAlive < 2) {
				//	return true;
				//}

				//if (matchOverIfAllGoalsAreControlledBySameTeam && (_goals.Count > 0)) {
				//	for (int i = 0; i < _teams.Count; ++i) {
				//		var team = _teams[i];

				//		int numGoals = 0;

				//		for (int k = 0; k < _goals.Count; ++k) {
				//			var g = _goals[k];
				//			if ((g.controllingPlayer != null) && (g.controllingPlayer.team == team)) {
				//				++numGoals;
				//			}
				//		}

				//		if (numGoals == _goals.Count) {
				//			return true;
				//		}
				//	}
				//}

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
				} else if (!overtimeFlag) {
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

		protected virtual float unitTradingTime {
			get {
				return GameManager.instance.tradingTime;
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

		public bool tied {
			get;
			protected set;
		}

		public virtual bool matchIsTimed {
			get {
				return true;
			}
		}

		protected bool overtimeFlag {
			get {
				return (overtimeEnabled > 0) || (tied && overtimeIfTied);
			}
		}

		protected virtual bool matchOverIfAllGoalsAreControlledBySameTeam {
			get {
				return false;
			}
		}

		protected virtual bool teamDeadWhenAllPlayersDead {
			get { // false means that teams can still win with higher scores even if everyone on the team is dead
				return false;
			}
		}

		protected virtual bool scoreRemainingTime {
			get {
				return true;
			}
		}

		public virtual float delayUntilUnitTrading {
			get {
				return DELAY_UNTIL_UNIT_TRADING;
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
					return Mathf.Max(Mathf.CeilToInt(_timer), 0);
				}
			}
		}

		public bool playerCanIssueCommands {
			get {
				return matchInProgress || matchIsComplete;
			}
		}

		public virtual bool playerCanWinIfDead {
			get {
				return true;
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

		public bool isUnitTrading {
			get {
				return (matchState == EMatchState.UnitTrading);
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

		public virtual bool canTradeUnitsAnytime {
			get {
				return false;
			}
		}

		public PlayerStart[] playerStarts {
			get {
				return _playerStarts;
			}
		}

		public TeamStart[] teamStarts {
			get {
				return _teamStarts;
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

		protected abstract Type gameStateType { get; }

		ServerPlayerController SpawnPlayerActorForChannel(ActorReplicationChannel channel) {
			if ((channel.owningPlayer == null) || (channel.owningPlayer.GetType() != playerControllerType)) {
				if (channel.owningPlayer != null) {
					channel.owningPlayer.Destroy();
				}
				var playerController = (ServerPlayerController)world.Spawn(playerControllerType, null, SpawnParameters.defaultParameters);
				channel.owningPlayer = playerController;
				playerController.SetOwningConnection(channel);
			}

			return (ServerPlayerController)channel.owningPlayer;
		}

		ServerPlayerState SpawnPlayerStateActor(ServerPlayerController playerController) {
			return (ServerPlayerState)world.Spawn(playerStateType, null, SpawnParameters.defaultParameters);
		}

		public Server.ServerWorld.EClientConnectResult TickPendingConnection(ActorReplicationChannel channel) {
			if (!(_readyForConnect && channel.clientLevelLoaded)) {
				return Server.ServerWorld.EClientConnectResult.Pending;
			}

			if (!SpawnPlayer(channel)) {
				return Server.ServerWorld.EClientConnectResult.Disconnected;
			}

			return Server.ServerWorld.EClientConnectResult.Connected;
		}

		protected virtual void PrepareForMatchInProgress() {

			//for (int i = 0;i < _players.Count; ++i) {
			//	var player = _players[i];
			//	if (_config != null) {
			//		player.SpendRemainingPoints(_config);
			//	}
			//	player.playerState.killedUnitValue = 0;
								
			//	foreach (var otherPlayer in world.GetActorIterator<ServerPlayerController>()) {
			//		otherPlayer.Owner_SetSpells(player.playerState, player.playerState.primaryDeity, player.playerState.secondaryDeity, player.playerState.relic, (ushort)player.playerState.reliciLvl, player.playerState.potion, (ushort)player.playerState.potioniLvl, player.playerState.primarySpells, player.playerState.secondarySpells);
			//	}

			//	player.ServerSpawnAbilities();
			//}

			//DestroyUnallocatedUnits();

			//if (_missionTracker != null) {
			//	_missionTracker.StartMission();
			//}
		}

		//public bool PlayerSelectedMatchPreset(ServerPlayerController player, MatchPreset preset) {
		//	if (matchState <= EMatchState.UnitTrading) {
		//		player.SetPlayerPreset(_config, preset, Bowhead.Actors.EUnitAllocateReplicationMode.Team);
		//		return true;
		//	}

		//	return false;
		//}

		//public string GenerateUnitName(UnitClass unitClass, bool forNPC) {

		//	if ((unitClass.names.numVariants < 1) || (forNPC && !unitClass.names.npcHasName)) {
		//		return null;
		//	}

		//	List<int> slots;
		//	if (!_availableNames.TryGetValue(unitClass, out slots)) {
		//		slots = new List<int>(unitClass.names.numVariants);
		//		_availableNames[unitClass] = slots;
		//	}

		//	if (slots.Count < 1) {
		//		for (int i = 0; i < unitClass.names.numVariants; ++i) {
		//			slots.Add(i);
		//		}
		//	}

		//	var index = GameManager.instance.RandomRange(0, slots.Count);
		//	var name = slots[index];
		//	slots.RemoveAt(index);

		//	return Utils.GetLocalizedText("UI." + unitClass.name + "Name" + name);
		//}

		//void PrepareForUnitTrading() {

		//	for (int i = _goals.Count-1; i >= 0; --i) {
		//		var g = _goals[i];
		//		if (!(g.pendingKill || g.dead)) {
		//			g.ServerGameStart();
		//		}
		//		if (g.pendingKill || g.dead) {
		//			_goals.RemoveAt(i);
		//		}
		//	}

		//	if (GameManager.instance.serverPerfTest) {
		//		foreach (var u in world.GetActorIterator<Bowhead.Actors.Unit>()) {
		//			if (u.owner == null) {

		//				if (u.spawnTag.owningPlayer.playerTeam.teamActor == null) {
		//					var teamActor = (Actors.ServerTeam)world.Spawn(teamType, null, SpawnParameters.defaultParameters);
		//					teamActor.teamNumber = u.spawnTag.owningPlayer.playerTeam.teamNumber;
		//					teamActor.teamColor = u.spawnTag.owningPlayer.playerTeam.color;
		//					u.spawnTag.owningPlayer.playerTeam.teamActor = teamActor;
		//				}

		//				u.PerfTest_PossesByTeam(u.spawnTag.owningPlayer.playerTeam.teamActor);
		//				u.allocated = true;
		//				u.Multicast_SetAllocated();
		//			}
		//		}
		//	}

		//	InternalScoreMatch(0f);
		//}

		//// Destroys units that aren't valid for this particular game mode.
		//void DestroyInvalidUnits() {
		//	if ((_config != null) && (_config.limits != null) && (_teamStarts != null)) {

		//		foreach (var u in world.GetActorIterator<Unit>()) {
		//			var limits = _config.GetUnitClassLimits(u.spawnTag.unitClass);
		//			if (limits.y < 1) {
		//				u.Destroy();
		//			}
		//		}

		//		for (int i = 0; i < _config.limits.Length; ++i) {
		//			var l = _config.limits[i];

		//			if (l.minMax.y > 0) {

		//				for (int k = 0; k < _teamStarts.Length; ++k) {
		//					var ts = _teamStarts[k];

		//					DestroyInvalidUnits(ts, l.unitClass, l.minMax.y);
		//				}

		//			}
		//		}
		//	}
		//}

		//void DestroyInvalidUnits(TeamStart teamStart, Bowhead.Actors.UnitClass unitClass, int maxUnits) {

		//	List<Unit> unitsToDestroy = new List<Unit>();

		//	for (int i = 0; i < teamStart.playerStarts.Count; ++i) {

		//		var player = teamStart.playerStarts[i];

		//		int count = 0;
		//		int order = 0;

		//		foreach (var u in world.GetActorIterator<Unit>()) {
		//			if (ReferenceEquals(u.spawnTag.unitClass, unitClass)) {
		//				if (player == u.spawnTag.owningPlayer) {
		//					if (u.staticSpawnTag != null) {
		//						order = Mathf.Max(order, u.staticSpawnTag.order);
		//						++count;
		//					}
		//				}
		//			}
		//		}
				
		//		while (count > maxUnits) {
		//			bool found = false;

		//			foreach (var u in world.GetActorIterator<Unit>()) {
		//				if (ReferenceEquals(u.spawnTag.unitClass, unitClass)) {
		//					if (player == u.spawnTag.owningPlayer) {
		//						if (u.staticSpawnTag != null) {
		//							if (u.staticSpawnTag.order == order) {
		//								unitsToDestroy.Add(u);
		//								--count;
		//								--order;
		//								found = true;
		//								break;
		//							}
		//						}
		//					}
		//				}
		//			}

		//			if (!found) {
		//				break;
		//			}
		//		}
		//	}

		//	for (int i = 0; i < unitsToDestroy.Count; ++i) {
		//		var u = unitsToDestroy[i];
		//		u.Destroy();
		//	}
		//}

		//void DestroyUnallocatedUnits() {
		//	foreach (var u in world.GetActorIterator<Unit>()) {
		//		if (u.allocated) {
		//			u.Multicast_SetAllocated();
		//		} else {
		//			u.Destroy();
		//		}
		//	}
		//}

		//void FreezeUnits() {
		//	foreach (var u in world.GetActorIterator<Unit>()) {
		//		u.ServerStopMoving();
		//	}
		//}

		bool SpawnPlayer(ActorReplicationChannel channel) {

			if (matchState >= EMatchState.Countdown) {
				world.DisconnectClient(channel.connection, null, EDisconnectReason.Error, "Error.Networking.MatchStarted");
				return false;
			}

			var playerStart = GameManager.instance.teamSchedule.FindPlayerStart(this, channel);
			if (playerStart == null) {
#if UNITY_EDITOR
				Debug.LogWarning("There is no player start in the map for this player -- Did you forget to put in player starts?");
#endif
				world.DisconnectClient(channel.connection, null, EDisconnectReason.Kicked, "Error.Networking.PlayerNotInSchedule");
				return false;
			}

			var playerController = SpawnPlayerActorForChannel(channel);
			Assert.IsFalse(_players.Contains(playerController));
			_players.Add(playerController);

			playerStart.playerController = playerController;
			playerController.playerStart = playerStart;
			playerController.inventorySkills = GameManager.instance.teamSchedule.GetPrecachedInventory(channel.uuid);

			Actors.ServerTeam teamActor = playerStart.playerTeam.teamActor;

			if (teamActor == null) {
				teamActor = world.Spawn<Actors.ServerTeam>(null, SpawnParameters.defaultParameters);
				teamActor.teamNumber = playerStart.playerTeam.teamNumber;
				teamActor.teamColor = playerStart.playerTeam.color;
				teamActor.score = initialTeamScore;
				teamActor.score2 = initialTeamScore2;
				playerStart.playerTeam.teamActor = teamActor;
				_teams.Add(teamActor);
				teamActor.NetFlush();
			}

			var playerState = SpawnPlayerStateActor(playerController);
			playerState.team = teamActor;
			playerState.teamSlot = playerStart.teamSlot;
			playerState.primaryColor = playerStart.primaryColor;
			playerState.secondaryColor = playerStart.secondaryColor;
			playerState.playerController = playerController;
			//playerState.primaryDeity = _gameState.gameModeConfig.firstDeity;
			//playerState.secondaryDeity = _gameState.gameModeConfig.firstDeity;
			//playerState.relic = _gameState.gameModeConfig.spellLibrary.defaultRelic;
			//playerState.reliciLvl = (playerState.relic != null) ? GameManager.instance.staticData.inventoryItemLibrary.GetAutoGrantSpellBaseiLvl(playerState.relic) : 1;
			//playerState.potion = _gameState.gameModeConfig.spellLibrary.defaultPotion;
			//playerState.potioniLvl = (playerState.potion != null) ? GameManager.instance.staticData.inventoryItemLibrary.GetAutoGrantSpellBaseiLvl(playerState.potion) : 1;

#if BACKEND_SERVER
			{
				var ilvl = playerController.inventorySkills.ilvl;
				playerState.xp = playerController.inventorySkills.xp;
				playerState.drop_ilvl = Mathf.Min(ilvl, _tierMaxiLvl);
				playerState.min_ilvl = _tierMiniLvl;
				playerState.max_ilvl = _tierMaxiLvl;
				playerState.level = playerController.inventorySkills.level;
				playerState.scaledLevel = Mathf.Clamp(playerState.level, _tierMinLevel, _tierMaxLevel);

				var xpTable = GameManager.instance.staticData.xpTable;

				if (ilvl < _tierMiniLvl) {
					var adjustedMOB = xpTable.GetMOBLevel(ilvl);
					playerController.mobLevelBasis = Mathf.FloorToInt(Mathf.Lerp(adjustedMOB.mobLevel.x, adjustedMOB.mobLevel.y, 0.5f)+0.5f);
					playerController.mobXPBasis = xpTable.GetXPScale(playerController.mobLevelBasis);
				} else {
					playerController.mobLevelBasis = Mathf.FloorToInt(Mathf.Lerp(mobLevel.mobLevel.x, mobLevel.mobLevel.y, 0.5f)+0.5f);
					if (ilvl > _tierMaxiLvl) {
						var adjustedMOB = xpTable.GetMOBLevel(ilvl);
						playerController.mobXPBasis = xpTable.GetXPScale(adjustedMOB.mobLevel.x);
					} else {
						playerController.mobXPBasis = xpTable.GetXPScale(mobLevel.mobLevel.x);
					}
				}
			}
#else
			playerState.xp = 0;
			playerState.drop_ilvl = 1;
			playerState.min_ilvl = 1;
			playerState.max_ilvl = 1;
			playerState.level = 1;
			playerState.scaledLevel = 1;
#endif
			playerState.onlineUUID = channel.uuid;
#if !(LOGIN_SERVER || BACKEND_SERVER)
			playerState.playerName = "Player" + (++_playerNameIndex);
#endif
			playerState.score = initialPlayerScore;
			playerState.score2 = initialPlayerScore2;
			playerState.SetPermissionLevel(0);
			teamActor.AddPlayerToTeam(playerState);
			
			playerController.playerState = playerState;
			playerController.SetStartingPositionAndRotation(playerStart.startPoint.position, playerStart.startPoint.rotation.eulerAngles.y);
			playerController.PossessUnits();

			// This needs to call RPCs so the channel has to be flushed.
			// Additionally we need all the units flushed in order to set the
			// allocated flags on the client.
			//
			// The entire world state would be flushed shortly after this anyway
			// so we do it explicitly here so we can replicate the team state.

			channel.Flush();
			playerController.ReplicateTeamState();

			return true;
		}

		public void NotifyPlayerDisconnected(ServerPlayerController playerController, Exception e, EDisconnectReason reason, string msg) {
			_players.Remove(playerController);
			
			playerController.NotifyPlayerDisconnected(matchState >= EMatchState.Countdown);
			if (playerController.playerStart != null) {
				GameManager.instance.teamSchedule.NotifyPlayerDisconnected(playerController.ownerConnection, playerController.playerStart, e, reason, msg);
			}

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

		public int tierMiniLvl {
			get {
				return _tierMiniLvl;
			}
		}
		
		public int tierMaxiLvl {
			get {
				return _tierMaxiLvl;
			}
		}
	}
}