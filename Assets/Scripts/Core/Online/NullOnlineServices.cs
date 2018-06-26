// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

namespace Bowhead.Online.Null {

	public sealed class OnlineNullPlayerID : OnlinePlayerID {

		public OnlineNullPlayerID(ulong uuid) {
			this.uuid = uuid;
		}

		public OnlineNullPlayerID() {
			uuid = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
		}

		public ulong uuid {
			get;
			private set;
		}

		public bool Equals(Bowhead.Online.OnlinePlayerID other) {
			return other.uuid == uuid;
		}

		public static OnlineNullPlayerID GetLocalPlayerPersistentID() {
			var guid = UserPrefs.instance.GetString("null_online_localplayer_guid", null);
			if (guid != null) {
				return new OnlineNullPlayerID(ulong.Parse(guid));
			}
			var uuid = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
			UserPrefs.instance.SetString("null_online_localplayer_guid", uuid.ToString());
			UserPrefs.instance.Save();
			return new OnlineNullPlayerID(uuid);
		}
	}

	public class OnlineNullPlayer : RefCountedObj, OnlinePlayer {

		public OnlineNullPlayer(OnlinePlayerID id, bool isLocalPlayer) {
			this.id = id;
#if LOGIN_SERVER
			if (isLocalPlayer) {
				name = string.Format("LP-{0:X4}", id.uuid.ToString()).Substring(0, 8);
			} else {
				name = string.Format("{0:X4}", id.uuid.ToString()).Substring(0, 8);
			}
#else
			name = id.uuid.ToString();
#endif
		}

		public OnlinePlayerID id {
			get;
			private set;
		}

		public string name {
			get;
			private set;
		}

		public Texture2D smallAvatar {
			get {
				return null;
			}
		}

		public Texture2D largeAvatar {
			get {
				return null;
			}
		}

		public EOnlinePlayerStatus status {
			get {
				return EOnlinePlayerStatus.Online;
			}
		}

		public void SendLobbyInvite(uint lobbyID) { }

		public void AsyncGetSmallAvatar() { }
		public void AsyncGetLargeAvatar() { }
		public void ReleaseSmallAvatar() { }
		public void ReleaseLargeAvatar() { }

		public void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) { }
		public void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) { }

		protected override void OnDisposed() { }
	}

	public sealed class OnlineNullLocalPlayer : OnlineNullPlayer, OnlineLocalPlayer {

		public OnlineNullLocalPlayer() : base(OnlineNullPlayerID.GetLocalPlayerPersistentID(), true) { }

		public string ticket {
			get {
				return "INVALID";
			}
		}

		public int skver {
			get {
				return 0;
			}
		}
	}

	public sealed class OnlineNullServices : OnlineServices {
		static readonly ReadOnlyCollectionEx<OnlinePlayer> _friends = new ReadOnlyCollectionEx<OnlinePlayer>(new OnlinePlayer[0]);

		Dictionary<ulong, OnlineNullPlayer> _players = new Dictionary<ulong, OnlineNullPlayer>();
		OnlineNullLocalPlayer _localPlayer;

		public bool Initialize() {
			return true;
		}

		public void Dispose() { }

		public void Tick(float dt) { }

		public void AsyncLogin(OnlinePlayerLoggedInDelegate callback) {
			if (_localPlayer == null) {
				_localPlayer = new OnlineNullLocalPlayer();
				_players.Add(_localPlayer.id.uuid, _localPlayer);
			}
			callback(_localPlayer, null);
		}

		public void AsyncGetOnlinePlayer(ulong uuid, OnlinePlayerChangedDelegate callback) {
			OnlineNullPlayer player;
			if (!_players.TryGetValue(uuid, out player)) {
				player = new OnlineNullPlayer(new OnlineNullPlayerID(uuid), false);
				_players.Add(uuid, player);
			}
			callback(player, EOnlinePlayerChangedFlags.Name|EOnlinePlayerChangedFlags.Presence);
		}

		public void AsyncGetOnlinePlayer(OnlinePlayerID id, OnlinePlayerChangedDelegate callback) {
			AsyncGetOnlinePlayer(id.uuid, callback);
		}
		
		public void RemovePendingAsyncGetOnlinePlayerCallback(OnlinePlayerChangedDelegate callback) { }

		public OnlinePlayerID GetOnlinePlayerID(ulong uuid) {
			return new OnlineNullPlayerID(uuid);
		}

		public void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) { }
		public void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback) { }

		public void AddFriendshipChangedCallback(OnlinePlayerChangedDelegate callback) { }
		public void RemoveFriendshipChangedCallback(OnlinePlayerChangedDelegate callback) { }

		public void AddFriendChatMessageCallback(OnlineFriendChatDelegate callback) { }
		public void RemoveFriendChatMessageCallback(OnlineFriendChatDelegate callback) { }
		
		public void SetLobbyID(uint lobbyID, ELobbyType type) { }

		public void ReleaseAllAvatars() { }

		public bool IsFriend(OnlinePlayer player) {
			return false;
		}

		public bool IsFriend(ulong uuid) {
			return false;
		}

		public ReadOnlyCollection<OnlinePlayer> friends {
			get {
				return _friends;
			}
		}

		public bool IsPlayingThisGame(OnlinePlayer player) {
			return IsPlayingThisGame(player.id.uuid);
		}

		public bool IsPlayingThisGame(ulong id) {
			return id == _localPlayer.id.uuid;
		}

		public void SendPrivateMessage(OnlinePlayer player, string text) { }

		public bool ShowPlayerProfile(OnlinePlayer player) {
			return false;
		}

		

		public void SendPrivateMessage(ulong id, string text) { }

		public bool ShowPlayerProfile(ulong id) {
			return false;
		}
	}
}