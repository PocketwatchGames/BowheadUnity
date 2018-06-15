// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;
using Bowhead.Client.Actors;
using Bowhead.Actors.Spells;
using System.Collections.Generic;

namespace Bowhead.Client.UI {
	using EMatchState = Server.GameMode.EMatchState;

	public abstract class HUD : System.IDisposable {
		const float DAMAGE_DECAY_RATE = 1000f;
		const float DAMAGE_OPACITY_SCALE = 5000f;
		const float ALPHA_BLEND_SPEED = 100f;
		const float DAMAGE_BLEND_TIME = 1f;
		const float MESSAGE_STAY_TIME = 1.5f;
		const float MESSAGE_FADE_TIME = 1f;

		GameState _gameState;
		ClientWorld _world;
		GameObject _hudRoot;
		Canvas _hudCanvas;
		CanvasGroup _damageEffect;
		HUDDescription _hudDescription;

		float _damage;
		float _damageAlpha;
		float _damageTime;
		
		public HUD(ClientWorld world, GameState gameState) {
			_world = world;
			_gameState = gameState;

			_hudCanvas = GameObject.Instantiate(GameManager.instance.clientData.hudCanvasPrefab);
			//_damageEffect = _hudCanvas.transform.Find("DamageEffectCanvas").GetComponent<CanvasGroup>();

			//if (_hudCanvas != null) {
			//	var mmap = GameObject.Instantiate(GameManager.instance.clientData.minimapPrefab);
			//	mmap.transform.SetParent(_hudCanvas.transform, false);
			//	_minimap = new Minimap(mmap.transform);
			//	mmap.SetActive(false);

			//	_unitFrame = GameObject.Instantiate(GameManager.instance.clientData.unitFramePrefab);
			//	_unitFrame.transform.SetParent(_hudCanvas.transform, false);
			//	_unitFrame.gameObject.SetActive(false);


			//	if (gameState.hasSoulWell) {
			//		_abilityBar = GameObject.Instantiate(GameManager.instance.clientData.soulWellAbilityBarPrefab);
			//	} else {
			//		_abilityBar = GameObject.Instantiate(GameManager.instance.clientData.abilityBarPrefab);
			//	}

			//	_abilityBar.transform.SetParent(_hudCanvas.transform, false);
			//	_abilityBar.gameObject.SetActive(false);

			//	if (gameState.isCOOPMap) {
			//		_tradingPanel = GameObject.Instantiate(GameManager.instance.clientData.campaignTradingPrefab);
			//		_tradingPanel.transform.SetParent(_hudCanvas.transform, false);
			//		_tradingPanel.gameObject.SetActive(false);
			//	} else {
			//		_tradingPanel = GameObject.Instantiate(GameManager.instance.clientData.multiplayerTradingPrefab);
			//		_tradingPanel.transform.SetParent(_hudCanvas.transform, false);
			//		_tradingPanel.gameObject.SetActive(false);
			//	}

			//	_chatPanel = GameObject.Instantiate(GameManager.instance.clientData.inGameChatPanelPrefab);
			//	_chatPanel.transform.SetParent(_hudCanvas.transform, false);

			//	if (gameState.hasSoulWell) {
			//		_resurrectPrompt = GameObject.Instantiate(GameManager.instance.clientData.hudResurrectPromptPrefab);
			//		_resurrectPrompt.transform.SetParent(_hudCanvas.transform, false);
			//	}

			//	if (hudPrefab != null) {
			//		_hudRoot = GameObject.Instantiate(hudPrefab);
			//		_hudRoot.transform.SetParent(_hudCanvas.transform, false);
			//	}
				
			//	if (gameState.canTradeUnitsAnytime) {
			//		_inGameTradingPanel = GameObject.Instantiate(GameManager.instance.clientData.inGameTradingPrefab);
			//		_inGameTradingPanel.transform.SetParent(_hudCanvas.transform, false);
			//		_inGameTradingPanel.gameObject.SetActive(false);
			//	}

			//	if (gameState.gameModeConfig.missions != null) {
			//		_missionTracker = GameObject.Instantiate(GameManager.instance.clientData.hudMissionTracker);
			//		_missionTracker.transform.SetParent(_hudCanvas.transform, false);
			//		_missionTracker.transform.SetAsFirstSibling();
			//	}

			//	_messageWidgets = GameObject.Instantiate(GameManager.instance.clientData.hudMessageWidgetsPrefab);
			//	_messageWidgets.transform.SetParent(_hudCanvas.transform, false);

			//	_overlay = GameObject.Instantiate(GameManager.instance.clientData.hudOverlayPrefab);
			//	_overlay.transform.SetParent(_hudCanvas.transform, false);
			//	Fade(hudDescription.initialOverlayColor, hudDescription.initialOverlayColor, 0f);

			//}
		}

		public virtual void Initialize() {}

