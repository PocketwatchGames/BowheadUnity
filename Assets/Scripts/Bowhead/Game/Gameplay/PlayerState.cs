// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {
	using ClientPlayerController = Bowhead.Client.Actors.ClientPlayerController;
	/**
		A PlayerState actor contains all the information about a player that is
		publicly known to all other players.
	**/
	public class PlayerState : Actor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Team _team;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Color _primaryColor;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Color _secondaryColor;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _permissionLevel;
#if !(LOGIN_SERVER || BACKEND_SERVER)
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		string _playerName;
#endif
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		ulong _onlineUUID;
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		byte _teamSlot;
		[Replicated]
		byte _level;
#if BACKEND_SERVER
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
#else
		[Replicated]
#endif
		byte _scaledLevel;
#if BACKEND_SERVER
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
#else
		[Replicated(Notify = "OnRep_level")]
#endif
		byte _minilvl;
#if BACKEND_SERVER
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
#else
		[Replicated(Notify = "OnRep_level")]
#endif
		byte _maxilvl;
#if BACKEND_SERVER
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
#else
		[Replicated(Notify = "OnRep_xp")]
#endif
		byte _drop_ilvl;
		[Replicated(Notify = "OnRep_Score")]
		int _score;
		[Replicated(Notify = "OnRep_Score")]
		int _score2;
		[Replicated(Notify = "OnRep_xp")]
		int _xp;
		[Replicated(Notify = "OnRep_Health")]
		float _health;
		[Replicated(Notify = "OnRep_Winner")]
		bool _winner;
		[Replicated(Notify = "OnRep_Loaded")]
		bool _loaded;
		
		public PlayerState() {
			SetReplicates(true);
			SetReplicateRate(1/4f);
			_health = 1f;
		}

		public Color primaryColor {
			get {
				return _primaryColor;
			}
			protected set {
				_primaryColor = value;
			}
		}

		public Color secondaryColor {
			get {
				return _secondaryColor;
			}
			protected set {
				_secondaryColor = value;
			}
		}

		public Team team {
			get {
				return _team;
			}
			protected set {
				_team = value;
			}
		}

		public int teamSlot {
			get {
				return _teamSlot;
			}
			protected set {
				_teamSlot = (byte)value;
			}
		}

		public int drop_ilvl {
			get {
				return _drop_ilvl;
			}
			protected set {
				_drop_ilvl = (byte)value;
			}
		}

		public int min_ilvl {
			get {
				return _minilvl;
			}
			protected set {
				_minilvl = (byte)value;
			}
		}

		public int max_ilvl {
			get {
				return _maxilvl;
			}
			protected set {
				_maxilvl = (byte)value;
			}
		}

		public int level {
			get {
				return _level;
			}
			protected set {
				_level = (byte)value;
			}
		}

		public int scaledLevel {
			get {
				return _scaledLevel;
			}
			protected set {
				_scaledLevel = (byte)value;
			}
		}

		public int permissionLevel {
			get {
				return _permissionLevel;
			}
		}

		public float spellPower {
			get {
				return 1f;// GameManager.instance.staticData.xpTable.GetSpellPower(_drop_ilvl);
			}
		}

#if !(LOGIN_SERVER || BACKEND_SERVER)
		public string playerName {
			get {
				return _playerName;
			}
			protected set {
				_playerName = value;
			}
		}
#else
		public string playerName {
			get;
			set;
		}
#endif

		public ulong onlineUUID {
			get {
				return _onlineUUID;
			}
			protected set {
				_onlineUUID = value;
			}
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected void Multicast_SetPermissionLevel(int level) {
			_permissionLevel = level;
		}

		protected void Multicast_SetiLvl(ushort ilvl, ushort base_ilvl) {

		}

		protected virtual void OnRep_Score() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerScoreChanged(this);
			}
		}

		protected virtual void OnRep_Health() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerHealthChanged(this);
			}
		}

		protected virtual void OnRep_Winner() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerWinningChanged();
			}
		}

		protected virtual void OnRep_Loaded() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerLoaded(this);
			}
		}

		protected virtual void OnRep_xp() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerScoreChanged(this);
			}
		}

		protected virtual void OnRep_level() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerScoreChanged(this);
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
#if !DEDICATED_SERVER && LOGIN_SERVER
			GetOnlinePlayerInfo();
#else
			if (hud != null) {
				hud.OnPlayerJoinGame(this);
			}
#endif
		}

#if !DEDICATED_SERVER && LOGIN_SERVER
		void GetOnlinePlayerInfo() {
			var onlineServices = GameManager.instance.onlineServices;
			onlineServices.AsyncGetOnlinePlayer(onlineServices.GetOnlinePlayerID(onlineUUID), AsyncOnlinePlayerCallback);
		}

		void AsyncOnlinePlayerCallback(Online.OnlinePlayer player, Online.EOnlinePlayerChangedFlags whatChanged) {
			playerName = player.name;
			if (playerName.Length > 20) {
				playerName = playerName.Substring(0, 20) + "...";
			}

			if (hud != null) {
				hud.OnPlayerJoinGame(this);
			}
		}
#endif

		protected override void Dispose(bool disposing) {
#if !DEDICATED_SERVER && LOGIN_SERVER
			GameManager.instance.onlineServices.RemovePendingAsyncGetOnlinePlayerCallback(AsyncOnlinePlayerCallback);
#endif
			if (!world.isTraveling && (hud != null)) {
				hud.OnPlayerLeaveGame(this);
			}
			base.Dispose(disposing);
		}

		ClientPlayerController localPlayer {
			get {
				return ClientPlayerController.localPlayer;
			}
		}

		Client.UI.HUD hud {
			get {
				return ((localPlayer != null) && (localPlayer.gameState != null)) ? localPlayer.gameState.hud : null;
			}
		}

		public int score {
			get {
				return _score;
			}
			protected set {
				_score = value;
			}
		}

		public int score2 {
			get {
				return _score2;
			}
			protected set {
				_score2 = value;
			}
		}

		public int xp {
			get {
				return _xp;
			}
			protected set {
				_xp = value;
			}
		}

		public float health {
			get {
				return _health;
			}
			protected set {
				_health = value;
			}
		}

		public bool winner {
			get {
				return _winner;
			}
			protected set {
				_winner = value;
			}
		}

		public bool loaded {
			get {
				return _loaded;
			}
			protected set {
				_loaded = value;
			}
		}

		public override Type clientType {
			get {
				return typeof(PlayerState);
			}
		}

		public override Type serverType {
			get {
				return typeof(Server.Actors.ServerPlayerState);
			}
		}
	}
}