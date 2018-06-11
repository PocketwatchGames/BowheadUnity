﻿// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Steamworks;
using System;

namespace Bowhead.Online.Steam {
	using InventoryItemClass = Bowhead.MetaGame.InventoryItemClass;

	public sealed class SteamPlayerID : OnlinePlayerID {

		public SteamPlayerID(CSteamID steamID) {
			this.steamID = steamID;
			uuid = steamID.m_SteamID;
		}

		public bool Equals(OnlinePlayerID _other) {
			var other = _other as SteamPlayerID;
			return (other != null) && (other.uuid == uuid);
		}

		public CSteamID steamID {
			get;
			private set;
		}

		public ulong uuid {
			get;
			private set;
		}
	}

	public class SteamPlayer : RefCountedObj, OnlinePlayer {

		SteamServices _api;
		SteamPlayerID _id;
		bool _waitingForPersona;
		List<OnlinePlayerChangedDelegate> _changedCallbacks;

		public SteamPlayer(SteamServices api, CSteamID id, string name) {
			_api = api;
			_id = new SteamPlayerID(id);

			if (string.IsNullOrEmpty(name)) {
				UpdateName();
				_waitingForPersona = true;
			} else {
				this.name = name;
				InternalRichPresenceUpdated();
			}

			_api.InternalRegisterPlayer(this);
		}

		void UpdateName() {
			CheckDisposed();

			var name = SteamFriends.GetPlayerNickname(_id.steamID);
			if (string.IsNullOrEmpty(name)) {
				name = SteamFriends.GetFriendPersonaName(_id.steamID);
			}

			if (!string.IsNullOrEmpty(name)) {
				this.name = name;
			}
		}

		internal void InternalPersonaReady() {
			_waitingForPersona = false;
			CheckDisposed();
			UpdateName();
		}

		internal void InternalPersonaChanged(PersonaStateChange_t param) {
			_waitingForPersona = false;

			CheckDisposed();

			var flags = GetChangedFlags(param);
			if ((flags&EOnlinePlayerChangedFlags.Name) != 0) {
				UpdateName();
			}

			if ((flags&EOnlinePlayerChangedFlags.Avatar) != 0) {
				api.GetSmallAvatar(this, false);
				api.GetLargeAvatar(this, false);
			}

			api.InternalPersonaStateChanged(this, flags);
		}

		internal bool InternalRichPresenceUpdated() {
			api.InternalPersonaStateChanged(this, EOnlinePlayerChangedFlags.Presence);
			return true;
		}

		internal bool NeedsRequestRichPresence() {
			var primaryColor = SteamFriends.GetFriendRichPresence(steamID, "primaryColor");
			var secondaryColor = SteamFriends.GetFriendRichPresence(steamID, "secondaryColor");
			return string.IsNullOrEmpty(primaryColor) || string.IsNullOrEmpty(secondaryColor);
		}

		internal bool isUpdatePending {
			get {
				return _waitingForPersona;
			}
		}

		static EOnlinePlayerChangedFlags GetChangedFlags(PersonaStateChange_t param) {
			EOnlinePlayerChangedFlags flags = 0;

			if ((param.m_nChangeFlags&EPersonaChange.k_EPersonaChangeAvatar) != 0) {
				flags |= EOnlinePlayerChangedFlags.Avatar;
			}
			if ((param.m_nChangeFlags&EPersonaChange.k_EPersonaChangeStatus) != 0) {
				flags |= EOnlinePlayerChangedFlags.Status;
			}
			if ((param.m_nChangeFlags&(EPersonaChange.k_EPersonaChangeName|EPersonaChange.k_EPersonaChangeNickname)) != 0) {
				flags |= EOnlinePlayerChangedFlags.Name;
			}
			if ((param.m_nChangeFlags&EPersonaChange.k_EPersonaChangeRelationshipChanged) != 0) {
				var relationship = SteamFriends.GetFriendRelationship(new CSteamID(param.m_ulSteamID));
				switch (relationship) {
					case EFriendRelationship.k_EFriendRelationshipFriend:
						flags |= EOnlinePlayerChangedFlags.FriendAdded;
					break;
					case EFriendRelationship.k_EFriendRelationshipNone:
						flags |= EOnlinePlayerChangedFlags.FriendRemoved;
					break;
				}
			}
			
			return flags;
		}

		protected override void OnDisposed() {
			ReleaseSmallAvatar();
			ReleaseLargeAvatar();
			_changedCallbacks = null;
			_api.InternalUnregisterPlayer(this);
		}

		public string name {
			get;
			set;
		}

		public OnlinePlayerID id {
			get {
				return _id;
			}
		}

		public CSteamID steamID {
			get {
				return _id.steamID;
			}
		}

		public SteamServices api {
			get {
				return _api;
			}
		}