		void InitLocalPlayer() {
			foreach (var player in world.GetActorIterator<PlayerState>()) {
				//if (player.clientHUDPlayerWidget == null) {
				//	OnPlayerJoinGame(player);
				//}
				if (player.loaded) {
					OnPlayerLoaded(player);
				}
			}
		}

		public virtual void Dispose() {}

		public virtual void Tick(float dt) {
			_damageTime = Mathf.Max(0f, _damageTime - dt);
			_damage = Mathf.Max(_damage - DAMAGE_DECAY_RATE*dt, 0);

			var targetAlpha = Mathf.Clamp01(((_damage / DAMAGE_OPACITY_SCALE) * 0.5f) + 0.5f) * (_damageTime / DAMAGE_BLEND_TIME);
			_damageAlpha = Mathf.Lerp(_damageAlpha, targetAlpha, dt*ALPHA_BLEND_SPEED);
			//_damageEffect.alpha = _damageAlpha;
        }

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
		public virtual void OnLocalPlayerItemPickedUp(MetaGame.InventoryItemClass itemClass, int ilvl) {}
		public virtual void OpenInventory() { }

		public virtual void InputSettingsChanged(GameplayInputActions actions) {
			for (int i = 0; i < actions.spells.Length; ++i) {
				SetLocalPlayerAbilityKey(i, actions.spells[i].GetShortBindingText());
			}

			//for (int i = 0; i < actions.selectFormation.Length; ++i) {
			//	SetLocalPlayerFormationKey(i, actions.selectFormation[i].GetShortBindingText());
			//}

			//for (int i = 0; i < actions.presetRecall.Length; ++i) {
			//	SetLocalPlayerPresetKey(i, actions.presetRecall[i].GetShortBindingText());
			//}
		}

		void SetLocalPlayerAbilityKey(int index, string key) {
		}

		void SetLocalPlayerFormationKey(int index, string key) {
		}

		void SetLocalPlayerPresetKey(int index, string key) {
		}

		public void SetLocalPlayerPresetSprite(int index, Sprite sprite) {
		}

		public void SetSelectedFormationIndex(int index) {
		}

		public void ExecuteAbilitySlot(int index) {
		}

		public void FlashAbilitySlot(int index, bool flash) {
		}

		public void FlashSoulBar() {
		}

		public void FlashFormation(int index, bool flash) {
		}

		public void FlashMinimap(bool flash) {
		}

		public void FlashSquadPreset(int index, bool flash) {
		}

		public void ShowSpellBar(bool show) {
		}

		public virtual void OnPlayerSpectate() {
			foreach (var player in world.GetActorIterator<PlayerState>()) {
				OnPlayerJoinGame(player);
				if (player.loaded) {
					OnPlayerLoaded(player);
				}
			}
		}

