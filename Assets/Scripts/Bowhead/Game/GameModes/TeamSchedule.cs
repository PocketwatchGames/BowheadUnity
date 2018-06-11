// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Xml;

namespace Bowhead.Server {
	public interface TeamSchedule {

		PlayerStart FindPlayerStart(GameMode gameMode, ActorReplicationChannel channel);
		void NotifyPlayerDisconnected(ActorReplicationChannel channel, PlayerStart playerStart, Exception e, EDisconnectReason reason, string msg);
		bool allPlayersConnected { get; }
		int numPlayers { get; }
		int team0Size { get; }
		int team1Size { get; }
		int teamSize { get; }
		bool loaded { get; }
		void Reset();
		MetaGame.PlayerInventorySkills GetPrecachedInventory(ulong uuid);
		MetaGame.PlayerInventorySkills[] GetAllPlayerInventorySkills();
	}

	public interface SessionTeamSchedule : TeamSchedule {
		ReadOnlyCollection<Online.OnlinePlayerID> sessionPlayers { get; }
	}

	// Player join order determines team assignments.

	public sealed class StandardTeamSchedule : TeamSchedule {

		int _numPlayers;
		int _numPlayersConnected;

		public StandardTeamSchedule(int teamSize) {
			_numPlayers = 0;
			_numPlayersConnected = 0;
			this.teamSize = teamSize;
		}

		public PlayerStart FindPlayerStart(GameMode gameMode, ActorReplicationChannel channel) {

			TeamStart teamStart = null;
			int bestCount = 0;

			foreach (var ts in gameMode.teamStarts) {
				if (ts.unassignedPlayerStarts.Count > bestCount) {
					teamStart = ts;
					bestCount = ts.unassignedPlayerStarts.Count;
				}
			}

			if (teamStart == null) {
				return null;
			}

			int playerIndex = 0;

			for (;;) {
				int prevIndex = playerIndex;
				for (int i = 0; i < teamStart.assignedPlayerStarts.Count; ++i) {
					var ps = teamStart.assignedPlayerStarts[i];
					if (ps.teamSlot == playerIndex) {
						++playerIndex;
					}
				}
				if (prevIndex == playerIndex) {
					break;
				}
			}

			List<PlayerStart> wildcardPlayerStarts = new List<PlayerStart>();

			foreach (var playerStart in teamStart.unassignedPlayerStarts) {
				if (playerStart.playerController == null) {
					if (playerStart.playerIndex == -1) {
						wildcardPlayerStarts.Add(playerStart);
					} else if (playerStart.playerIndex == playerIndex) {
						++_numPlayersConnected;
						teamStart.unassignedPlayerStarts.Remove(playerStart);
						teamStart.assignedPlayerStarts.Add(playerStart);
						playerStart.primaryColor = teamStart.color;
						playerStart.secondaryColor = playerStart.color;
						playerStart.teamSlot = playerIndex;
						return playerStart;
					}
				}
			}

			if (wildcardPlayerStarts.Count > 0) {
				++_numPlayersConnected;
				var ps = wildcardPlayerStarts[GameManager.instance.RandomRange(0, wildcardPlayerStarts.Count)];
				teamStart.unassignedPlayerStarts.Remove(ps);
				teamStart.assignedPlayerStarts.Add(ps);
				ps.primaryColor = teamStart.color;
				ps.secondaryColor = ps.color;
				ps.teamSlot = playerIndex;
				return ps;
			}

			return null;
		}

		public void NotifyPlayerDisconnected(ActorReplicationChannel channel, PlayerStart playerStart, Exception e, EDisconnectReason reason, string msg) {
			playerStart.playerTeam.assignedPlayerStarts.Remove(playerStart);
			playerStart.playerTeam.unassignedPlayerStarts.Add(playerStart);
			playerStart.playerController = null;
			--_numPlayersConnected;
		}

		public MetaGame.PlayerInventorySkills GetPrecachedInventory(ulong uuid) {
			return new MetaGame.PlayerInventorySkills(uuid, MetaGame.PlayerInventorySkills.API.Server);
		}

