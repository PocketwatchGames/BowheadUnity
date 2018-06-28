// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		InventoryPanel _inventory;
		PlayerStatePanel _playerState;
        ButtonHint _interactHint;
		Minimap _minimap;

		public BowheadHUD(ClientWorld world, GameState gameState) : base(world, gameState) {
			_inventory = GameObject.Instantiate(GameManager.instance.clientData.hudInventoryPanelPrefab, hudCanvas.transform, false);
			_playerState = GameObject.Instantiate(GameManager.instance.clientData.hudPlayerStatePanelPrefab, hudCanvas.transform, false);
            _interactHint = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, hudCanvas.transform, false);
			_minimap = GameObject.Instantiate(GameManager.instance.clientData.minimapPrefab, hudCanvas.transform, false);
        }

        public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_inventory.Init(player);
			_playerState.Init(player);

			_minimap.SetStreaming(player.world.worldStreaming);
			_minimap.SetOrigin(0, 0);

            player.OnExplore += OnExplore;
		}

        private void OnExplore(Vector2 pos, float radius) {
            _minimap.RevealArea(new Vector2(pos.x, pos.y), radius);
            _minimap.SetOrigin((int)(pos.x/32), (int)(pos.y/32));
        }

        public override void Tick(float dt) {
            base.Tick(dt);

            var target = localPlayer.playerPawn.GetInteractTarget();
            _interactHint.SetTarget(target);
        }
    }
}