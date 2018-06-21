// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		UIController _uiController;

		public BowheadHUD(ClientWorld world, GameState gameState) : base(world, gameState) {
			_uiController = GameObject.Instantiate(GameManager.instance.clientData.hudUIController, hudCanvas.transform, false);
		}

		public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_uiController.SetPlayer(player);
		}
	}
}