		public MetaGame.PlayerInventorySkills[] GetAllPlayerInventorySkills() {
			return null;
		}

		public bool allPlayersConnected {
			get {
				return (_numPlayersConnected >= numPlayers);
			}
		}

		public int numPlayers {
			get {
				return _numPlayers;
			}
		}

		public int team0Size {
			get {
				return numPlayers - team1Size;
			}
		}

		public int team1Size {
			get {
				return numPlayers / 2;
			}
		}

		public int teamSize {
			get;
			private set;
		}

		public bool loaded {
			get {
				return true;
			}
		}

		public void Reset() {
			_numPlayers = GameManager.instance.numPlayers;
			_numPlayersConnected = 0;
		}
	}

#if BACKEND_SERVER
	public class XMLTeamSchedule : TeamSchedule {

		struct Player {
			public Player(ulong steamID, ulong challenge, int team, int index, Color32 color0, Color32 color1) {
				this.steamID = steamID;
				this.challenge = challenge;
				this.team = team;
				this.index = index;
				this.color0 = color0;
				this.color1 = color1;
				this.inventorySkills = new MetaGame.PlayerInventorySkills(steamID, MetaGame.PlayerInventorySkills.API.Server);
			}
			
			public readonly ulong steamID;
			public readonly ulong challenge;
			public readonly int index;
			public readonly int team;
			public readonly Color32 color0;
			public readonly Color32 color1;
			public readonly MetaGame.PlayerInventorySkills inventorySkills;
		}
		
		Dictionary<ulong, Player> _players = new Dictionary<ulong, Player>();
		int _numPlayersConnected;

		public XMLTeamSchedule(string path) {
			LoadXML(path);
		}

		public bool allPlayersConnected {
			get {
				return (_numPlayersConnected >= numPlayers);
			}
		}

		public int numPlayers {
			get {
				return _players.Count;
			}
		}

		public int team0Size {
			get;
			private set;
		}

		public int team1Size {
			get;
			private set;
		}

		public int teamSize {
			get;
			private set;
		}

		void LoadXML(string path) {
			var xml = new XmlDocument();
			xml.Load(path);

			var schedule = (XmlElement)xml.SelectNodes("//schedule")[0];

			var numTeams = int.Parse(schedule.GetAttribute("numTeams"));
			teamSize = int.Parse(schedule.GetAttribute("teamSize"));

			for (int i = 0; i < numTeams; ++i) {
				var telm = (XmlElement)schedule.SelectNodes("//team" + i)[0];

				int index = 0;
				foreach (var n in telm.ChildNodes) {
					var elm = n as XmlElement;
					if (elm != null) {
						if (elm.Name == "player") {
							if (i == 0) {
								++team0Size;
							} else if (i == 1) {
								++team1Size;
							}

							ulong steamID = ulong.Parse(elm.GetAttribute("steamID"));
							ulong challenge = ulong.Parse(elm.GetAttribute("challenge"));
							uint color0 = uint.Parse(elm.GetAttribute("color0"));
							uint color1 = uint.Parse(elm.GetAttribute("color1"));
							_players.Add(steamID, new Player(steamID, challenge, i, index++, Utils.GetColor32FromUIntRGB(color0), Utils.GetColor32FromUIntRGB(color1)));
						}
					}
				}
			}
		}

		public MetaGame.PlayerInventorySkills GetPrecachedInventory(ulong uuid) {
			Player player;
			if (_players.TryGetValue(uuid, out player)) {
				return player.inventorySkills;
			}
			return null;
		}

		public MetaGame.PlayerInventorySkills[] GetAllPlayerInventorySkills() {
			List<MetaGame.PlayerInventorySkills> list = new List<MetaGame.PlayerInventorySkills>();
			foreach (var player in _players.Values) {
				list.Add(player.inventorySkills);
			}
			return list.ToArray();
		}
		
