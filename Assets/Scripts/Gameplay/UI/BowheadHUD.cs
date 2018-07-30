// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		InventoryPanel _inventory;
        ButtonHint _interactHint;
		Map _worldmap;
        Compass _compass;

        GameObject _pawnHUDs;
        GameObject _playerHUD;
        WeaponChargeHUD _weaponChargeLeft, _weaponChargeRight;

		class MapMarker : IMapMarker, IDisposable {
			public Transform mmMarker;
			public Transform wmMarker;

			public void SetAsFirstSibling() {
				if (mmMarker != null) {
					mmMarker.SetAsFirstSibling();
				}
				if (wmMarker != null) {
					wmMarker.SetAsFirstSibling();
				}
			}

			public Vector2 worldPosition {
				get {
					return mmMarker.localPosition;
				}
				set {
					if (mmMarker != null) {
						mmMarker.localPosition = value;
					}
					if (wmMarker != null) {
						wmMarker.localPosition = value;
					}
				}
			}

			public void Dispose() {
				if (mmMarker != null) {
					Utils.DestroyGameObject(mmMarker.gameObject);
				}
				if (wmMarker != null) {
					Utils.DestroyGameObject(wmMarker.gameObject);
				}
			}
		};

		public BowheadHUD(ClientWorld world, GameState gameState) : base(world, gameState) {
			_inventory = GameObject.Instantiate(GameManager.instance.clientData.hudInventoryPanelPrefab, hudCanvas.transform, false);
            _interactHint = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, hudCanvas.transform, false);
            _worldmap = GameObject.Instantiate(GameManager.instance.clientData.worldMapPrefab, hudCanvas.transform, false);
			_compass = GameObject.Instantiate(GameManager.instance.clientData.compassPrefab, hudCanvas.transform, false);
			_weaponChargeLeft = GameObject.Instantiate(GameManager.instance.clientData.weaponChargePrefab, hudCanvas.transform, false);
			_weaponChargeRight = GameObject.Instantiate(GameManager.instance.clientData.weaponChargePrefab, hudCanvas.transform, false);
			_pawnHUDs = new GameObject("PawnHuds");
			_pawnHUDs.transform.parent = hudCanvas.transform;

            _playerHUD = new GameObject("PlayerHud");
            _playerHUD.transform.parent = worldHUDCanvas.transform;

			_worldmap.transform.SetAsLastSibling(); // keep world-map on top of everything.
			_worldmap.gameObject.SetActive(false);

			world.CritterActiveEvent += OnCritterActive;
            world.DamageEvent += OnDamage;
            world.StatusEffectAddedEvent += OnStatusEffectAdded;
        }

        public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_inventory.Init(player);
            _compass.Init(Camera.main, player);

			_worldmap.SetStreaming(player.world.worldStreaming);
			_worldmap.SetOrigin(0, 0);

            //_minimap.SetOrigin(0, 0);
			player.OnExplore += OnExplore;

			// for now, minimap reveal still kinda broke ass.
			OnExplore(new Vector2(player.spawnPosition.x, player.spawnPosition.z), 64);

			_weaponChargeLeft.SetTarget(player, 1);
			_weaponChargeRight.SetTarget(player, 2);

            var playerHUD = GameObject.Instantiate<PlayerHUD>(GameManager.instance.clientData.playerHudPrefab, _pawnHUDs.transform);
            playerHUD.SetTarget(player);
            var directionPreview = GameObject.Instantiate<DirectionPreview>(GameManager.instance.clientData.directionPreviewPrefab, _playerHUD.transform);
            directionPreview.SetTarget(player);
        }

        public void OnCritterActive(Critter critter) {
			var critterHUD = GameObject.Instantiate<PawnHUD>(GameManager.instance.clientData.critterHudPrefab, _pawnHUDs.transform);
			critterHUD.SetTarget(critter);
		}

		private void OnExplore(Vector2 pos, float radius) {
			var chunkPos = World.WorldToChunk(World.Vec3ToWorld(new Vector3(pos.x, 0, pos.y)));
            _worldmap.SetOrigin(chunkPos.cx, chunkPos.cy);
            _worldmap.RevealArea(new Vector2(pos.x, pos.y), radius);
			if (GameManager.instance.clientData.mapFlagIconPrefab != null) {
				var marker = CreateMapMarker(GameManager.instance.clientData.mapFlagIconPrefab, EMapMarkerStyle.Normal);
				marker.worldPosition = pos;
			}
        }

        private void OnDamage(Pawn target, float damage)
        {
			if (damage < 0.5f) {
				return;
			}
            var damageHUD = GameObject.Instantiate<DamageHUD>(GameManager.instance.clientData.damageHUDPrefab, _pawnHUDs.transform);
            damageHUD.Init(damage, 1.5f, target);
        }
        private void OnStatusEffectAdded(Pawn target, StatusEffect e)
        {
            var damageHUD = GameObject.Instantiate<DamageHUD>(GameManager.instance.clientData.damageHUDPrefab, _pawnHUDs.transform);
            damageHUD.Init("+" + e.data.name, 20, 1.5f, target);
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
			_interactHint.SetButton("B");
            _interactHint.SetHint(interaction);

			if (Input.GetButtonDown("Start")) {
				ShowWorldMap(!worldMapVisible);
			}
        }

		public override IMapMarker CreateMapMarker<T>(T prefab, EMapMarkerStyle style) {
			var wmMarker = _worldmap.CreateMarker(prefab, style);

			return new MapMarker() {
				wmMarker = wmMarker.GetGameObject().transform
			};
		}

		public override void ShowWorldMap(bool show) {
			_worldmap.gameObject.SetActive(show);
		}

		public override bool worldMapVisible {
			get {
				return _worldmap.gameObject.activeSelf;
			}
		}
	}
}