		public Texture2D smallAvatar {
			get;
			set;
		}

		public Texture2D largeAvatar {
			get;
			set;
		}

		public EOnlinePlayerStatus status {
			get {
				var personaState = SteamFriends.GetFriendPersonaState(steamID);
				switch (personaState) {
					default:
						return EOnlinePlayerStatus.Offline;
					case EPersonaState.k_EPersonaStateAway:
						return EOnlinePlayerStatus.Away;
					case EPersonaState.k_EPersonaStateBusy:
						return EOnlinePlayerStatus.Busy;
					case EPersonaState.k_EPersonaStateLookingToPlay:
						return EOnlinePlayerStatus.Online;
					case EPersonaState.k_EPersonaStateLookingToTrade:
						return EOnlinePlayerStatus.Online;
					case EPersonaState.k_EPersonaStateOnline:
						return EOnlinePlayerStatus.Online;
					case EPersonaState.k_EPersonaStateSnooze:
						return EOnlinePlayerStatus.Away;
				}
			}
		}

		public void SendLobbyInvite(uint lobbyID) {
			CheckDisposed();
		}

		public void AsyncGetSmallAvatar() {
			CheckDisposed();
			if (smallAvatar == null) {
				api.GetSmallAvatar(this, true);
			}
		}

		public void AsyncGetLargeAvatar() {
			CheckDisposed();
			if (largeAvatar == null) {
				api.GetLargeAvatar(this, true);
			}
		}

		public void ReleaseSmallAvatar() {
			if (smallAvatar != null) {
				GameObject.Destroy(smallAvatar);
				smallAvatar = null;
			}
		}

		public void ReleaseLargeAvatar() {
			if (largeAvatar != null) {
				GameObject.Destroy(largeAvatar);
				largeAvatar = null;
			}
		}

		public void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) {
			if (_changedCallbacks == null) {
				_changedCallbacks = new List<OnlinePlayerChangedDelegate>();
			}
			if (!_changedCallbacks.Contains(callback)) {
				_changedCallbacks.Add(callback);
			}
		}

