// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Bowhead.Actors;

namespace Bowhead.Client.Actors {
	using Bowhead.Actors.Spells;

	public sealed class ClientPlayerController : PlayerController {

		[System.Flags]
		public enum EPlayerActionFlags {
			Stop = 0x1,
			Move = 0x2,
			Attack = 0x4,
			AssignPreset = 0x8,
			RecallPreset = 0x10,
			ChangeFormation = 0x20,
			Pan = 0x40,
			Orbit = 0x80,
			Zoom = 0x100
		}

		public const EPlayerActionFlags ALL_PLAYER_ACTIONS = EPlayerActionFlags.Stop|EPlayerActionFlags.Move|EPlayerActionFlags.Attack|EPlayerActionFlags.AssignPreset|EPlayerActionFlags.RecallPreset|EPlayerActionFlags.ChangeFormation|EPlayerActionFlags.Pan|EPlayerActionFlags.Orbit|EPlayerActionFlags.Zoom;

		public const int MAX_UMA_UPDATE_TIME_DURING_LOAD = 100;
		public const int MAX_UMA_UPDATE_TIME_DURING_GAME = 10;

		public const int MAX_WAYPOINTS = 64;
		public const float SVPERF_WARMUP_TIME = 5;
		public const float CAMERA_MIN_DIST = 16.36f;
		public const float CAMERA_MAX_DIST = 53.18f;
		public const float CAMERA_DELTA_DIST = CAMERA_MAX_DIST-CAMERA_MIN_DIST;
		public const float PRESET_SAVED_KEY_HOLD_TIME = 1f;

		const float STARTING_CAMERA_DISTANCE = 24.54f;
		const float STARTING_CAMERA_ANGLE = 30f;
		const float CAMERA_MAX_ANGLE = 85f;
		const float CAMERA_MIN_ANGLE = 25f;
		const float CAMERA_MOVE_LERP_SPEED = 8f;
		const float CAMERA_MINIMAP_DRAG_LERP_SPEED = 16f;
		const float CAMERA_ZOOM_LERP_SPEED = 8f;
		const float CAMERA_ORBIT_LERP_SPEED = 8f;
		const float PANNING_EDGE_SIZE = 0.025f;
		const float RAGDOLL_EXPLOSION_RATE_LIMIT = 0.5f;
		const int MAX_CONCURRENT_RAGDOLL_EXPLOSIONS = 1;
		const float CAMERA_SPHERE_MIN_SIZE = 0.25f;
		const float CAMERA_SPHERE_MAX_SIZE = 0.75f;
		
		static readonly Vector3 FORMATION_DECAL_SIZE = new Vector3(1.25f, 1.25f, 1.25f);

		GameplayInputActions _inputActions;
		ActorSingleton<GameState> _gameState;

		bool _didLevelStart;
		bool _didSpectate;
		bool _readyToPlay;
		bool _cameraReady;
		bool _minimapDrag;
		Vector2 _mapMins;
		Vector2 _mapMaxs;
		Camera _camera;
		int _formationIndex = -1;
		int _numUnitsInFormation;
		bool _debugFogOfWar;
		EPlayerActionFlags _validActions = ALL_PLAYER_ACTIONS;
		bool _pickupCursor;

		readonly ActorRPC rpc_Server_ClientHasLoaded;
		readonly ActorRPC<bool> rpc_Server_ReadyToPlay;
		readonly ActorRPC<string> rpc_Server_ExecuteCFunc;
		readonly ActorRPC<string> rpc_Server_Say;
		readonly ActorRPC<string> rpc_Server_SayTeam;

		static ClientPlayerController _localPlayer;

		public ClientPlayerController() {
			rpc_Server_ClientHasLoaded = BindRPC(Server_ClientHasLoaded);
			rpc_Server_ReadyToPlay = BindRPC<bool>(Server_ReadyToPlay);
			rpc_Server_ExecuteCFunc = BindRPC<string>(Server_ExecuteCFunc);
			rpc_Server_Say = BindRPC<string>(Server_Say);
			rpc_Server_SayTeam = BindRPC<string>(Server_SayTeam);
		}

