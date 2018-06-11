// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Server {
	public class TeamStart : MonoBehaviour {

		public Color color;
		public int teamNumber;

		[NonSerialized]
		public List<PlayerStart> playerStarts = new List<PlayerStart>();
		[NonSerialized]
		public List<PlayerStart> assignedPlayerStarts = new List<PlayerStart>();
		[NonSerialized]
		public List<PlayerStart> unassignedPlayerStarts = new List<PlayerStart>();

		[NonSerialized]
		public Actors.ServerTeam teamActor;

		void Reset() {
			color = Color.red;
			teamNumber = -1;
		}
	}
}