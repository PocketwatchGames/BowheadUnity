// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {
	// Contains the state of the game as replicated to clients.
	using EMatchState = Bowhead.Server.GameMode.EMatchState;

	public enum EUnitAllocateReplicationMode {
		LocalOnly,
		Team,
		Everyone
	}

	public abstract class GameState<T> : GameState where T : GameState<T> {
		public override Type serverType {
			get {
				return typeof(T);
			}
		}

		public override Type clientType {
			get {
				return typeof(T);
			}
		}
	}
	
	public abstract class GameState : Actor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		string _gameModeClass;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _matchPlayTime;

		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_OnMatchStateChanged")]
		byte _matchState;
		
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _numPlayersPerTeam;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _numPlayers;
		
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _team0Size;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _team1Size;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _tier;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _difficulty;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _isTeamMap;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _isFFAMap;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _isCOOPMap;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _isCampaignMap;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _isHordeMap;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _canTradeUnitsAnytime;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		bool _matchIsTimed;

		bool _overtimeEnabled;
		int _matchTimer;
		Client.UI.HUD _hud;

		readonly ActorRPC<byte, bool, int> rpc_Multicast_SetMatchState;

		public GameState() {
			SetReplicates(true);
			SetReplicateRate(1/4f);
			_matchState = 255; // match state change when it's received

			rpc_Multicast_SetMatchState = BindRPC<byte, bool, int>(Multicast_SetMatchState);
		}

		public void ServerSetGameMode(Server.GameMode gameMode) {
			gameModeType = gameMode.GetType();
			_gameModeClass = gameModeType.FullName;
			_numPlayersPerTeam = (byte)gameMode.numPlayersPerTeam;
			_numPlayers = (byte)GameManager.instance.teamSchedule.numPlayers;
			_matchPlayTime = Mathf.FloorToInt(gameMode.matchPlayTime);
			_team0Size = (byte)GameManager.instance.teamSchedule.team0Size;
			_team1Size = (byte)GameManager.instance.teamSchedule.team1Size;
			_isTeamMap = gameMode.isTeamMap;
			_isFFAMap = gameMode.isFFAMap;
			_isCOOPMap = gameMode.isCOOPMap;
			_canTradeUnitsAnytime = gameMode.canTradeUnitsAnytime;
			_matchIsTimed = gameMode.matchIsTimed;
			_tier = (byte)GameManager.instance.tier;
			_difficulty = (byte)GameManager.instance.difficulty;
		}

		public override void Tick() {
			base.Tick();

			if (_hud != null) {
				_hud.Tick(world.deltaTime);
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();

			if (!string.IsNullOrEmpty(_gameModeClass)) {
				gameModeType = Type.GetType(_gameModeClass);
			}

			// clients have a HUD
			if (hudType != null) {
				var constructor = hudType.GetConstructor(new[] { typeof(Client.ClientWorld), typeof(GameState) });
				if (constructor != null) {
					_hud = (Client.UI.HUD)constructor.Invoke(new object [] { (Client.ClientWorld)world, this });
					_hud.Initialize();
				} else {
					throw new System.Exception("HUD class ' " + hudType.FullName + "' does not have a compatible constructor!");
				}
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (_hud != null) {
				_hud.Dispose();
				_hud = null;
			}
		}

		public void Server_SetMatchState(EMatchState matchState, bool overtimeEnabled, int matchTimer) {
			Multicast_SetMatchState((byte)matchState, overtimeEnabled, matchTimer);
			rpc_Multicast_SetMatchState.Invoke((byte)matchState, overtimeEnabled, matchTimer);
		}

		public virtual void OnMatchWaitingForPlayers() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchWaitingForPlayers();
			}
		}

		public virtual void OnMatchCountdown() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchCountdown();
			}
		}

		public virtual void OnStartUnitTrading() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnStartUnitTrading();
			}
		}

		public virtual void OnMatchStart() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchStart();
			}
		}

		public virtual void OnMatchOvertime() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchOvertime();
			}
		}

		public virtual void OnMatchComplete() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchComplete();
			}
		}

		public virtual void OnMatchFreeze() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchFreeze();
			}
		}

		public virtual void OnMatchExit() {
			foreach (var actor in world.GetActorIterator<MatchStateEventReceiver>()) {
				actor.OnMatchExit();
			}
		}

		public virtual void OnOvertimeEnabled() {
			if (_hud != null) {
				_hud.OnOvertimeEnabled();
			}
		}

		public virtual void OnMatchTimer() {
			if (_hud != null) {
				_hud.OnMatchTimer();
			}
		}

		public EMatchState matchState {
			get {
				return (EMatchState)_matchState;
			}
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected void Multicast_SetMatchState(byte matchState, bool overtimeEnabled, int matchTimer) {
			if (_matchState != matchState) {
				_matchState = matchState;
				OnMatchStateChanged();
			}

			if (_overtimeEnabled != overtimeEnabled) {
				_overtimeEnabled = overtimeEnabled;
				OnOvertimeEnabled();
			}

			if (_matchTimer != matchTimer) {
				_matchTimer = matchTimer;
				OnMatchTimer();
			}
		}

		void OnRep_OnMatchStateChanged() {
			OnMatchStateChanged();
		}
		
		void OnMatchStateChanged() {
			if (hasAuthority) {
				Debug.Log("Server GameState matchState = " + matchState.ToString());
			} else {
				Debug.Log("Client GameState matchState = " + matchState.ToString());
			}

			switch (matchState) {
				case EMatchState.WaitingForPlayers:
					OnMatchWaitingForPlayers();
				break;
				case EMatchState.Countdown:
					OnMatchCountdown();
				break;
				case EMatchState.UnitTrading:
					OnStartUnitTrading();
					GC.Collect();
				break;
				case EMatchState.MatchInProgress:
					OnMatchStart();
				break;
				case EMatchState.MatchOvertime:
					OnMatchOvertime();
				break;
				case EMatchState.MatchComplete:
					OnMatchComplete();
				break;
				case EMatchState.MatchFrozen:
					OnMatchFreeze();
				break;
				case EMatchState.MatchExit:
					OnMatchExit();
				break;
			}

			if (_hud != null) {
				_hud.OnMatchStateChanged();
			}

			if (!hasAuthority) {
				GameManager.instance.LogMemStat();
			}
		}
	
		//public static int SetPlayerPreset(World world, PlayerState player, MapDescription.GameModeConfig config, MatchPreset preset, EUnitAllocateReplicationMode replicateMode) {
		//	int points = config.CalculateStartingPoints();

		//	if (preset.units != null) {
		//		for (int i = 0; i < preset.units.Length; ++i) {
		//			var u = preset.units[i];
		//			if (u.unitClass != null) {
		//				SetPlayerAllocatedUnits(world, player, config, u.unitClass, u.count, 0, ref points, replicateMode);
		//			}
		//		}
		//	}

		//	return points;
		//}

		//public static int SetPlayerAllocatedUnits(World world, PlayerState player, MapDescription.GameModeConfig config, UnitClass unitClass, int numAllocated, int delta, ref int points, EUnitAllocateReplicationMode replicateMode) {

		//	if ((config == null) || (unitClass.pointCost < 1)) {
		//		return 0;
		//	}

		//	var limit = config.GetUnitClassLimits(unitClass);

		//	int count = 0;
		//	int order = 0;

		//	foreach (var u in world.GetActorIterator<Unit>()) {
		//		if ((u.originalOwner == player) && ReferenceEquals(u.spawnTag.unitClass, unitClass)) {
		//			if (u.allocated) {
		//				order = Mathf.Max(u.staticSpawnTag.order, order);
		//				++count;
		//			}
		//		}
		//	}

		//	int originalCount = count;

		//	if (delta != 0) {
		//		numAllocated = count + delta;
		//	}

		//	numAllocated = Mathf.Clamp(numAllocated, limit.x, limit.y);

		//	while (count > numAllocated) {
		//		bool found = false;

		//		foreach (var u in world.GetActorIterator<Unit>()) {
		//			if ((u.originalOwner == player) && ReferenceEquals(u.spawnTag.unitClass, unitClass)) {
		//				if (u.staticSpawnTag.order == order) {
		//					if (u.allocated && (u.owner == player)) {
		//						u.allocated = false;
		//						if (!u.hasAuthority) {
		//							u.ApplyVisibility();
		//						}
		//						if (points >= 0) {
		//							points += unitClass.pointCost;
		//						}
		//						--count;
		//					}
		//					--order;
		//					found = true;
		//					break;
		//				}
		//			}
		//		}

		//		if (!found) {
		//			break;
		//		}
		//	}

		//	while (count < numAllocated) {
		//		bool found = false;

		//		if ((points >= 0) && (points < unitClass.pointCost)) {
		//			break;
		//		}

		//		foreach (var u in world.GetActorIterator<Unit>()) {
		//			if ((u.originalOwner == player) && ReferenceEquals(u.spawnTag.unitClass, unitClass)) {
		//				if ((u.staticSpawnTag != null) && (u.staticSpawnTag.order == order)) {
		//					if (!u.allocated && (u.owner == player)) {
		//						u.allocated = true;
		//						if (!u.hasAuthority) {
		//							u.ApplyVisibility();
		//						}
		//						if (points >= 0) {
		//							points -= unitClass.pointCost;
		//						}
		//						++count;
		//					}
		//					++order;
		//					found = true;
		//					break;
		//				}
		//			}
		//		}

		//		if (!found) {
		//			break;
		//		}
		//	}

		//	if ((replicateMode != EUnitAllocateReplicationMode.LocalOnly) && player.hasAuthority && (count != originalCount)) {
		//		foreach (var otherPlayer in world.GetActorIterator<Server.Actors.ServerPlayerController>()) {
		//			if (otherPlayer.playerState != player) {
		//				if (replicateMode == EUnitAllocateReplicationMode.Team) {
		//					if (otherPlayer.team != player.team) {
		//						continue;
		//					}
		//				}

		//				otherPlayer.Owner_SetAllocated(player, unitClass, count);
		//			}
		//		}
		//	}

		//	return count;
		//}

		public static void ReplicateTeamState(World world, Server.Actors.ServerPlayerController player) {
			//if (!player.hasAuthority) {
			//	return;
			//}

			//int numTeamUnits = 0;

			//foreach (var otherPlayer in world.GetActorIterator<Server.Actors.ServerPlayerController>()) {
			//	if ((otherPlayer.playerState != null) && (otherPlayer.team == player.team)) {
			//		numTeamUnits += otherPlayer.unitsControlledByPlayer.Count;
			//	}
			//}

			//player.ReplicateSoulStonePointsToPlayer(player);

			//foreach (var otherPlayer in world.GetActorIterator<Server.Actors.ServerPlayerController>()) {
			//	if ((otherPlayer.playerState != null) && (otherPlayer.team == player.team)) {

			//		otherPlayer.Owner_SetNumTeamUnits(numTeamUnits);
					
			//		if (otherPlayer != player) {
			//			otherPlayer.ReplicateSoulStonePointsToPlayer(player);
			//			player.Owner_SetSpells(otherPlayer.playerState, otherPlayer.playerState.primaryDeity, otherPlayer.playerState.secondaryDeity, otherPlayer.playerState.relic, (ushort)otherPlayer.playerState.reliciLvl, otherPlayer.playerState.potion, (ushort)otherPlayer.playerState.potioniLvl, otherPlayer.playerState.primarySpells, otherPlayer.playerState.secondarySpells);

			//			if (config.limits != null) {
			//				for (int i = 0; i < config.limits.Length; ++i) {
			//					var uc = config.limits[i].unitClass;
			//					if (uc != null) {
			//						int count = GetAllocatedUnitCount(world, otherPlayer.playerState, uc);
			//						player.Owner_SetAllocated(otherPlayer.playerState, uc, count);
			//					}
			//				}
			//			}
			//		}
			//	}
			//}
		}
		
		public bool playerCanIssueCommands {
			get {
				return matchInProgress || matchIsComplete;
			}
		}

		public bool playerCanMoveCamera {
			get {
				return true;
			}
		}

		public virtual bool overtimeEnabled {
			get {
				return _overtimeEnabled;
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
				return (matchState > EMatchState.Countdown);
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

		public virtual bool liftFogOfWarAtEndOfMatch {
			get {
				return true;
			}
		}

		public bool canTradeUnitsAnytime {
			get {
				return _canTradeUnitsAnytime;
			}
		}

		public int matchTimer {
			get {
				return _matchTimer;
			}
		}

		public int numPlayersPerTeam {
			get {
				return _numPlayersPerTeam;
			}
		}

		public bool isFFAMap {
			get {
				return _isFFAMap;
			}
		}

		public bool isTeamMap {
			get {
				return _isTeamMap;
			}
		}

		public bool isCOOPMap {
			get {
				return _isCOOPMap;
			}
		}

		public bool isMPMap {
			get {
				return !_isCOOPMap;
			}
		}

		public bool isCampaignMap {
			get {
				return _isCampaignMap;
			}
		}

		public bool isHordeMap {
			get {
				return _isHordeMap;
			}
		}

		public int numPlayers {
			get {
				return _numPlayers;
			}
		}

		public int team0Size {
			get {
				return _team0Size;
			}
		}

		public int team1Size {
			get {
				return _team1Size;
			}
		}

		public int matchPlayTime {
			get {
				return _matchPlayTime;
			}
		}

		public bool matchIsTimed {
			get {
				return _matchIsTimed;
			}
		}

		public int tier {
			get {
				return _tier;
			}
		}

		public int difficulty {
			get {
				return _difficulty;
			}
		}

		public Client.UI.HUD hud {
			get {
				return _hud;
			}
		}

		public Type hudType {
			get {
				return null;
			}
		}

		//protected virtual Type teamsHudType {
		//	get {
		//		return typeof(Client.UI.TeamsHUD);
		//	}
		//}

		//protected virtual Type ffaHudType {
		//	get {
		//		return typeof(Client.UI.FFAHUD);
		//	}
		//}

		public Type gameModeType {
			get;
			private set;
		}

	}
}