		public override void PreConstruct(object outer) {
			base.PreConstruct(outer);
			SetRemoteRole(ERemoteRole.AutonomousProxy);

			clWorld = (ClientWorld)world;

			Assert.IsNull(_localPlayer);
			_localPlayer = this;
			_gameState = null;
			GameManager.instance.SetScreenModeCursorLockState();

			_inputActions = GameplayInputActions.LoadInputSettings();
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
		}

		protected override void OnRep_PlayerState() {
			gameState.hud.OnPlayerJoinGame(playerState);
		}

		public override void BeginTravel() {
			base.BeginTravel();
			_readyToPlay = false;
			_didLevelStart = false;
			_didSpectate = false;
			_cameraReady = false;
			_gameState = null;
			_minimapDrag = false;
			ShowPickupCursor(false);
		}

		public override void FinishTravel() {
			base.FinishTravel();
			_inputActions.Flush();
		}

		public override void Tick() {
			base.Tick();

			if (!Console.isOpen) {
				if (_inputActions.say.pressed) {
					OpenSay();
				}

				if (_inputActions.teamSay.pressed) {
					OpenTeamSay();
				}

				if (_inputActions.peekChat.pressed) {
					gameState.hud.PeekChat();
				}
			}

			if (_cameraReady) {
				UpdateCamera(world.deltaTime);

				if (Console.isOpen || (GameManager.instance.inMenus > 0)) {
					_inputActions.Flush();
				} else {
					ClickThrough.Push();
					TickPlayerInput(world.deltaTime, gameState.playerCanIssueCommands);
					ClickThrough.Pop();
				}
			}

			var wantPickupCursor = _pickupCursor;
			ShowPickupCursor(wantPickupCursor);
		}

		void ShowPickupCursor(bool show) {
			if (show != _pickupCursor) {
				_pickupCursor = show;
				if (show) {
					Cursor.SetCursor(GameManager.instance.clientData.pickupCursor, new Vector2(16, 16), CursorMode.Auto);
				} else {
					Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
				}
			}
		}

		public void InputSettingsChanged() {
			if (_inputActions != null) {
				_inputActions.Destroy();
			}

			_inputActions = GameplayInputActions.LoadInputSettings();
			gameState.hud.InputSettingsChanged(_inputActions);
		}

		public void SetValidActions(EPlayerActionFlags flags) {
			_validActions = flags;
		}

		bool CheckAction(EPlayerActionFlags flag) {
			return (_validActions&flag) != 0;
		}

		void LockCursor() {
			if (Cursor.lockState != CursorLockMode.Locked) {
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
				GameManager.instance.ClearMouseDelta();
			}
		}

		void UnlockCursor() {
			Cursor.visible = true;
			GameManager.instance.SetScreenModeCursorLockState();
		}

		void TickCameraInput(float dt) {
		}

		void Pan(float x, float y) {
		}

		void UpdateCamera(float dt) {
			if (!Console.isOpen && (GameManager.instance.inMenus <= 0) && gameState.playerCanMoveCamera) {
				TickCameraInput(dt);
			}
		}

		public override void GlobalCooldown(Ability instigator) {
		}

		void TickPlayerInput(float dt, bool canIssueCommands) {
		}

		[CFunc]
		void ShowName() {
			ConsolePrint(LogType.Warning, playerState.playerName);
		}

		public void Ready(bool ready) {
			rpc_Server_ReadyToPlay.Invoke(ready);
		}

		
		public override void OnLevelStart() {
			base.OnLevelStart();
			_didLevelStart = true;
		}

