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
		bool _matchIsTimed;

		bool _overtimeEnabled;
		int _matchTimer;
		Client.UI.HUD _hud;
		bool _diposeStreaming;
		WorldStreaming.IWorldStreaming _streaming;
		GameTime _gameTime;
		EnviroSky _sky;

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
			_matchPlayTime = Mathf.FloorToInt(gameMode.matchPlayTime);
			_matchIsTimed = gameMode.matchIsTimed;
			_matchTimer = gameMode.matchTimer;
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

			if (!GameManager.instance.isServer) {
				_streaming = CreateWorldStreaming();
			}
			
			_gameTime = GameTime.FromSeconds(_matchTimer);
		}

		public override void OnLevelStart() {
			base.OnLevelStart();

			_sky = GameObject.FindObjectOfType<EnviroSky>();
			SyncEnviroSkyTime();
		}

		void SyncEnviroSkyTime() {
			_sky.GameTime.Years = 1;
			_sky.GameTime.Days = gameTime.days + 1;
			_sky.GameTime.Hours = gameTime.hours;
			_sky.GameTime.Minutes = gameTime.minutes;
			_sky.GameTime.Seconds = gameTime.seconds;
		}

		protected abstract WorldStreaming.IWorldStreaming CreateWorldStreaming();

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);

			if (_hud != null) {
				_hud.Dispose();
				_hud = null;
			}

			if (_streaming != null) {
				_streaming.Dispose();
				_streaming = null;
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

		[RPC(ERPCDomain.Multicast)]
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
				_gameTime = GameTime.FromSeconds(_matchTimer);
				if (_sky != null) {
					SyncEnviroSkyTime();
				}
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

		public int matchTimer {
			get {
				return _matchTimer;
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

		public Client.UI.HUD hud {
			get {
				return _hud;
			}
		}

		public virtual Type hudType {
			get {
				return null;
			}
		}

		public Type gameModeType {
			get;
			private set;
		}

		public GameTime gameTime => _gameTime;

		public WorldStreaming.IWorldStreaming worldStreaming {
			get;
			private set;
		}
	}
}