		public void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) {
			if (_changedCallbacks != null) {
				_changedCallbacks.Remove(callback);
			}
		}

		internal void InvokePlayerChangedCallbacks(EOnlinePlayerChangedFlags whatChanged) {
			if (_changedCallbacks != null) {
				for (int i = _changedCallbacks.Count - 1; i >= 0; --i) {
					_changedCallbacks[i](this, whatChanged);
				}
			}
		}
	}

	sealed class SteamInventoryContainer : MetaGame.ImmutableInventory {

		public SteamInventoryContainer(SteamItemDetails_t[] items) {
			var library = GameManager.instance.staticData.inventoryItemLibrary;

			List<MetaGame.InventoryItem> iitems = new List<MetaGame.InventoryItem>();

			for (int i = 0; i < items.Length; ++i) {
				var item = items[i];
				InventoryItemClass itemClass;
				int ilvl;

				if (library.TryGetItem(item.m_iDefinition.m_SteamItemDef, out itemClass, out ilvl)) {
					if ((item.m_unQuantity > 0) && (item.m_iDefinition.m_SteamItemDef > 0) && ((((ESteamItemFlags)item.m_unFlags)&ESteamItemFlags.k_ESteamItemRemoved) == 0)) {
						var iitem = new MetaGame.InventoryItem(item.m_itemId.m_SteamItemInstanceID, item.m_iDefinition.m_SteamItemDef, itemClass, ilvl, item.m_unQuantity);
						iitems.Add(iitem);
					}
				}
			}

			this.items = new ReadOnlyCollection<MetaGame.InventoryItem>(iitems);
		}

		public ReadOnlyCollection<MetaGame.InventoryItem> items {
			get;
			private set;
		}
	}

	sealed class SteamDeitySkillsSheet : MetaGame.DeitySkillSheet {
		const string DEITY_SLOT = SteamPlayerSkillSheet.SKILL_SHEET + ".d[{1}]";
		const string DEITY_XP = DEITY_SLOT + ".xp";

		readonly int _skillSheet;
		readonly int _deityIndex;

		public SteamDeitySkillsSheet(int skillSheet, int deityIndex) {
			_skillSheet = skillSheet;
			_deityIndex = deityIndex;

			int value;
			if (SteamUserStats.GetStat(string.Format(DEITY_XP, skillSheet, deityIndex), out value)) {
				xp = value;
			}
		}

		public int xp {
			get;
			private set;
		}

	}

	sealed class SteamPlayerSkillSheet : MetaGame.PlayerSkillSheet {
		public const string SKILL_SHEET = "ss[{0}]";
		public const string SKILL_SHEET_XP = SKILL_SHEET + ".xp";

		public SteamPlayerSkillSheet(int skillSheet) {
			var deities = new List<MetaGame.DeitySkillSheet>();
			this.deities = new ReadOnlyCollection<MetaGame.DeitySkillSheet>(deities);

			for (int i = 0; i < MetaGame.Constants.NUM_DEITIES; ++i) {
				deities.Add(new SteamDeitySkillsSheet(skillSheet, i));
			}

			int value;
			if (SteamUserStats.GetStat(string.Format(SKILL_SHEET_XP, skillSheet), out value)) {
				xp = value;
			}
		}

		public ReadOnlyCollection<MetaGame.DeitySkillSheet> deities {
			get;
			private set;
		}

		public int xp {
			get;
			private set;
		}
	}

	sealed class SteamPlayerSkills : MetaGame.PlayerSkills {

		public static SteamPlayerSkills ReadFromSteam() {
			return new SteamPlayerSkills();
		}

		SteamPlayerSkills() {
			var skills = new List<MetaGame.PlayerSkillSheet>();
			skillSheets = new ReadOnlyCollection<MetaGame.PlayerSkillSheet>(skills);

			for (int i = 0; i < MetaGame.Constants.NUM_SKILL_SHEETS; ++i) {
				skills.Add(new SteamPlayerSkillSheet(i));
			}

			int value = 0;
			if (!SteamUserStats.GetStat("skver", out value)) {
				value = 0;
			}
			skver = value;
		}

		public ReadOnlyCollection<MetaGame.PlayerSkillSheet> skillSheets {
			get;
			private set;
		}

		public int skver {
			get;
			private set;
		}
	}

	public sealed class SteamLocalPlayer : SteamPlayer, OnlineLocalPlayer {
		readonly string _ticket;

		Callback<SteamInventoryResultReady_t> _cbInventoryReady;
		Callback<UserStatsReceived_t> _cbUserStatsReceived;
		List<OnlineLocalPlayerGetInventoryDelegate> _inventoryCallbacks;
		List<OnlineLocalPlayerGetSkillsDelegate> _skillsCallbacks;
		bool _didPromoItems;

		public SteamLocalPlayer(SteamServices api, string authTicket) : base(api, SteamUser.GetSteamID(), SteamFriends.GetPersonaName()) {
			_ticket = authTicket;
			_cbInventoryReady = Callback<SteamInventoryResultReady_t>.Create(OnInventoryResult);
			_cbUserStatsReceived = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
			SteamUserStats.RequestCurrentStats();
		}

		void OnInventoryResult(SteamInventoryResultReady_t param) {

			if (!_didPromoItems) {
				Debug.Log("Steam_OnInventoryResults: promotional items granted, requesting inventory...");
				SteamInventory.DestroyResult(param.m_handle);
				_didPromoItems = true;

				SteamInventoryResult_t result;
				if (!SteamInventory.GetAllItems(out result)) {
					Debug.LogError("Steam_OnInventoryResults: inventory request failed.");
					for (int i = 0; i < _inventoryCallbacks.Count; ++i) {
						_inventoryCallbacks[i](null);
					}
					_inventoryCallbacks.Clear();
				}
				return;
			}

			if ((_inventoryCallbacks != null) && (_inventoryCallbacks.Count > 0)) {
				if (param.m_result == EResult.k_EResultOK) {
					uint numItems = 0;
					if (SteamInventory.GetResultItems(param.m_handle, null, ref numItems)) {
						var items = new SteamItemDetails_t[numItems];
						if (SteamInventory.GetResultItems(param.m_handle, items, ref numItems)) {
							Debug.Log("Steam_OnInventoryResults: inventory successfully retrieved " + numItems + " item(s)." );
							var inventory = new SteamInventoryContainer(items);
							for (int i = 0; i < inventory.items.Count; ++i) {
								var item = inventory.items[i];
								Debug.Log("Steam_OnInventoryResults: (" + i + ") " + item.itemClass.name + " ilvl " + item.ilvl + " iid + " + item.iid + " id " + item.id);
							}
							for (int i = 0; i < _inventoryCallbacks.Count; ++i) {
								_inventoryCallbacks[i](inventory);
							}
							_inventoryCallbacks.Clear();
						}
					}
				} else {
					Debug.LogError("Steam_OnInventoryResults: inventory request failed with code " + param.m_result);
					for (int i = 0; i < _inventoryCallbacks.Count; ++i) {
						_inventoryCallbacks[i](null);
					}
					_inventoryCallbacks.Clear();
				}
			}

			SteamInventory.DestroyResult(param.m_handle);
		}

		public void AsyncGetInventory(OnlineLocalPlayerGetInventoryDelegate callback) {
			if (_inventoryCallbacks == null) {
				_inventoryCallbacks = new List<OnlineLocalPlayerGetInventoryDelegate>();
			}

			bool invoke = _inventoryCallbacks.Count == 0;

			if (!_inventoryCallbacks.Contains(callback)) {
				_inventoryCallbacks.Add(callback);
			}

			if (invoke) {
				SteamInventoryResult_t result;
				if (!_didPromoItems) {
					Debug.Log("Steam_AsyncGetInventory: Granting promotional items...");
					if (!SteamInventory.GrantPromoItems(out result)) {
						_inventoryCallbacks.Clear();
						callback(null);
					}
				} else if (!SteamInventory.GetAllItems(out result)) {
					Debug.Log("Steam_AsyncGetInventory: Requesting inventory...");
					for (int i = 0; i < _inventoryCallbacks.Count; ++i) {
						_inventoryCallbacks[i](null);
					}
					_inventoryCallbacks.Clear();
				}
			}
		}

		public void RemovePendingAsyncGetInventoryCallback(OnlineLocalPlayerGetInventoryDelegate callback) {
			if (_inventoryCallbacks != null) {
				_inventoryCallbacks.RemoveSwapSlow(callback);
			}
		}

		void OnUserStatsReceived(UserStatsReceived_t result) {
			if (result.m_eResult == EResult.k_EResultOK) {
				if ((_skillsCallbacks != null) && (_skillsCallbacks.Count > 0)) {
					var skills = SteamPlayerSkills.ReadFromSteam();
					for (int i = 0; i <_skillsCallbacks.Count; ++i) {
						_skillsCallbacks[i](skills);
					}
					_skillsCallbacks.Clear();
				}
			} else {
				for (int i = 0; i < _skillsCallbacks.Count; ++i) {
					_skillsCallbacks[i](null);
				}
				_skillsCallbacks.Clear();
			}
		}

		public void AsyncGetSkills(OnlineLocalPlayerGetSkillsDelegate callback) {
			if (_skillsCallbacks == null) {
				_skillsCallbacks = new List<OnlineLocalPlayerGetSkillsDelegate>();
			}

			bool invoke = _skillsCallbacks.Count == 0;

			if (!_skillsCallbacks.Contains(callback)) {
				_skillsCallbacks.Add(callback);
			}

			if (invoke) {
				if (!SteamUserStats.RequestCurrentStats()) {
					_skillsCallbacks.Clear();
					callback(null);
				}
			}
		}

		public void RemovePendingAsyncGetSkillsCallback(OnlineLocalPlayerGetSkillsDelegate callback) {
			if (_skillsCallbacks != null) {
				_skillsCallbacks.RemoveSwapSlow(callback);
			}
		}

		public string ticket {
			get {
				return _ticket;
			}
		}
	}
	
	public sealed class SteamServices : OnlineServices {
		public const int MAX_LOBBY_MEMBERS = 8;
		public static readonly AppId_t APP_ID = new AppId_t(346930);

		readonly ReadOnlyCollection<OnlinePlayer> _roFriends;

		Dictionary<ulong, SteamPlayer> _players = new Dictionary<ulong, SteamPlayer>();
		Dictionary<ulong, List<OnlinePlayerChangedDelegate>> _pendingAsyncPlayerGets = new Dictionary<ulong, List<OnlinePlayerChangedDelegate>>();
		List<OnlinePlayer> _friends = new List<OnlinePlayer>();
		HashSet<ulong> _friendSet = new HashSet<ulong>();
		List<OnlinePlayerChangedDelegate> _friendChangedCallbacks = new List<OnlinePlayerChangedDelegate>();
		List<OnlinePlayerChangedDelegate> _changedCallbacks;
		List<OnlineFriendChatDelegate> _friendChatCallbacks = new List<OnlineFriendChatDelegate>();
 
		uint _lobbyID;
		ELobbyType _lobbyType;

		SteamLocalPlayer _localPlayer;

		Callback<GameRichPresenceJoinRequested_t> _cbGameInviteAccepted;
		Callback<PersonaStateChange_t> _cbPersonaStateChange;
		Callback<FriendRichPresenceUpdate_t> _cbRichPresenceUpdate;
		Callback<AvatarImageLoaded_t> _cbAvatarLoaded;
		Callback<GameConnectedFriendChatMsg_t> _cbFriendChatMsg;

		HAuthTicket _hticket;

		public SteamServices() {
			_roFriends = new ReadOnlyCollection<OnlinePlayer>(_friends);
		}
		
		public bool Initialize() {
			if (!SteamAPI.Init()) {
				Debug.LogError("Steam is not running!");
				return false;
			}

			SteamClient.SetWarningMessageHook(WarningHook);

			if (!SteamUser.BLoggedOn()) {
				Debug.LogError("Not signed into steam!");
				return false;
			}

			Debug.Log("Steamworks initialized.");

			return true;
		}

		public void Tick(float dt) {
			SteamAPI.RunCallbacks();
		}

		public void Dispose() {
			if (_hticket != HAuthTicket.Invalid) {
				SteamUser.CancelAuthTicket(_hticket);
			}
			SteamFriends.ClearRichPresence();
			SteamAPI.Shutdown();
		}

		static void WarningHook(int nSeverity, System.Text.StringBuilder pchDebugText) {
			if (nSeverity >= 1) {
				Debug.LogError(pchDebugText.ToString());
			} else {
				Debug.Log(pchDebugText.ToString());
			}
		}

		public void AsyncLogin(OnlinePlayerLoggedInDelegate callback) {
			if (_hticket == HAuthTicket.Invalid) {
				byte[] buff = new byte[1024];
				uint len;

				_hticket = SteamUser.GetAuthSessionTicket(buff, buff.Length, out len);

				string ticket = System.BitConverter.ToString(buff, 0, (int)len);
				ticket = ticket.Replace("-", string.Empty);

				_localPlayer = new SteamLocalPlayer(this, ticket);

				GetFriends(EFriendFlags.k_EFriendFlagImmediate);

#if !DEDICATED_SERVER && LOGIN_SERVER
				var connectLobby = Utils.GetCommandLineArg("+connect_lobby");

				if (connectLobby != null) {
					uint lobbyID;
					if (uint.TryParse(connectLobby, out lobbyID)) {
						if (lobbyID != 0) {
							GameManager.instance.loginServer.pendingLobbyID = lobbyID;
							GameManager.instance.loginServer.pendingLobbyConnect = true;
							GameManager.instance.loginServer.pendingPassword = string.Empty;  // password is set in LoginServerNetMsgDispatch
                            Debug.Log("SteamServices: AsyncLogin passed connect_lobby=" + lobbyID);
						}
					}
				}
#endif
				HookEvents();
				callback(_localPlayer, null);
			}
		}

		public void AsyncGetOnlinePlayer(ulong playerID, OnlinePlayerChangedDelegate callback) {
			var player = CreateSteamPlayer(new CSteamID(playerID));
			
			if (player.name == null) {
				List<OnlinePlayerChangedDelegate> changed;
				if (!_pendingAsyncPlayerGets.TryGetValue(playerID, out changed)) {
					player.AddRef();
					changed = new List<OnlinePlayerChangedDelegate>();
					_pendingAsyncPlayerGets.Add(playerID, changed);
				}
				if (!changed.Contains(callback)) {
					changed.Add(callback);
				}
			} else {
				callback(player, EOnlinePlayerChangedFlags.Name|EOnlinePlayerChangedFlags.Status|EOnlinePlayerChangedFlags.Avatar);
				if (DispatchAsyncPlayerGetCallbacks(player)) {
					player.Dispose();
				}
			}

			player.Dispose();
		}

		public void AsyncGetOnlinePlayer(OnlinePlayerID id, OnlinePlayerChangedDelegate callback) {
			AsyncGetOnlinePlayer(id.uuid, callback);
		}

		public void RemovePendingAsyncGetOnlinePlayerCallback(OnlinePlayerChangedDelegate callback) {
			List<ulong> keysToRemove = new List<ulong>();

			foreach (var pair in _pendingAsyncPlayerGets) {
				pair.Value.RemoveAll(x => x == callback);
				if (pair.Value.Count == 0) {
					var player = GetSteamPlayer(new CSteamID(pair.Key));
					player.Dispose(); // unref for pending callback.
					player.Dispose(); // unref for GetSteamPlayer()
					keysToRemove.Add(pair.Key);
				}
			}

			if (keysToRemove.Count == _pendingAsyncPlayerGets.Count) {
				_pendingAsyncPlayerGets.Clear();
			} else {
				for (int i = 0; i < keysToRemove.Count; ++i) {
					_pendingAsyncPlayerGets.Remove(keysToRemove[i]);
				}
			}
		}

		public void AddFriendChatMessageCallback(OnlineFriendChatDelegate callback) {
			if (!_friendChatCallbacks.Contains(callback)) {
				_friendChatCallbacks.Add(callback);
			}
		}

		public void RemoveFriendChatMessageCallback(OnlineFriendChatDelegate callback) {
			_friendChatCallbacks.Remove(callback);
		}

		public void SetLobbyID(uint lobbyID, ELobbyType type) {
			if ((_lobbyID != lobbyID) || (_lobbyType != type)) {
				_lobbyID = lobbyID;
				_lobbyType = type;
				if ((lobbyID == 0) || (type == ELobbyType.Private)) {
					SteamFriends.SetRichPresence("connect", null);
				} else {
					SteamFriends.SetRichPresence("connect", "+connect_lobby " + _lobbyID);
				}
			}
		}

		internal void SendLobbyInvite(SteamPlayer player, uint lobbyID) {
			SteamFriends.InviteUserToGame(player.steamID, "+connect_lobby " + lobbyID);
		}

		void GetFriends(EFriendFlags flags) {
			int numFriends = SteamFriends.GetFriendCount(flags);
			for (int i = 0; i < numFriends; ++i) {
				var friend = CreateSteamPlayer(SteamFriends.GetFriendByIndex(i, flags));
				_friends.Add(friend);
				_friendSet.Add(friend.id.uuid);
			}
		}

		public void AddFriendshipChangedCallback(OnlinePlayerChangedDelegate callback) {
			if (!_friendChangedCallbacks.Contains(callback)) {
				_friendChangedCallbacks.Add(callback);
			}
		}

		public void RemoveFriendshipChangedCallback(OnlinePlayerChangedDelegate callback) {
			_friendChangedCallbacks.Remove(callback);
		}

		public void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) {
			if (_changedCallbacks == null) {
				_changedCallbacks = new List<OnlinePlayerChangedDelegate>();
			}
			if (!_changedCallbacks.Contains(callback)) {
				_changedCallbacks.Add(callback);
			}
		}

		public void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) {
			if (_changedCallbacks != null) {
				_changedCallbacks.Remove(callback);
			}
		}

		void FriendshipChanged(SteamPlayer player, EOnlinePlayerChangedFlags flags) {
			bool doCallbacks = false;
			bool dispose = false;

			if ((flags & EOnlinePlayerChangedFlags.FriendAdded) != 0) {
				if (!_friends.Contains(player)) {
					player.AddRef();
					_friends.Add(player);
					_friendSet.Add(player.id.uuid);
					doCallbacks = true;
				}
			} else if (_friends.Remove(player)) {
				_friendSet.Remove(player.id.uuid);
				doCallbacks = true;
				dispose = true;
			}

			if (doCallbacks) {
				for (int i = _friendChangedCallbacks.Count - 1; i >= 0; --i) {
					_friendChangedCallbacks[i](player, flags);
				}
			}

			if (dispose) {
				player.Dispose();
			}
		}

		public OnlinePlayerID GetOnlinePlayerID(ulong uuid) {
			return new SteamPlayerID(new CSteamID(uuid));
		}

		public bool IsFriend(OnlinePlayer player) {
			return _friendSet.Contains(player.id.uuid);
		}

		public bool IsFriend(ulong uuid) {
			return _friendSet.Contains(uuid);
		}

		internal SteamPlayer GetSteamPlayer(CSteamID id) {
			SteamPlayer player;
			if (_players.TryGetValue(id.m_SteamID, out player)) {
				player.AddRef();
				return player;
			}
			return null;
		}

		internal SteamPlayer CreateSteamPlayer(CSteamID id) {
			var player = GetSteamPlayer(id);

			if (player == null) {
				player = new SteamPlayer(this, id, null);
				if (!SteamFriends.RequestUserInformation(id, false)) {
					player.InternalPersonaReady();
				}
			}

			return player;
		}

		internal void InternalRegisterPlayer(SteamPlayer player) {
			_players.Add(player.id.uuid, player);
		}

		internal void InternalUnregisterPlayer(SteamPlayer player) {
			if (player == _localPlayer) {
				_localPlayer = null;
			}
			_players.Remove(player.id.uuid);
			_pendingAsyncPlayerGets.Remove(player.id.uuid);
		}

		void HookEvents() {
			_cbGameInviteAccepted = Callback<GameRichPresenceJoinRequested_t>.Create(OnGameInviteAccepted);
			_cbPersonaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChanged);
			_cbRichPresenceUpdate = Callback<FriendRichPresenceUpdate_t>.Create(OnRichPresenceUpdated);
			_cbAvatarLoaded = Callback<AvatarImageLoaded_t>.Create(OnAvatarLoaded);
			_cbFriendChatMsg = Callback<GameConnectedFriendChatMsg_t>.Create(OnFriendChatMsg);
			SteamFriends.SetListenForFriendsMessages(true);
		}

		void OnPersonaStateChanged(PersonaStateChange_t param) {
			var player = GetSteamPlayer(new CSteamID(param.m_ulSteamID));
			
			if (player != null) {
				player.InternalPersonaChanged(param);
			}

			if ((param.m_nChangeFlags&EPersonaChange.k_EPersonaChangeRelationshipChanged) != 0) {
				var steamID = new CSteamID(param.m_ulSteamID);
				var relationship = SteamFriends.GetFriendRelationship(steamID);
				if ((relationship == EFriendRelationship.k_EFriendRelationshipFriend) || (relationship == EFriendRelationship.k_EFriendRelationshipNone)) {
					if (player == null) {
						player = CreateSteamPlayer(steamID);
					}
					if (relationship == EFriendRelationship.k_EFriendRelationshipFriend) {
						FriendshipChanged(player, EOnlinePlayerChangedFlags.FriendAdded);
					} else {
						FriendshipChanged(player, EOnlinePlayerChangedFlags.FriendRemoved);
					}
				}
			}

			if (player != null) {
				player.Dispose();
			}
		}

		void OnFriendChatMsg(GameConnectedFriendChatMsg_t param) {
			if (_friendChatCallbacks.Count == 0) {
				return;
			}

			var player = GetSteamPlayer(param.m_steamIDUser);
			if (player != null) {
				IntPtr pvData = Marshal.AllocHGlobal(256);
				EChatEntryType chatEntryType;
				int getMessage = NativeMethods.ISteamFriends_GetFriendMessage(param.m_steamIDUser, param.m_iMessageID, pvData, 256, out chatEntryType);
				if (chatEntryType == EChatEntryType.k_EChatEntryTypeTyping) {
					// TODO: use this for "user is typing" feature (if appropriate)
				} else if ((chatEntryType == EChatEntryType.k_EChatEntryTypeChatMsg) && (pvData != IntPtr.Zero)) {
					string message = Marshal.PtrToStringAnsi(pvData);
					if (message.Length > 0) {
						for (int i = _friendChatCallbacks.Count - 1; i >= 0; --i) {
							_friendChatCallbacks[i](player.name, (ulong)param.m_steamIDUser, message);
						}
					}
				}
				Marshal.FreeHGlobal(pvData);
				player.Dispose();
			}
		}

		void OnAvatarLoaded(AvatarImageLoaded_t param) {
			var player = GetSteamPlayer(param.m_steamID);
			if (player != null) {
				LoadPlayerAvatar(player, param.m_iImage, param.m_iWide, param.m_iTall, true);
				player.Dispose();
			}
		}

		void LoadPlayerAvatar(SteamPlayer player, int id, int width, int height, bool raiseEvent) {
			if (width == 32) {
				player.ReleaseSmallAvatar();
				player.smallAvatar = GetSteamAvatar(id, width, height);
				if (raiseEvent && (player.smallAvatar != null)) {
					InternalPersonaStateChanged(player, EOnlinePlayerChangedFlags.Avatar);
				}
			} else {
				player.ReleaseLargeAvatar();
				player.largeAvatar = GetSteamAvatar(id, width, height);
				if (raiseEvent && (player.largeAvatar != null)) {
					InternalPersonaStateChanged(player, EOnlinePlayerChangedFlags.Avatar);
				}
			}
		}

		internal void GetLargeAvatar(SteamPlayer player, bool raiseEvent) {
			var index = SteamFriends.GetMediumFriendAvatar(player.steamID);
			if (index < 1) {
				return;
			}
			uint w, h;
			if (SteamUtils.GetImageSize(index, out w, out h)) {
				LoadPlayerAvatar(player, index, (int)w, (int)h, raiseEvent);
			}
		}

		internal void GetSmallAvatar(SteamPlayer player, bool raiseEvent) {
			var index = SteamFriends.GetSmallFriendAvatar(player.steamID);
			if (index < 1) {
				return;
			}
			uint w, h;
			if (SteamUtils.GetImageSize(index, out w, out h)) {
				LoadPlayerAvatar(player, index, (int)w, (int)h, raiseEvent);
			}
		}

		static byte[] _avatarBytes = new byte[184*184*4];

		Texture2D GetSteamAvatar(int id, int width, int height) {
			Texture2D avatar = null;
			var numPixels = width*height;
            var numBytes = numPixels*4;
			if (numBytes <= _avatarBytes.Length) {
				if (SteamUtils.GetImageRGBA(id, _avatarBytes, numBytes)) {
					avatar = new Texture2D(width, height, TextureFormat.ARGB32, true);

					var pixels = new Color32[numPixels];
					int pixOfs = 0;
					for (int y = height-1; y >= 0; --y) {
						var baseOfs = y*width;
						for (int x = 0; x < width; ++x) {
							var ofs = (baseOfs+x)*4;
							pixels[pixOfs++] = new Color32(_avatarBytes[ofs], _avatarBytes[ofs+1], _avatarBytes[ofs+2], _avatarBytes[ofs+3]);
						}
					}

					avatar.SetPixels32(pixels);
					avatar.Apply();
					avatar.Compress(false);
				}
			}
			return avatar;
		}

		public void ReleaseAllAvatars() {
			foreach (var player in _players.Values) {
				player.ReleaseSmallAvatar();
				player.ReleaseLargeAvatar();
			}
		}

		void OnRichPresenceUpdated(FriendRichPresenceUpdate_t param) {
			var player = GetSteamPlayer(param.m_steamIDFriend);
			if (player != null) {
				player.InternalRichPresenceUpdated();
				player.Dispose();
			}
		}

		public ReadOnlyCollection<OnlinePlayer> friends {
			get {
				return _roFriends;
			}
		}

		public bool startupLobbyConnect {
			get;
			set;
		}

		internal void InternalPersonaStateChanged(SteamPlayer player, EOnlinePlayerChangedFlags flags) {
			if (player.isUpdatePending) {
				return;
			}

			if (DispatchAsyncPlayerGetCallbacks(player)) {
				player.Dispose();
			}

			player.InvokePlayerChangedCallbacks(flags);

			if (_changedCallbacks != null) {
				if (_changedCallbacks.Count > 0) {
					player.CheckDisposed();
				}
				for (int i = _changedCallbacks.Count-1; i >= 0; --i) {
					_changedCallbacks[i](player, flags);
				}
			}
		}

		internal bool DispatchAsyncPlayerGetCallbacks(SteamPlayer player) {
			List<OnlinePlayerChangedDelegate> callbacks;
			if (_pendingAsyncPlayerGets.TryGetValue(player.id.uuid, out callbacks)) {
				_pendingAsyncPlayerGets.Remove(player.id.uuid);
				for (int i = 0; i < callbacks.Count; ++i) {
					callbacks[i](player, EOnlinePlayerChangedFlags.Name|EOnlinePlayerChangedFlags.Status);
				}
				return true;
			}
			return false;
		}

		void OnGameInviteAccepted(GameRichPresenceJoinRequested_t param) {
            // This callback function is activated when a steam invite has been received & we're already playing the game.
            // It is not called when the game is opened from scratch in response to an invite.

#if !DEDICATED_SERVER && LOGIN_SERVER
			if (GameManager.instance.loginServer.pendingLobbyConnect) {
				// this can only happen if the client's been started in response to another invite (sent when it wasn't running)
				// and has already performed the AsyncLogin & is in the process of switching rooms/lobby.  Hence we'll do 
				// nothing with an in-game connect request while the other one's being handled.
				Debug.Log("SteamServices: OnGameInviteAccepted - a pending invite is already being processed, ignoring this one");
				return;
			} 

			// the connect string should hold the lobby ID, lets switch to it...
			if (param.m_rgchConnect.Contains("+connect_lobby ")) {
				var str = param.m_rgchConnect.Substring(15);
				if (!string.IsNullOrEmpty(str)) {
					Debug.Log("SteamServices: OnGameInviteAccepted has a '+connect_lobby' string of " + str);
					uint lobbyID;
					if (uint.TryParse(str, out lobbyID)) {
						if (lobbyID != 0) {
							// Set the pendingLobby values, which will be handled in the Tick() loop...
							GameManager.instance.loginServer.pendingLobbyConnect = true;
							GameManager.instance.loginServer.pendingLobbyID = lobbyID;
							GameManager.instance.loginServer.pendingPassword = string.Empty;  // passwords are handled elsewhere
							Debug.Log("SteamServices: OnGameInviteAccepted - pendingLobbyID set to " + lobbyID);
						} else {
							Debug.Log("SteamServices: OnGameInviteAccepted - Error: argument's lobbyID is 0");
						}
					} else {
						Debug.Log("SteamServices: OnGameInviteAccepted failed to parse argument");
					}
				} else {
					Debug.Log("SteamServices: OnGameInviteAccepted was passed an empty string");
				}
			} else {
				Debug.Log("SteamServices: OnGameInviteAccepted called but not passed a '+connect_lobby' argument");
			}
#endif
        }

		public bool IsPlayingDeadhold(OnlinePlayer player) {
			return IsPlayingDeadhold(player.id.uuid);
		}

		public bool IsPlayingDeadhold(ulong id) {
			FriendGameInfo_t gameInfo;
			if (NativeMethods.ISteamFriends_GetFriendGamePlayed((CSteamID)id, out gameInfo)) {
				if (gameInfo.m_gameID.m_GameID == APP_ID.m_AppId) {
					return true;
				}
			}
			return false;
		}

		public void SendPrivateMessage(OnlinePlayer player, string text) {
			SendPrivateMessage(player.id.uuid, text);
		}

		public void SendPrivateMessage(ulong id, string text) {
			InteropHelp.UTF8StringHandle convertedMessage = new InteropHelp.UTF8StringHandle(text);
			NativeMethods.ISteamFriends_ReplyToFriendMessage((CSteamID)id, convertedMessage);
			convertedMessage.Dispose();
		}

		public bool ShowPlayerProfile(OnlinePlayer player) {
			return ShowPlayerProfile(player.id.uuid);
		}

		public bool ShowPlayerProfile(ulong id) {
			if (!NativeMethods.ISteamUtils_IsOverlayEnabled()) {
				return false;
			}
			InteropHelp.UTF8StringHandle dialog = new InteropHelp.UTF8StringHandle("steamid");
			NativeMethods.ISteamFriends_ActivateGameOverlayToUser(dialog, (CSteamID)id);
			dialog.Dispose();
			return true;
		}
	}
}
