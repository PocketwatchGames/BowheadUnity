// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace Bowhead.Server.Actors {
	using PlayerState = Bowhead.Actors.PlayerState;
	
	/**
		Mutable version of player state.
	**/
	public class ServerPlayerState : PlayerState {

		ServerTeam _team;

		readonly ActorRPC<int> rpc_Multicast_SetPermissionLevel;

		public ServerPlayerState() {
			rpc_Multicast_SetPermissionLevel = BindRPC<int>(Multicast_SetPermissionLevel);
		}

		public new ServerTeam team {
			get {
				return _team;
			}
			set {
				_team = value;
				base.team = value;
			}
		}

#if !(LOGIN_SERVER || BACKEND_SERVER)
		public new string playerName {
			get {
				return base.playerName;
			}
			set {
				base.playerName = value;
			}
		}
#endif

		public new ulong onlineUUID {
			get {
				return base.onlineUUID;
			}
			set {
				base.onlineUUID = value;
			}
		}

		public new bool loaded {
			get {
				return base.loaded;
			}
			set {
				base.loaded = value;
			}
		}

		public ServerPlayerController playerController {
			get;
			set;
		}

		public void SetPermissionLevel(int level) {
			Multicast_SetPermissionLevel(level);
			rpc_Multicast_SetPermissionLevel.Invoke(level);
		}
	}

}