// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;
using Bowhead.Client.Actors;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	using EMatchState = Server.GameMode.EMatchState;

	public enum EMapMarkerStyle {
		Normal,
		AlwaysVisible
	};

	public interface IMapMarker {
		void SetAsFirstSibling();
		Vector2 worldPosition { get; set; }
	};

	public abstract class HUD : System.IDisposable {
		GameState _gameState;
		ClientWorld _world;
		GameObject _hudRoot;
        Canvas _hudCanvas;
        WorldHUD _worldHUDCanvas;

        public HUD(ClientWorld world, GameState gameState) {
			_world = world;
			_gameState = gameState;
            _hudCanvas = GameObject.Instantiate(GameManager.instance.clientData.hudCanvasPrefab);
            _worldHUDCanvas = GameObject.Instantiate(GameManager.instance.clientData.worldHUDPrefab);
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

		public abstract IMapMarker CreateMapMarker<T>(T prefab, EMapMarkerStyle style) where T: UnityEngine.Object;

		public abstract void ShowInventory(bool show);

		public abstract bool inventoryVisible { get; }

		public abstract void ShowWorldMap(bool show);

		public abstract bool worldMapVisible { get; }


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
        public WorldHUD worldHUDCanvas {
            get {
                return _worldHUDCanvas;
            }
        }

        public Rect screenBounds {
			get {
				return new Rect(0, 0, Screen.width, Screen.height);
			}
		}
	}
}
