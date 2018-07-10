// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;
using Bowhead.Client.Actors;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	using EMatchState = Server.GameMode.EMatchState;

	public enum EMinimapMarkerStyle {
		Normal,
		AlwaysVisible
	};

	public abstract class HUD : System.IDisposable {
		GameState _gameState;
		ClientWorld _world;
		GameObject _hudRoot;
		Canvas _hudCanvas;
				
		public HUD(ClientWorld world, GameState gameState) {
			_world = world;
			_gameState = gameState;
			_hudCanvas = GameObject.Instantiate(GameManager.instance.clientData.hudCanvasPrefab);
		}

		public virtual void Initialize() {}

		public virtual void OnLevelStart() { }

		public virtual void Dispose() {}

		public virtual void Tick(float dt) {}

		public virtual void OnMatchStateChanged() {
			switch (gameState.matchState) {
				case EMatchState.WaitingForPlayers:
					OnMatchWaitingForPlayers();
				break;
				case EMatchState.MatchInProgress:
					OnMatchStart();
				break;
				case EMatchState.MatchOvertime:
					OnMatchOvertime();
				break;
				case EMatchState.MatchComplete:
					OnMatchComplete();
				break;
				case EMatchState.MatchFrozen:
					OnMatchFreeze();
				break;
				case EMatchState.MatchExit:
					OnMatchExit();
				break;
			}
		}

		public virtual void OnMatchTimer() { }

		public virtual void OnOvertimeEnabled() { }

		protected virtual void OnMatchWaitingForPlayers() { }
		protected virtual void OnMatchCountdown() { }
				
		protected virtual void OnMatchStart() {
			DragDropWidget.CancelDrag();
		}

		protected virtual void OnMatchOvertime() {}
		protected virtual void OnMatchComplete() {}
		protected virtual void OnMatchFreeze() { }
		protected virtual void OnMatchExit() { }
		public virtual void OpenInventory() { }

		public virtual void InputSettingsChanged(GameplayInputActions actions) {}

		public virtual void OnPlayerJoinGame(PlayerState player) {}
		public virtual void OnPlayerLeaveGame(PlayerState player) { }
		public virtual void OnPlayerLoaded(PlayerState player) { }
		public virtual void OnPlayerPossessed(Player player) { }

		public virtual void OpenSay() { }

		public virtual void OpenTeamSay() { }

		public virtual void PeekChat() { }

		public virtual void OnPlayerChatMsg(PlayerState player, string msg) { }

		public virtual void OnPlayerTeamChatMsg(PlayerState player, string msg) { }

		public virtual void OnSystemMessage(string msg) { }

		public virtual void DisplaySubtitle(string text, float stayTime) { }

		public virtual void DisplayMessage(string msg) { }

		public virtual void Fade(Color src, Color dst, float time) { }

		public abstract T CreateMinimapMarker<T>(T prefab, EMinimapMarkerStyle style) where T: UnityEngine.Object;
		
		protected virtual GameObject hudPrefab {
			get {
				return null;
			}
		}

		public GameState gameState {
			get {
				return _gameState;
			}
		}

		public ClientWorld world {
			get {
				return _world;
			}
		}

		public ClientPlayerController localPlayer {
			get {
				return ClientPlayerController.localPlayer;
			}
		}

		public GameObject hudRoot {
			get {
				return _hudRoot;
			}
		}

		public Canvas hudCanvas {
			get {
				return _hudCanvas;
			}
		}

		public Rect screenBounds {
			get {
				return new Rect(0, 0, Screen.width, Screen.height);
			}
		}
	}
}
