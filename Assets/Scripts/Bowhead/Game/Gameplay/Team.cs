// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Bowhead.Actors {

	/**
		The team actor contains a list of all players that belong on a team, and
		includes the teams score.
	**/
	public class Team : Actor {
		public const int MONSTER_TEAM_NUMBER = -1;
		public const int NPC_TEAM_NUMBER = -2;

		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_players")]
		List<PlayerState> _players;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _teamNumber;

		ReadOnlyCollectionEx<PlayerState> _roPlayers = new ReadOnlyCollectionEx<PlayerState>(new List<PlayerState>());
		
		public Team() {
			SetReplicates(true);
			SetReplicateRate(1/4f);
		}

		public int teamNumber {
			get {
				return _teamNumber;
			}
			protected set {
				_teamNumber = value;
			}
		}

		public bool isMonsterTeam {
			get {
				return _teamNumber == MONSTER_TEAM_NUMBER;
			}
		}

		public bool isNPCTeam {
			get {
				return _teamNumber == NPC_TEAM_NUMBER;
			}
		}

		public ReadOnlyCollection<PlayerState> players {
			get {
				return _roPlayers;
			}
		}

		protected virtual void OnRep_players() {
			if (_players == null) {
				_roPlayers = new ReadOnlyCollectionEx<PlayerState>(new List<PlayerState>());
			} else if ((_roPlayers == null) || !_roPlayers.Wraps(_players)) {
				_roPlayers = new ReadOnlyCollectionEx<PlayerState>(_players);
			}
		}

		[RPC(ERPCDomain.Multicast)]
		protected void NetMulticast_AddPlayerToTeam(PlayerState player) {
			if (_players == null) {
				_players = new List<PlayerState>();
			}

			_players.Add(player);
			OnRep_players();
		}

		[RPC(ERPCDomain.Multicast)]
		protected void NetMulticast_RemovePlayerFromTeam(PlayerState player) {
			if (_players != null) {
				_players.Remove(player);
				OnRep_players();
			}
		}

		public override Type clientType => typeof(Team);
		public override Type serverType => typeof(Server.Actors.ServerTeam);
	}
}