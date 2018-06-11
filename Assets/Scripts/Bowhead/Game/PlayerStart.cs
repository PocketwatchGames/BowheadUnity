// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Server {
	public class PlayerStart : MonoBehaviour {

		public Transform startPoint;
		public TeamStart playerTeam;
		public int playerIndex;
		public Actors.ServerPlayerController playerController;
		public Color color;

		[NonSerialized]
		public Color primaryColor;
		[NonSerialized]
		public Color secondaryColor;

		public int teamSlot {
			get;
			set;
		}

		void Reset() {
			startPoint = transform;
			playerTeam = null;
			playerIndex = -1;
			color = Color.yellow;
		}
	}
}