		void Spectate() {
			Debug.Log("Spectating...");

			_didSpectate = true;

			var spectatorCameras = GameObject.FindObjectsOfType<SpectatorCamera>();
			if (spectatorCameras.Length > 0) {
				var targetCamera = spectatorCameras[GameManager.instance.RandomRange(0, spectatorCameras.Length)];
				_camera.gameObject.SetActive(false);
				targetCamera.Spectate();
				clWorld.AddDecalRendererToCamera(targetCamera.GetComponent<Camera>());
			}

			GameManager.instance.CloseLoadingScreen();
			GameManager.instance.StartPlayOnAwakeSounds();

			//_presets = new List<Unit>[GameplayInputActions.NUM_PRESETS];

			//for (int i = 0; i < _presets.Length; ++i) {
			//	SaveUnitClassPreset(i);
			//}

			//((UMA.UMAGeneratorBuiltin)GameManager.instance.umaGenerator).maxUpdateTime = MAX_UMA_UPDATE_TIME_DURING_GAME;
			//((UMA.UMAGeneratorBuiltin)GameManager.instance.umaGenerator).gc = false;

			gameState.hud.OnPlayerSpectate();
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (_inputActions != null) {
				_inputActions.Destroy();
			}
			_localPlayer = null;
		}

		public void ExecuteServerCFunc(CFuncMethod cfuncMethod, string command) {
			if (cfuncMethod.cfunc.IsServer) {
				rpc_Server_ExecuteCFunc.Invoke(command);
			}
		}

		protected override void Owner_Say(PlayerState player, string text) {
			if ((gameState != null) && (gameState.hud != null)) {
				gameState.hud.OnPlayerChatMsg(player, text);
			}
		}

		protected override void Owner_SayTeam(PlayerState player, string text) {
			if ((gameState != null) && (gameState.hud != null)) {
				gameState.hud.OnPlayerTeamChatMsg(player, text);
			}
		}

		protected override void Owner_HUDDisplaySubtitle(string key, float stayTime) {
			if ((gameState != null) && (gameState.hud != null)) {
				gameState.hud.DisplaySubtitle(string.IsNullOrEmpty(key) ? null : Utils.GetLocalizedText(key), stayTime);
			}
		}

		public void Say(string text) {
			rpc_Server_Say.Invoke(text);
		}

		public void SayTeam(string text) {
			rpc_Server_SayTeam.Invoke(text);
		}

		protected override void Owner_Explosion(ExplosiveForce explosion) {
			if ((explosion.shockwaveLayers != 0) && (explosion.outer > 0f)) {
				var clWorld = (ClientWorld)world;
				var components = Physics.OverlapSphere(explosion.worldPos, explosion.outer, explosion.shockwaveLayers);
				List<RagdollController> actors = new List<RagdollController>();
				for (int i = 0; i < components.Length; ++i) {
					var c = components[i];
					var rb = c.GetComponent<Rigidbody>();
					if (rb != null) {
						var colliderCenter = c.GetWorldSpaceCenter();

						if ((explosion.blockingLayers == 0) || !Physics.Linecast(explosion.worldPos, colliderCenter, explosion.blockingLayers)) {
							var d = (explosion.worldPos - colliderCenter).magnitude;

                            if (d < explosion.inner) {
								d = explosion.inner;
							}
							if (d > explosion.outer) {
								d = 0f;
							} else {
								var r = explosion.outer - explosion.inner;
								if (r > 0f) {
									d = (d - explosion.inner) / r;

									if (explosion.falloff == ExplosionDamageClass.EFalloff.Exponential) {
										d = d*d;
									}

									d = Mathf.Lerp(explosion.innerForce, explosion.outerForce, d);
								} else {
									d = 0f;
								}
							}

							if (d > 0f) {
								bool rateLimited = false;

								Gib gib = null;
								var a = rb.transform.FindClientActorUpwards() as RagdollController;

								if (a == null) {
									gib = rb.GetComponent<Gib>();
									if (gib != null) {
										a = (RagdollController)gib;
									}
								}

								if (a != null) {
									if (!a.ragdollEnabled) {
										continue;
									}

									if (!actors.Contains(a)) {
										actors.Add(a);

										var dt = world.unscaledTime - a.lastRagdollExplosionTime;
										if (dt >= RAGDOLL_EXPLOSION_RATE_LIMIT) {
											a.ragdollExplosionRateLimited = false;
											a.numConcurrentRagdollExplosions = 1;
										} else {
											++a.numConcurrentRagdollExplosions;
											a.ragdollExplosionRateLimited = a.numConcurrentRagdollExplosions > MAX_CONCURRENT_RAGDOLL_EXPLOSIONS;
										}

										a.lastRagdollExplosionTime = world.unscaledTime;
									}

									rateLimited = a.ragdollExplosionRateLimited;
								}

								//if (gib == null) {
								//	var u = a as Unit;
								//	if (u != null) {
								//		var unitGib = u.gib;

								//		gib = u.ClientGibLimb(rb, d);
								//		if (gib != null) {
								//			if (!rateLimited && (unitGib != null)) {
								//				unitGib.AddExplosionForce(d, explosion.worldPos, explosion.ejection);
								//			}

								//			clWorld.RequestGibs(1);
								//			rateLimited = false;
								//			gib.numConcurrentRagdollExplosions = 1;
								//			gib.lastRagdollExplosionTime = world.unscaledTime;
								//			clWorld.GibAdded(gib);
								//		} else {
								//			gib = unitGib;
								//		}
								//	}
								//}

								//if (!rateLimited) {
								//	if (gib != null) {
								//		gib.AddExplosionForce(d, explosion.worldPos, explosion.ejection);
								//	} else {
								//		rb.isKinematic = false;
								//		rb.AddExplosionForce(d, explosion.worldPos, 0, explosion.ejection, ForceMode.Force);
								//	}
								//}
							}
						}
					}
				}
			}
		}

