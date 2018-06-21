// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.ObjectModel;
using System.Threading;

namespace Bowhead.Online {

	[Flags]
	public enum EOnlinePlayerChangedFlags {
		Name = 0x1,
		Status = 0x2,
		Icon = 0x4,
		Avatar = 0x8,
		Presence = 0x10,
		FriendAdded = 0x20,
		FriendRemoved = 0x40,
		FriendChanged = FriendAdded|FriendRemoved
	}

	public enum EOnlinePlayerStatus {
		Offline,
		Online,
		Away,
		Busy,
		InThisGame,
		InAnotherGame
	}

	public enum ELobbyType {
		Public,
		Friends,
		Private
	}

	public delegate void OnlinePlayerLoggedInDelegate(OnlineLocalPlayer player, string errorMessage);
	public delegate void OnlinePlayerChangedDelegate(OnlinePlayer player, EOnlinePlayerChangedFlags whatChanged);
	public delegate void OnlinePlayerDelegate(OnlinePlayer player);
	public delegate void OnlineFriendChatDelegate(string playerName, ulong playerID, string text);

	public interface RefCounted : System.IDisposable {
		int refCount { get; }
		bool disposed { get; }
		void AddRef();
	}

	public abstract class RefCountedObj : RefCounted {
		int _refCount;

		public RefCountedObj() : this(1) { }

		public RefCountedObj(int initialCount) {
			_refCount = initialCount;
		}

		public int refCount {
			get {
				return _refCount;
			}
		}

		public bool disposed {
			get;
			private set;
		}

		public void AddRef() {
			Interlocked.Increment(ref _refCount);
		}

		public void Dispose() {
			CheckDisposed();
			var r = Interlocked.Decrement(ref _refCount);
			if (r < 0) {
				throw new ObjectDisposedException(GetType().FullName);
			} else if (r == 0) {
				OnDisposed();
				disposed = true;
			}
		}

		protected abstract void OnDisposed();

		public void CheckDisposed() {
			if (disposed) {
				throw new ObjectDisposedException(GetType().FullName);
			}
		}
	}

	public interface OnlinePlayerID : System.IEquatable<OnlinePlayerID> {
		ulong uuid { get; }
	}

	public interface OnlinePlayer : RefCounted {
		string name { get; }
		OnlinePlayerID id { get; }
		Texture2D smallAvatar { get; }
		Texture2D largeAvatar { get;	}
		EOnlinePlayerStatus status { get; }
		void SendLobbyInvite(uint lobbyID);
		void AsyncGetSmallAvatar();
		void AsyncGetLargeAvatar();
		void ReleaseSmallAvatar();
		void ReleaseLargeAvatar();
		void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback);
		void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback);
	}

	public interface OnlineLocalPlayer : OnlinePlayer {
		string ticket { get; }
	}

	public interface OnlineServices : IDisposable {
		bool Initialize();
		void Tick(float dt);
		void SetLobbyID(uint lobbyID, ELobbyType type);
		void AsyncLogin(OnlinePlayerLoggedInDelegate callback);
		void AsyncGetOnlinePlayer(ulong uuid, OnlinePlayerChangedDelegate callback);
		void AsyncGetOnlinePlayer(OnlinePlayerID id, OnlinePlayerChangedDelegate callback);
		void RemovePendingAsyncGetOnlinePlayerCallback(OnlinePlayerChangedDelegate callback);
		OnlinePlayerID GetOnlinePlayerID(ulong uuid);
		void AddFriendshipChangedCallback(OnlinePlayerChangedDelegate callback);
		void RemoveFriendshipChangedCallback(OnlinePlayerChangedDelegate callback);
		void AddOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback);
		void RemoveOnlinePlayerChangedCallback(OnlinePlayerChangedDelegate callback);
		void AddFriendChatMessageCallback(OnlineFriendChatDelegate callback);
		void RemoveFriendChatMessageCallback(OnlineFriendChatDelegate callback);
		bool IsFriend(OnlinePlayer player);
		bool IsFriend(ulong uuid);
		bool IsPlayingThisGame(OnlinePlayer player);
		bool IsPlayingThisGame(ulong uuid);
		void SendPrivateMessage(OnlinePlayer player, string text);
		void SendPrivateMessage(ulong uuid, string text);
		bool ShowPlayerProfile(OnlinePlayer player);
		bool ShowPlayerProfile(ulong uuid);
		void ReleaseAllAvatars();
        ReadOnlyCollection<OnlinePlayer> friends { get; }
	}

	public static class OnlineServicesAPI {
#if STEAM_API
		public static OnlineServices Create() {
			return new Steam.SteamServices();
		}
#else
		public static OnlineServices Create() {
			return new Null.OnlineNullServices();
		}
#endif
	}
}