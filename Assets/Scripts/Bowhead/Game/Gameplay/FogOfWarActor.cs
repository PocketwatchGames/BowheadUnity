// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;

namespace Bowhead.Actors {
	public enum EFogOfWarTest {
		SightCheck,
		AlwaysVisible,
		NeverVisible
	}

	public interface FogOfWarActor : ActorWithTeam {
		
		float fogOfWarSightRadius { get; }
		Vector2 fogOfWarPosition { get; }
		int fogOfWarActorID { get; }
		float fogOfWarObjectRadius { get; }
		float fogOfWarMaxVisRadius { get; }
		float fogOfWarUnderwaterMaxVisRadius { get; }
		bool fogOfWarIsUnderwater { get; }
		bool fogOfWarCanSeeUnderwater { get; }
		EFogOfWarTest fogOfWarTest { get; }
	}

	public class FogOfWarController {

		class TeamSightList {
			public List<FogOfWarActor> actors = new List<FogOfWarActor>();
			public Dictionary<int, bool> visiblity = new Dictionary<int, bool>();
		}

		List<TeamSightList> _teams = new List<TeamSightList>();

		float _updateRate;
		float _nextUpdate;
		Server.ServerWorld _world;

		public FogOfWarController(Server.ServerWorld world, float updateRate) {
			_world = world;
			_updateRate = updateRate;
			enabled = true;
		}

		public void Tick(float dt) {
			_nextUpdate -= dt;
			if (_nextUpdate <= 0f) {
				Update();
			}
		}

		public void Update() {
			for (int i = 0; i < _teams.Count; ++i) {
				var t = _teams[i];
				if (t != null) {
					t.visiblity.Clear();
				}
			}

			_nextUpdate = _updateRate;
		}

		public void AddActor(FogOfWarActor actor) {
			if (actor.team != null) {
				var team = GetTeamSightList(actor.team);
				Assert.IsFalse(team.actors.Contains(actor));
				team.actors.Add(actor);
			}		
		}

		public void RemoveActor(FogOfWarActor actor) {
			if (actor.team != null) {
				var team = GetTeamSightList(actor.team);
				team.actors.Remove(actor);
			}
		}

		public bool CanBeSeenByTeam(Team team, FogOfWarActor actor) {
			Perf.Begin("FogOfWarController.CanBeSeenByTeam");
			if (team.isMonsterTeam) {
				return true;
			}

			if (!enabled || Client.Actors.ClientPlayerController.debugFogOfWar || (team == actor.team) || (actor.fogOfWarTest == EFogOfWarTest.AlwaysVisible) || (_world.gameMode.liftFogOfWarAtEndOfMatch && (_world.gameMode.matchState >= Server.GameMode.EMatchState.MatchComplete))) {
				Perf.End();
				return true;
			}

			if (actor.fogOfWarTest == EFogOfWarTest.NeverVisible) {
				Perf.End();
				return false;
			}

			var teamState = GetTeamSightList(team);

			bool vis;

			if (teamState.visiblity.TryGetValue(actor.fogOfWarActorID, out vis)) {
				Perf.End();
				return vis;
			}

			vis = false;

			for (int i = 0; i < teamState.actors.Count; ++i) {
				var a = teamState.actors[i];
				if (CheckSightRange(a, actor)) {
					vis = true;
					break;
				}
			}

			teamState.visiblity[actor.fogOfWarActorID] = vis;
			Perf.End();
			return vis;
		}

		bool CheckSightRange(FogOfWarActor a, FogOfWarActor b) {
			if (a.fogOfWarSightRadius <= 0f) {
				return false;
			}

			var d = a.fogOfWarPosition - b.fogOfWarPosition;
			var dmag = d.magnitude;

			if (b.fogOfWarIsUnderwater && !(a.fogOfWarIsUnderwater || a.fogOfWarCanSeeUnderwater) && (b.fogOfWarUnderwaterMaxVisRadius > 0f) && (dmag > b.fogOfWarUnderwaterMaxVisRadius)) {
				return false;
			}
			
			if ((b.fogOfWarMaxVisRadius > 0f) && (dmag > b.fogOfWarMaxVisRadius)) {
				return false;
			}

			var dd = dmag - a.fogOfWarSightRadius - b.fogOfWarObjectRadius;
			return dd <= 0f;
		}

		TeamSightList GetTeamSightList(Team team) {
			while (_teams.Count <= team.teamNumber) {
				_teams.Add(null);
			}

			var teamSightList = _teams[team.teamNumber];
			if (teamSightList == null) {
				teamSightList = new TeamSightList();
				_teams[team.teamNumber] = teamSightList;
			}

			return teamSightList;
		}

		public bool enabled {
			get; set;
		}

		public float updateRate {
			get {
				return _updateRate;
			}
		}
	}
}