		[CFunc(Shortcuts = new[] { "lp" })]
		void ListPlayers() {
			foreach (var player in world.GetActorIterator<PlayerState>()) {
				Debug.LogWarning(player.playerName + " = " + player.netID);
			}
		}

		[CFunc(Shortcuts = new[] { "pl" } )]
		void ShowPermissionLevel() {
			Debug.LogWarning("Your permission level is " + playerState.permissionLevel + ".");
		}

		public void HUDPrint(string text) {
			if ((gameState != null) && (gameState.hud != null)) {
				gameState.hud.DisplayMessage(text);
			}
		}

		public void OpenSay() {
			gameState.hud.OpenSay();
		}

		public void OpenTeamSay() {
			gameState.hud.OpenTeamSay();
		}

		public float GetPingAdjustedTime(float time) {
			return Mathf.Max(time - (pingSeconds / 2f), 0f);
		}

		public static float GetLocalPlayerPingAdjustedTime(float time) {
			return Mathf.Max(time - (localPlayerPingSeconds / 2f), 0f);
		}

		public bool readyToPlay {
			get {
				return _readyToPlay;
			}
			set {
				readyToPlay = value;
			}
		}

		public bool wantsLockedCursor {
			get {
				return _inputActions.edgePanning;
			}
		}

		public ClientWorld clWorld {
			get;
			private set;
		}

		public GameState gameState {
			get {
				if (_gameState == null) {
					_gameState = new ActorSingleton<GameState>(world);
				}
				return _gameState;
			}
		}

		public GameplayInputActions inputActions {
			get {
				return _inputActions;
			}
		}

		public int pingMillis {
			get {
				return (ownerConnection != null) ? ownerConnection.ping : 0;
			}
		}

		public float pingSeconds {
			get {
				return pingMillis / 1000f;
			}
		}

		public static ClientPlayerController localPlayer {
			get {
				return (_localPlayer != null) && (_localPlayer.playerState != null) ? _localPlayer : null;
			}
		}

		public static bool IsLocalPlayer(PlayerState playerState) {
			var lp = localPlayer;
			return (lp != null) && (lp.playerState == playerState);
		}

		public static int localPlayerPingMillis {
			get {
				return (_localPlayer != null) ? _localPlayer.pingMillis : 0;
			}
		}

		public static float localPlayerPingSeconds {
			get {
				return localPlayerPingMillis / 1000f;
			}
		}

		[CFunc(Shortcuts = new[] { "dfg" })]
		bool DebugFogOfWar() {
			_debugFogOfWar = !_debugFogOfWar;
			return _debugFogOfWar;
		}

		public static bool debugFogOfWar {
			get {
				return (_localPlayer != null) ? _localPlayer._debugFogOfWar : false;
			}
		}
	}
}