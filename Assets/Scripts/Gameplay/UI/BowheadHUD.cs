// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		InventoryPanel _inventory;
		PlayerStatePanel _playerState;
        ButtonHint _interactHint;
		Minimap _minimap;
        Compass _compass;

		GameObject _pawnHUDs;
		WeaponChargeHUD _weaponChargeLeft, _weaponChargeRight;

		public BowheadHUD(ClientWorld world, GameState gameState) : base(world, gameState) {
			_inventory = GameObject.Instantiate(GameManager.instance.clientData.hudInventoryPanelPrefab, hudCanvas.transform, false);
			_playerState = GameObject.Instantiate(GameManager.instance.clientData.hudPlayerStatePanelPrefab, hudCanvas.transform, false);
            _interactHint = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, hudCanvas.transform, false);
            _minimap = GameObject.Instantiate(GameManager.instance.clientData.minimapPrefab, hudCanvas.transform, false);
			_compass = GameObject.Instantiate(GameManager.instance.clientData.compassPrefab, hudCanvas.transform, false);
			_weaponChargeLeft = GameObject.Instantiate(GameManager.instance.clientData.weaponChargePrefab, hudCanvas.transform, false);
			_weaponChargeRight = GameObject.Instantiate(GameManager.instance.clientData.weaponChargePrefab, hudCanvas.transform, false);
			_pawnHUDs = new GameObject();
			_pawnHUDs.transform.parent = hudCanvas.transform;

			world.CritterActiveEvent += OnCritterActive;
        }

        public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_inventory.Init(player);
			_playerState.Init(player);
            _compass.Init(Camera.main, player);

			_minimap.SetStreaming(player.world.worldStreaming);
			player.OnExplore += OnExplore;

			// for now, minimap reveal still kinda broke ass.
			OnExplore(new Vector2(player.spawnPosition.x, player.spawnPosition.z), 1000);

			_weaponChargeLeft.SetTarget(player, 1);
			_weaponChargeRight.SetTarget(player, 2);
		}

		public void OnCritterActive(Critter critter) {
			var critterHUD = GameObject.Instantiate<PawnHUD>(GameManager.instance.clientData.critterHudPrefab, _pawnHUDs.transform);
			critterHUD.SetTarget(critter);
		}

		private void OnExplore(Vector2 pos, float radius) {
			var chunkPos = World.WorldToChunk(World.Vec3ToWorld(new Vector3(pos.x, 0, pos.y)));
            _minimap.SetOrigin(chunkPos.cx, chunkPos.cz);
            _minimap.RevealArea(new Vector2(pos.x, pos.y), radius);			
        }

        public override void Tick(float dt) {
            base.Tick(dt);

            Entity target;
            string interaction;
			Vector3? targetPos;
            localPlayer.playerPawn.GetInteractTarget(out target, out targetPos, out interaction);

			if (targetPos.HasValue) {
				_interactHint.SetTarget(targetPos.Value);
			}
			else {
				_interactHint.SetTarget(target);
			}
			_interactHint.SetButton("X");
            _interactHint.SetHint(interaction);

        }

		public override T CreateMinimapMarker<T>(T prefab) {
			return _minimap.CreateMarker(prefab);
		}
    }
}