		public PlayerStart FindPlayerStart(GameMode gameMode, ActorReplicationChannel channel) {

			Player schedulePlayer;
			if (!_players.TryGetValue(channel.uuid, out schedulePlayer)) {
				return null;
			}

			if (channel.challenge != schedulePlayer.challenge) {
				Debug.Log("Player " + channel.uuid + " failed challenge response.");
				return null;
			}

			if (schedulePlayer.team >= gameMode.teamStarts.Length) {
				return null;
			}

			var teamStart = gameMode.teamStarts[schedulePlayer.team];

			int playerIndex = 0;

			for (;;) {
				int prevIndex = playerIndex;
				for (int i = 0; i < teamStart.assignedPlayerStarts.Count; ++i) {
					var ps = teamStart.assignedPlayerStarts[i];
					if (ps.teamSlot == playerIndex) {
						++playerIndex;
					}
				}
				if (prevIndex == playerIndex) {
					break;
				}
			}
			
			List<PlayerStart> wildcardPlayerStarts = new List<PlayerStart>();

			foreach (var playerStart in teamStart.unassignedPlayerStarts) {
				if (playerStart.playerController == null) {
					if (playerStart.playerIndex == -1) {
						wildcardPlayerStarts.Add(playerStart);
					} else if (playerStart.playerIndex == schedulePlayer.index) {
						++_numPlayersConnected;
						teamStart.unassignedPlayerStarts.Remove(playerStart);
						teamStart.assignedPlayerStarts.Add(playerStart);
						playerStart.primaryColor = schedulePlayer.color0;
						playerStart.secondaryColor = schedulePlayer.color1;
						playerStart.teamSlot = playerIndex;

						if (teamStart.assignedPlayerStarts.Count == 1) {
							teamStart.color = playerStart.primaryColor;
						}

						return playerStart;
					}
				}
			}

			if (wildcardPlayerStarts.Count > 0) {
				++_numPlayersConnected;
				var ps = wildcardPlayerStarts[GameManager.instance.RandomRange(0, wildcardPlayerStarts.Count)];
				teamStart.unassignedPlayerStarts.Remove(ps);
				teamStart.assignedPlayerStarts.Add(ps);
				ps.primaryColor = schedulePlayer.color0;
				ps.secondaryColor = schedulePlayer.color1;
				ps.teamSlot = playerIndex;

				if (teamStart.assignedPlayerStarts.Count == 1) {
					teamStart.color = ps.primaryColor;
				}

				return ps;
			}

			return null;
		}

		public void NotifyPlayerDisconnected(ActorReplicationChannel channel, PlayerStart playerStart, Exception e, EDisconnectReason reason, string msg) {
			playerStart.playerTeam.assignedPlayerStarts.Remove(playerStart);
			playerStart.playerTeam.unassignedPlayerStarts.Add(playerStart);
			playerStart.playerController = null;
			--_numPlayersConnected;

			var gameMode = GameManager.instance.serverWorld.gameMode;
			var matchState = gameMode.matchState;
			if ((matchState >= GameMode.EMatchState.Countdown) && (matchState <= GameMode.EMatchState.MatchOvertime)) {
				GameManager.instance.telemetry.PlayerDropped(channel.uuid, e, reason, msg);
			}
			if (_numPlayersConnected < 1) {
				if (matchState < GameMode.EMatchState.MatchComplete) {
					GameManager.instance.telemetry.GameWasAbandoned();
				} else {
					GameManager.instance.telemetry.ExitGame();
				}
			}

		}

		public void PlayerDropped(ulong steamID) {
			// find player
			var world = GameManager.instance.serverWorld;
			var clients = world.clientConnections;
			for (int i = 0; i < clients.Count; ++i) {
				var client = clients[i];
				if (client.uuid == steamID) {
					world.DisconnectClient(client.connection, null, EDisconnectReason.DisconnectedByLoginServer, null);
					break;
				}
			}
		}

		public bool loaded {
			get {
				foreach (var player in _players.Values) {
					if (!player.inventorySkills.ready) {
						return false;
					}
				}
				return true;
			}
		}

		public void Reset() {
			_numPlayersConnected = 0;
		}
	}
#endif
}