		public virtual void OnPlayerAbilitiesChanged(PlayerState player) {
			/*
#if !DEDICATED_SERVER
			if (player.loaded && (player.clientHUDPlayerWidget != null)) {
				if ((player.primaryDeity != null) && (player.primaryDeity.icon != null)) {
					player.clientHUDPlayerWidget.playerAvatar.sprite = player.primaryDeity.icon.Load();
				}
				if ((player.secondaryDeity != null) && (player.secondaryDeity.icon2 != null)) {
					player.clientHUDPlayerWidget.playerAvatar2.sprite = player.secondaryDeity.icon2.Load();
					player.clientHUDPlayerWidget.deityColor.color = player.secondaryDeity.color;
				}
			}

			if (ClientPlayerController.localPlayer.playerState == player) {
				int numPassiveSoulstones = 0;

				List<AbilityClass> spells = new List<AbilityClass>();

				if (gameState.isMPMap) {
					spells.Add(player.primaryDeity.mpAbilities[0]);
					spells.Add(player.primaryDeity.mpAbilities[1]);
					spells.Add(player.secondaryDeity.mpAbilities[2]);
				} else {
					player.primaryDeity.GetMaskedSpells(player.primarySpells, spells, 2);
					player.secondaryDeity.GetMaskedSpells(player.secondarySpells, spells, 1);
				}

				var spellPower = player.spellPower;

				for (int i = 0; i < spells.Count; ++i) {
					var spell = spells[i];
					numPassiveSoulstones += spell.passiveSoulStoneCost;
					_abilityBar.abilityWidgets[i].SetTradingPreview(player, null, GetLocalPlayerDeityClassForAbilityButtonSlot(i), spell, spellPower, player.drop_ilvl, ClientPlayerController.localPlayer.inputActions.spells[i].GetShortBindingText());
				}

				if (player.relic != null) {
					MetaGame.InventoryGrantAbilityItemClass itemClass = null;

					if (gameState.isCOOPMap) {
						itemClass = GameManager.instance.clientInventory.GetUnlockedSpellItem(player.relic, player.reliciLvl);
					}

					numPassiveSoulstones += player.relic.passiveSoulStoneCost;
					_abilityBar.abilityWidgets[HUDAbilityBar.RELIC_ABILITY_INDEX].SetTradingPreview(player, itemClass, null, player.relic, GameManager.instance.staticData.xpTable.GetSpellPower(player.reliciLvl), player.reliciLvl, ClientPlayerController.localPlayer.inputActions.spells[HUDAbilityBar.RELIC_ABILITY_INDEX].GetShortBindingText());
				}

				if (player.potion != null) {
					MetaGame.InventoryGrantAbilityItemClass itemClass = null;

					if (gameState.isCOOPMap) {
						itemClass = GameManager.instance.clientInventory.GetUnlockedSpellItem(player.potion, player.potioniLvl);
					}

					numPassiveSoulstones += player.potion.passiveSoulStoneCost;
					_abilityBar.abilityWidgets[HUDAbilityBar.POTION_ABILITY_INDEX].SetTradingPreview(player, itemClass, null, player.potion, GameManager.instance.staticData.xpTable.GetSpellPower(player.potioniLvl), player.potioniLvl, ClientPlayerController.localPlayer.inputActions.spells[HUDAbilityBar.POTION_ABILITY_INDEX].GetShortBindingText());
				}

				var gameModeConfig = ClientPlayerController.localPlayer.gameState.gameModeConfig;
				var auxSpell = gameModeConfig.auxilliarySpell;
				if ((HUDAbilityBar.AUXILLIARY_ABILITY_INDEX < _abilityBar.abilityWidgets.Length) && (auxSpell != null)) {
					numPassiveSoulstones += auxSpell.passiveSoulStoneCost;
					_abilityBar.abilityWidgets[HUDAbilityBar.AUXILLIARY_ABILITY_INDEX].SetTradingPreview(player, null, null, auxSpell, spellPower, player.drop_ilvl, ClientPlayerController.localPlayer.inputActions.spells[HUDAbilityBar.AUXILLIARY_ABILITY_INDEX].GetShortBindingText());
				}

				int maxSoulStones = Mathf.Max(0, PlayerController.MAX_SOULSTONES - numPassiveSoulstones);
				_abilityBar.SetMaxSoulStones(maxSoulStones);
				_abilityBar.SetSoulStoneTradingCount(gameModeConfig.startingSoulStoneCount);

				if (player.clientHUDPlayerWidget != null) {
					player.clientHUDPlayerWidget.SetSoulStoneCount(Mathf.FloorToInt(gameModeConfig.startingSoulStoneCount));
				}
			}
#endif
			*/
		}

		public virtual void OnPlayerScoreChanged(PlayerState player) { }
		
		public virtual void OnTeamScoreChanged(Team team) { }
		public virtual void OnPlayerHealthChanged(PlayerState player) { }
		public virtual void OnPlayerJoinGame(PlayerState player) {}
		public virtual void OnPlayerLeaveGame(PlayerState player) { }
		public virtual void OnPlayerWinningChanged() { }
		public virtual void OnTeamWinningChanged() { }
		public virtual void OnPlayerLoaded(PlayerState player) { }

		public virtual void OpenSay() {
			//_chatPanel.OpenSay();
		}

		public virtual void OpenTeamSay() {
			//_chatPanel.OpenTeamSay();
		}

		public virtual void PeekChat() {
			//_chatPanel.PeekChat();
		}

		public virtual void OnPlayerChatMsg(PlayerState player, string msg) {
			//_chatPanel.OnSay(player, msg);
		}

		public virtual void OnPlayerTeamChatMsg(PlayerState player, string msg) {
			//_chatPanel.OnTeamSay(player, msg);
		}

		public virtual void OnSystemMessage(string msg) {
			//_chatPanel.OnSystemMessage(msg);
		}

		public virtual void DamageBlend(float damage) {
			// we can get "damage" events when we are slotting items
			// that increase/decrease unit health, don't flash the hud in
			// those cases.
			if (gameState.matchState > EMatchState.WaitingForPlayers) {
				_damage = Mathf.Min(_damage + damage, DAMAGE_OPACITY_SCALE);
				_damageTime = DAMAGE_BLEND_TIME;
			}
		}

		public virtual void DeathBlend() {
			DamageBlend(DAMAGE_OPACITY_SCALE);
		}

		public virtual void OnSelectionChanged() {}

		public virtual void ShowPlayerDescription(bool show, int index) {}

		public virtual void DisplaySubtitle(string text, float stayTime) {
			//if (text == null) {
			//	_messageWidgets.subtitleCanvas.FadeOut(MESSAGE_FADE_TIME, false);
			//} else {
			//	_messageWidgets.subtitleText.text = text;
			//	_messageWidgets.subtitleCanvas.FadeInOut(0f, stayTime, MESSAGE_FADE_TIME, false);
			//}
		}

		public virtual void DisplayMessage(string msg) {
			//_messageWidgets.messageText.SetText(msg, MESSAGE_STAY_TIME, MESSAGE_FADE_TIME);
		}

		public virtual void Fade(Color src, Color dst, float time) {
			//_overlay.Fade(src, dst, time);
		}

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
