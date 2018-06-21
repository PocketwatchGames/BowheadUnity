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
	}

}