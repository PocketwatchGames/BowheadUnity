// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class BowheadHUD : HUD {
		EquipPanel _equipHUD;
		InventoryHUD _inventoryHUD;
		ButtonHint _interactHint;
		LockTargetHUD _lockMarker;
		Map _worldmap;
        Compass _compass;

        GameObject _pawnHUDs;
        GameObject _playerHUD;
		IMapMarker _spawnMarker;

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
			_equipHUD = GameObject.Instantiate(GameManager.instance.clientData.hudEquipPrefab, hudCanvas.transform, false);
			_inventoryHUD = GameObject.Instantiate(GameManager.instance.clientData.hudInventoryPrefab, hudCanvas.transform, false);
			_interactHint = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, hudCanvas.transform, false);
			_lockMarker = GameObject.Instantiate(GameManager.instance.clientData.hudLockPrefab, hudCanvas.transform, false);
			_lockMarker.gameObject.SetActive(false);
			_worldmap = GameObject.Instantiate(GameManager.instance.clientData.worldMapPrefab, hudCanvas.transform, false);
			_compass = GameObject.Instantiate(GameManager.instance.clientData.compassPrefab, hudCanvas.transform, false);

			_pawnHUDs = new GameObject("PawnHuds");
			_pawnHUDs.transform.parent = hudCanvas.transform;

            _playerHUD = new GameObject("PlayerHud");
            _playerHUD.transform.parent = worldHUDCanvas.transform;

			_worldmap.transform.SetAsLastSibling(); // keep world-map on top of everything.
			_worldmap.gameObject.SetActive(false);

			_inventoryHUD.transform.SetAsLastSibling(); // keep world-map on top of everything.
			_inventoryHUD.gameObject.SetActive(false);

			world.CritterActiveEvent += OnCritterActive;
            world.DamageEvent += OnDamage;
            world.StatusEffectAddedEvent += OnStatusEffectAdded;


		}

		public override void Dispose() {
			base.Dispose();

			world.CritterActiveEvent -= OnCritterActive;
			world.DamageEvent -= OnDamage;
			world.StatusEffectAddedEvent -= OnStatusEffectAdded;
		}

		public override void OnPlayerPossessed(Player player) {
			base.OnPlayerPossessed(player);
			_equipHUD.Init(player);
            _compass.Init(Camera.main, player);

			_worldmap.SetStreaming(Bowhead.Server.BowheadGame.WORLD_GENERATOR_TYPE);
			_worldmap.SetOrigin(0, 0);

            //_minimap.SetOrigin(0, 0);
			player.OnExplore += OnExplore;

			// for now, minimap reveal still kinda broke ass.
			OnExplore(new Vector2(player.spawnPosition.x, player.spawnPosition.z), 128, false);

            var playerHUD = GameObject.Instantiate<PlayerHUD>(GameManager.instance.clientData.playerHudPrefab, _pawnHUDs.transform);
            playerHUD.SetTarget(player);
            var playerDecal = GameObject.Instantiate<PlayerDecal>(GameManager.instance.clientData.playerDecalPrefab, _playerHUD.transform);
			playerDecal.SetTarget(player);

			localPlayer.playerPawn.OnMerchantActivated += OnMerchantActivated;

			_inventoryHUD.Init(player);
		}

	

		public void OnCritterActive(Critter critter) {
			var critterHUD = GameObject.Instantiate<PawnHUD>(GameManager.instance.clientData.critterHudPrefab, _pawnHUDs.transform);
			critterHUD.SetTarget(critter);
			var critterDecal = GameObject.Instantiate<CritterDecal>(GameManager.instance.clientData.critterDecalPrefab, _playerHUD.transform);
			critterDecal.SetTarget(critter);
		}

		private void OnExplore(Vector2 pos, int radius, bool showMap) {
			var chunkPos = World.WorldToChunk(World.Vec3ToWorld(new Vector3(pos.x, 0, pos.y)));
            //_worldmap.SetOrigin(chunkPos.cx, chunkPos.cy);
            _worldmap.RevealArea(new Vector2(pos.x, pos.y), radius);
			if (_spawnMarker == null && GameManager.instance.clientData.mapFlagIconPrefab != null) {
				_spawnMarker = CreateMapMarker(GameManager.instance.clientData.mapFlagIconPrefab, EMapMarkerStyle.Normal);
			}
			_spawnMarker.worldPosition = pos;
			ShowWorldMap(showMap);
        }

		private void OnMerchantActivated(Pawn merchant) {
			ShowInventory(true, merchant);
		}

        private void OnDamage(Pawn target, float damage, bool directHit)
        {
			if (damage < 0.5f) {
				return;
			}
            var damageHUD = GameObject.Instantiate<DamageHUD>(GameManager.instance.clientData.damageHUDPrefab, _pawnHUDs.transform);
            damageHUD.Init(damage, 1.5f, directHit ? Color.red : Color.yellow, target);
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
			_interactHint.gameObject.SetActive(interaction != null);
            _interactHint.SetHint(interaction);
			_interactHint.SetButton(localPlayer.playerPawn.GetButtonHint("Y"));


			float angle;
			if (localPlayer.playerPawn.cur.fwd != 0 || localPlayer.playerPawn.cur.right != 0) {
				angle = Mathf.Atan2(localPlayer.playerPawn.cur.right, localPlayer.playerPawn.cur.fwd);
			} else {
				angle = localPlayer.playerPawn.yaw;
			}
			//var newTarget = localPlayer.playerPawn.GetAttackTarget(angle, 20, 360 * Mathf.Deg2Rad, null);
			//_lockMarker.gameObject.SetActive(newTarget != null);
			//if (newTarget != null) {
			//	_lockMarker.transform.position = Camera.main.WorldToScreenPoint(newTarget.headPosition());
			//}

			if (!inventoryVisible && (Input.GetButtonDown("Back") || (worldMapVisible && Input.GetButtonDown("B1")))) {
				ShowWorldMap(!worldMapVisible);
			}
			else if (!worldMapVisible && (Input.GetButtonDown("B1") || (inventoryVisible && Input.GetButtonDown("B1")))) {
				ShowInventory(!inventoryVisible, null);
			}
			else if (worldMapVisible && Input.GetButtonDown("X1")) {
				localPlayer.playerPawn.Teleport();
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
			localPlayer.playerPawn.FreezeMotion(show);
		}

		public override void ShowInventory(bool show, Pawn merchant) {
			if (show) {
				_inventoryHUD.SetMerchant(merchant);
			}
			_inventoryHUD.gameObject.SetActive(show);
			localPlayer.playerPawn.FreezeMotion(show);
		}

		public override bool worldMapVisible {
			get {
				return _worldmap.gameObject.activeSelf;
			}
		}
		public override bool inventoryVisible {
			get {
				return _inventoryHUD.gameObject.activeSelf;
			}
		}
	}
}