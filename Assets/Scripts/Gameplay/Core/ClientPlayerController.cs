// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using Bowhead.Actors;

namespace Bowhead.Client.Actors {

	public sealed class ClientPlayerController : PlayerController {

		GameplayInputActions _inputActions;
		ActorSingleton<GameState> _gameState;
		CameraController _cameraController;

		Vector2 _mapMins;
		Vector2 _mapMaxs;
		Camera _camera;
		bool _hideCursor = false;
		
		readonly ActorRPC rpc_Server_ClientHasLoaded;
		readonly ActorRPC<string> rpc_Server_ExecuteCFunc;
		readonly ActorRPC<string> rpc_Server_Say;
		readonly ActorRPC<string> rpc_Server_SayTeam;

		static ClientPlayerController _localPlayer;

		public ClientPlayerController() {
			rpc_Server_ClientHasLoaded = BindRPC(Server_ClientHasLoaded);
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

		protected override void OnRep_playerState() {
			gameState.hud.OnPlayerJoinGame(playerState);
		}

		protected override void OnRep_playerPawn() {
			base.OnRep_playerPawn();

			// this is a total fucking hack to avoid networking shit right now
			playerPawn = (Player)GameManager.instance.serverWorld.GetObjectByNetID(base.playerPawn.netID);

			gameState.hud.OnPlayerPossessed(playerPawn);
		}

		new public Player playerPawn {
			get;
			private set;
		}

		public override void BeginTravel() {
			base.BeginTravel();
			_gameState = null;
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

			if (Console.isOpen || (GameManager.instance.inMenus > 0)) {
				_inputActions.Flush();
			} else {
				ClickThrough.Push();
				TickPlayerInput(world.deltaTime, gameState.playerCanIssueCommands);
				ClickThrough.Pop();
			}

			//// FIXME: plug into input system properly later.
			//if (Input.GetKeyDown(KeyCode.C)) {
			//	_hideCursor = !_hideCursor;
   //             _cameraController.SetMouseLookActive(_hideCursor);
			//	SetCursorState();
			//}
		}

		public override void LateTick() {
			base.LateTick();
			UpdateCamera(world.deltaTime);
		}

		public void InputSettingsChanged() {
			if (_inputActions != null) {
				_inputActions.Destroy();
			}

			_inputActions = GameplayInputActions.LoadInputSettings();
			gameState.hud.InputSettingsChanged(_inputActions);
		}

		void UpdateCamera(float dt) {
			if (!Console.isOpen && (GameManager.instance.inMenus <= 0) && gameState.playerCanMoveCamera) {
				_cameraController.Update(dt);
			}
		}

		void TickPlayerInput(float dt, bool canIssueCommands) {
			// ANDY TODO
		}

		[CFunc]
		void ShowName() {
			ConsolePrint(LogType.Warning, playerState.playerName);
		}

		public override void OnLevelStart() {
			base.OnLevelStart();
			GameManager.instance.CloseLoadingScreen();
			GameManager.instance.StartPlayOnAwakeSounds();
			gameState.hud.OnLevelStart();
			_cameraController = new CameraController(Camera.main, GameManager.instance.clientData.cameraData);
			_cameraController.SetTarget(playerPawn);
            //_cameraController.SetMouseLookActive(_hideCursor);
            rpc_Server_ClientHasLoaded.Invoke();
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

		public void SetCursorState() {
			Cursor.visible = !_hideCursor;
			if (_hideCursor) {
				Cursor.lockState = CursorLockMode.Locked;
			} else {
				GameManager.instance.SetScreenModeCursorLockState();
			}
		}

		public ClientWorld clWorld {
			get;
			private set;
		}

		public CameraController cameraController {
			get {
				return _cameraController;
			}
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
	}
}