// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using Bowhead.Actors;

namespace Bowhead.Server.Actors {
	using Team = Bowhead.Actors.Team;
	using PlayerState = Bowhead.Actors.PlayerState;

	/**
		Mutable version of Team actor.
	**/
	public class ServerTeam : Team {

		float _score;
		float _score2;
		float _goalPoints;
		float _goalPoints2;

		readonly ActorRPC<PlayerState> rpc_NetMulticast_AddPlayerToTeam;
		readonly ActorRPC<PlayerState> rpc_NetMulticast_RemovePlayerFromTeam;

		public ServerTeam() {
			rpc_NetMulticast_AddPlayerToTeam = BindRPC<PlayerState>(base.NetMulticast_AddPlayerToTeam);
			rpc_NetMulticast_RemovePlayerFromTeam = BindRPC<PlayerState>(base.NetMulticast_RemovePlayerFromTeam);

		}

		public void AddPlayerToTeam(PlayerState player) {
			NetMulticast_AddPlayerToTeam(player);
			rpc_NetMulticast_AddPlayerToTeam.Invoke(player);
		}

		public void RemovePlayerFromTeam(PlayerState player) {
			NetMulticast_RemovePlayerFromTeam(player);
			rpc_NetMulticast_RemovePlayerFromTeam.Invoke(player);
		}

		public override void BeginTravel() {
			base.BeginTravel();
		}

		public new int teamNumber {
			get {
				return base.teamNumber;
			}
			set {
				base.teamNumber = value;
			}
		}		

		public new Color teamColor {
			get {
				return base.teamColor;
			}
            set {
				base.teamColor = value;
			}
		}

		public new float score {
			get {
				return _score;
			}
			set {
				_score = value;
				base.score = Mathf.FloorToInt(_score + _goalPoints);
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

		public int intScore {
			get {
				return base.score;
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

		public int intScore2 {
			get {
				return base.score2;
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

		public new bool winning {
			get {
				return base.winning;
			}
			set {
				base.winning = value;
			}
		}
	}

}