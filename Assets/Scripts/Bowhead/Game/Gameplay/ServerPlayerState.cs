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
		float _score;
		float _score2;
		float _goalPoints;
		float _goalPoints2;

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

		public new Color primaryColor {
			get {
				return base.primaryColor;
			}
			set {
				base.primaryColor = value;
			}
		}

		public new Color secondaryColor {
			get {
				return base.secondaryColor;
			}
			set {
				base.secondaryColor = value;
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

		public new float score {
			get {
				return _score;
			}
            set {
				_score = value;
				base.score = Mathf.FloorToInt(_score + _goalPoints);
			}
		}

		public new float score2 {
			get {
				return _score2;
			}
			set {
				_score2 = value;
				base.score2 = Mathf.FloorToInt(_score2 + _goalPoints2);
			}
		}

		public int intScore {
			get {
				return base.score;
			}
		}

		public float goalPoints {
			get {
				return _goalPoints;
			}

			set {
				_goalPoints = value;
				base.score = Mathf.FloorToInt(_score + _goalPoints);
			}
		}

		public float goalPoints2 {
			get {
				return _goalPoints2;
			}

			set {
				_goalPoints2 = value;
				base.score2 = Mathf.FloorToInt(_score2 + _goalPoints2);
			}
		}

		public new float health {
			get {
				return base.health;
			}
			set {
				base.health = value;
			}
		}

		public new bool winner {
			get {
				return base.winner;
			}
			set {
				base.winner = value;
			}
		}

		public new ulong onlineUUID {
			get {
				return base.onlineUUID;
			}
			set {
				base.onlineUUID = value;
			}
		}

		public new int xp {
			get {
				return base.xp;
			}
			set {
				base.xp = value;
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

		public int killedUnitValue {
			get;
			set;
		}

		public bool readyToPlay {
			get;
			set;
		}

		public ServerPlayerController playerController {
			get;
			set;
		}

		public int numDropKills;

		public void SetPermissionLevel(int level) {
			Multicast_SetPermissionLevel(level);
			rpc_Multicast_SetPermissionLevel.Invoke(level);
		}
	}

}