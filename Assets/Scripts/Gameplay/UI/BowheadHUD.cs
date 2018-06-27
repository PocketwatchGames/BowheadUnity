﻿// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		InventoryPanel _inventory;
		PlayerStatePanel _playerState;
        ButtonHint _interactHint;

		public BowheadHUD(ClientWorld world, GameState gameState) : base(world, gameState) {
			_inventory = GameObject.Instantiate(GameManager.instance.clientData.hudInventoryPanelPrefab, hudCanvas.transform, false);
			_playerState = GameObject.Instantiate(GameManager.instance.clientData.hudPlayerStatePanelPrefab, hudCanvas.transform, false);
            _interactHint = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, hudCanvas.transform, false);

        }

        public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_inventory.Init(player);
			_playerState.Init(player);
		}

        public override void Tick(float dt) {
            base.Tick(dt);

            var target = localPlayer.playerPawn.GetInteractTarget();
            _interactHint.SetTarget(target);
        }
    }
}