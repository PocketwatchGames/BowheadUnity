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
		public const int MONSTER_TEAM_NUMBER = 99;
		public const int NPC_TEAM_NUMBER = 100;

		[Replicated(Condition = EReplicateCondition.InitialOnly, Notify = "OnRep_players")]
		List<PlayerState> _players;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		int _teamNumber;

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		Color _teamColor;

		[Replicated(Notify = "OnRep_Score")]
		int _score;
		[Replicated(Notify = "OnRep_Score")]
		int _score2;

		[Replicated(Notify = "OnRep_Winning")]
		bool _winning;

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

		public Color teamColor {
			get {
				return _teamColor;
			}
			protected set {
				_teamColor = value;
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

		public bool winning {
			get {
				return _winning;
			}
			protected set {
				_winning = value;
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

		protected virtual void OnRep_Score() {
			var localPlayer = Client.Actors.ClientPlayerController.localPlayer;
			if ((localPlayer != null) && (localPlayer.gameState != null) && (localPlayer.gameState.hud != null)) {
				localPlayer.gameState.hud.OnTeamScoreChanged(this);
			}
		}

		protected virtual void OnRep_Winning() {
			var localPlayer = Client.Actors.ClientPlayerController.localPlayer;
			if ((localPlayer != null) && (localPlayer.gameState != null) && (localPlayer.gameState.hud != null)) {
				localPlayer.gameState.hud.OnTeamWinningChanged();
			}
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected void NetMulticast_AddPlayerToTeam(PlayerState player) {
			if (_players == null) {
				_players = new List<PlayerState>();
			}

			_players.Add(player);
			OnRep_players();
		}

		[RPC(ERPCDomain.Multicast, Reliable = true)]
		protected void NetMulticast_RemovePlayerFromTeam(PlayerState player) {
			if (_players != null) {
				_players.Remove(player);
				OnRep_players();
			}
		}

		public override Type clientType {
			get {
				return typeof(Team);
			}
		}

		public override Type serverType {
			get {
				return typeof(Server.Actors.ServerTeam);
			}
		}
	}
}