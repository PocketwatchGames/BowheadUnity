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
		int _permissionLevel;
#if !(STEAM_API || DEDICATED_SERVER)
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		string _playerName;
#endif
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		ulong _onlineUUID;
		[Replicated(Notify = "OnRep_Loaded")]
		bool _loaded;
		
		public PlayerState() {
			SetReplicates(true);
			SetReplicateRate(1/4f);
		}

		public Team team {
			get {
				return _team;
			}
			protected set {
				_team = value;
			}
		}

		public int permissionLevel {
			get {
				return _permissionLevel;
			}
		}

#if !(STEAM_API || DEDICATED_SERVER)
		public string playerName {
			get {
				return _playerName;
			}
			protected set {
				_playerName = value;
			}
		}
#elif !DEDICATED_SERVER
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

		[RPC(ERPCDomain.Multicast)]
		protected void Multicast_SetPermissionLevel(int level) {
			_permissionLevel = level;
		}

		protected void Multicast_SetiLvl(ushort ilvl, ushort base_ilvl) {}

		protected virtual void OnRep_Loaded() {
			if (!hasAuthority && (hud != null)) {
				hud.OnPlayerLoaded(this);
			}
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
#if !(STEAM_API || DEDICATED_SERVER)
			GetOnlinePlayerInfo();
#else
			if (hud != null) {
				hud.OnPlayerJoinGame(this);
			}
#endif
		}

#if !(STEAM_API || DEDICATED_SERVER)
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
#if !(STEAM_API || DEDICATED_SERVER)
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
		
		public bool loaded {
			get {
				return _loaded;
			}
			protected set {
				_loaded = value;
			}
		}

		public override Type clientType => typeof(PlayerState);
		public override Type serverType => typeof(Server.Actors.ServerPlayerState);
	}
}