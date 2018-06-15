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

		bool _unitsTraded;
		bool _did5;
		bool _did1;
		bool _did30;
		float _damage;
		float _damageAlpha;
		float _damageTime;
		float _teamEliminatedDelay;
		float _playerEliminatedDelay;

		protected abstract void SetTime(string time);
				
		public HUD(ClientWorld world, GameState gameState) {
			_world = world;
			_gameState = gameState;

			_hudCanvas = GameObject.Instantiate(GameManager.instance.clientData.hudCanvasPrefab);
			_damageEffect = _hudCanvas.transform.Find("DamageEffectCanvas").GetComponent<CanvasGroup>();

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

			if (gameState.matchState < EMatchState.MatchComplete) {
				if (_teamEliminatedDelay > 0f) {
					_teamEliminatedDelay -= dt;
					if (_teamEliminatedDelay <= 0f) {
						VO_TeamEliminated();
					}
				}
				if (_playerEliminatedDelay > 0f) {
					_playerEliminatedDelay -= dt;
					if (_playerEliminatedDelay <= 0f) {
						VO_PlayerEliminated();
					}
				}
			}
        }

		public virtual void OnMatchStateChanged() {
			switch (gameState.matchState) {
				case EMatchState.WaitingForPlayers:
					OnMatchWaitingForPlayers();
				break;
				case EMatchState.Countdown:
					OnMatchCountdown();
				break;
				case EMatchState.UnitTrading:
					OnStartUnitTrading();
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

		public virtual void OnMatchTimer() {
			if (gameState.matchIsTimed) {
				AnnounceTime();
			}

			int secs = gameState.matchTimer;
			int hours = secs / (60*60);
			secs -= hours*60*60;
			int min = secs / 60;
			secs -= min*60;

			if (hours > 0) {
				SetTime(string.Format("{0}:{1:D2}:{2:D2}", hours, min, secs));
			} else if (min > 0) {
				SetTime(string.Format("{0}:{1:D2}", min, secs));
			} else {
				SetTime(string.Format("{0:D2}", secs));
			}
		}

		void AnnounceTime() {
			if (gameState.matchInProgress) {
				var secs = gameState.matchTimer;

				if (!_did5 && (gameState.matchPlayTime > (5*60)) && (secs <= (5*60))) {
					_did5 = true;
					VO_5Min();
				}
				if (!_did1 && (gameState.matchPlayTime > 60) && (secs <= 60)) {
					_did1 = true;
					VO_1Min();
				}
				if (!_did30 && (gameState.matchPlayTime > 30) && (secs <= 30)) {
					_did30 = true;
					VO_30Sec();
				}
			}
		}

		public virtual void OnOvertimeEnabled() { }

		protected virtual void OnMatchWaitingForPlayers() { }
		protected virtual void OnMatchCountdown() { }

		protected virtual void OnStartUnitTrading() {}


		protected virtual void OnMatchStart() {
			DragDropWidget.CancelDrag();
			VO_MatchStart();
		}

		protected virtual void OnMatchOvertime() {
			VO_Overtime();
		}

		protected virtual void OnMatchComplete() {
			VO_MatchComplete();
			//var endMatchScreen = GameObject.Instantiate(GameManager.instance.clientData.endMatchScreen);
			//endMatchScreen.transform.SetParent(_hudCanvas.transform, false);

			//if (ClientPlayerController.localPlayer.playerState.winner) {
			//	endMatchScreen.Init(Utils.GetLocalizedText("UI.HUD.Victory"));
			//} else {
			//	endMatchScreen.Init(Utils.GetLocalizedText("UI.HUD.GameOver"));
			//}
		}

		protected void QueuePlayerEliminatedVO() {
			if (_playerEliminatedDelay <= 0f) {
				_playerEliminatedDelay = 0.25f;
			}
		}

		protected void QueueTeamEliminatedVO() {
			if (_teamEliminatedDelay <= 0f) {
				_teamEliminatedDelay = 0.25f;
			}
		}

		protected virtual void OnMatchFreeze() { }
		protected virtual void OnMatchExit() { }

		protected virtual void VO_MatchStart() {
			GameManager.instance.Play(
				Vector3.zero, 
				gameState.isCampaignMap ? GameManager.instance.clientData.sounds.game.announcer.gameStartCampaign : 
				gameState.isHordeMap ? GameManager.instance.clientData.sounds.game.announcer.gameStartHorde : 
				GameManager.instance.clientData.sounds.game.announcer.gameOn);
		}

		protected virtual void VO_Overtime() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.overtime);
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.overtimeStinger);
		}

		protected virtual void VO_MatchComplete() {
			if (ClientPlayerController.localPlayer.playerState.winner) {
				GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.victory);
			} else {
				GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.gameOver);
			}
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.gameOverStinger);
		}

		protected virtual void VO_5Min() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer._5MinRemaining);
		}

		protected virtual void VO_1Min() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer._1MinRemaining);
		}

		protected virtual void VO_30Sec() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer._30SecRemaining);
		}

		public virtual void VO_Casualty() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.casualty);
		}

		public virtual void VO_Casualties() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.casualties);
		}

		public virtual void VO_MassiveCasualties() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.massCasualties);
		}

		protected virtual void VO_TeamEliminated() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.teamEliminated);
		}

		protected virtual void VO_PlayerEliminated() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.playerEliminated);
		}

		public virtual void VO_WaveComplete() {
			GameManager.instance.Play(Vector3.zero, GameManager.instance.clientData.sounds.game.announcer.waveComplete);
		}

		public virtual void OnLocalPlayerItemPickedUp(MetaGame.InventoryItemClass itemClass, int ilvl) {}
		public virtual void OpenInventory() { }

		public virtual void OnLocalPlayerAbilitySpawned(Ability ability, int index, string key) {
//#if !DEDICATED_SERVER
//			MetaGame.InventoryGrantAbilityItemClass itemClass = null;

//			if (gameState.isCOOPMap && (index >= HUDAbilityBar.RELIC_ABILITY_INDEX) && (index <= HUDAbilityBar.POTION_ABILITY_INDEX)) {
//				itemClass = GameManager.instance.clientInventory.GetUnlockedSpellItem(ability.abilityClass, ability.level);
//			}

//			_abilityBar.abilityWidgets[index].Attach(
//				ClientPlayerController.localPlayer.playerState,
//				itemClass,
//				GetLocalPlayerDeityClassForAbilityButtonSlot(index),
//				ability, 
//				key
//			);
//#endif
		}

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
		public virtual void OnPlayerSoulStonePointsChanged(PlayerState player) { }
		public virtual void OnTeamScoreChanged(Team team) { }
		public virtual void OnPlayerHealthChanged(PlayerState player) { }
		public virtual void OnPlayerJoinGame(PlayerState player) {}
		public virtual void OnPlayerLeaveGame(PlayerState player) { }
		public virtual void OnPlayerWinningChanged() { }
		public virtual void OnTeamWinningChanged() { }
		public virtual void OnPlayerLoaded(PlayerState player) { }

		public virtual void OnEnterResurrectionMode() {
			//if (_resurrectPrompt != null) {
			//	_resurrectPrompt.error = false;
			//	_resurrectPrompt.Show();
			//}
		}

		public virtual void OnExitResurrectionMode() {
			//if (_resurrectPrompt != null) {
			//	_resurrectPrompt.Hide();
			//}
		}

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

		public virtual void SetResurrectPromptError(bool error) {
			//if (_resurrectPrompt != null) {
			//	_resurrectPrompt.error = error;
			//}
		}

		public virtual void DamageBlend(float damage) {
			// we can get "damage" events when we are slotting items
			// that increase/decrease unit health, don't flash the hud in
			// those cases.
			if (gameState.matchState > EMatchState.UnitTrading) {
				_damage = Mathf.Min(_damage + damage, DAMAGE_OPACITY_SCALE);
				_damageTime = DAMAGE_BLEND_TIME;
			}
		}

		public virtual void DeathBlend() {
			DamageBlend(DAMAGE_OPACITY_SCALE);
		}

		public virtual void OnUnitsTraded() {
			_unitsTraded = true;
		}

		public virtual void OnSelectionChanged() {

			//if (_tradingPanel != null) {
			//	bool canTrade = false;
			//	bool canUntrade = false;

			//	for (int i = 0; i < localPlayer.selectedUnits.Count; ++i) {
			//		var u = localPlayer.selectedUnits[i];
			//		if (u.originalOwner == localPlayer.playerState) {
			//			if (u.owner != localPlayer.playerState) {
			//				canUntrade = true;
			//			} else {
			//				canTrade = true;
			//			}
			//		}
			//	}

			//	_tradingPanel.canTrade = canTrade && !canUntrade;
			//	_tradingPanel.canUntrade = canUntrade;
			//} else if (_inGameTradingPanel != null) {
			//	bool canTrade = false;

			//	for (int i = 0; i < localPlayer.selectedUnits.Count; ++i) {
			//		var u = localPlayer.selectedUnits[i];
			//		if (u.owner == localPlayer.playerState) {
			//			canTrade = true;
			//			break;
			//		}
			//	}

			//	_inGameTradingPanel.canTrade = canTrade;
			//}

			//if (_unitFrame != null) {
			//	if (localPlayer.selectedTarget != null) {
			//		_unitFrame.UpdateSelection(new[] { localPlayer.selectedTarget });
			//	} else {
			//		_unitFrame.UpdateSelection(localPlayer.selectedUnits);
			//	}
			//}
		}

		//public virtual void ConditionalUpdateUnitFrame(Unit unit, bool statsOnly) {
		//	if (_unitFrame != null) {
		//		_unitFrame.ConditionalUpdateSelectedUnit(unit, statsOnly);
		//	}
		//}

		//public void UpdateSelectedUnit() {
		//	if (_unitFrame != null) {
		//		_unitFrame.UpdateSelectedUnit();
		//	}
		//}

		public virtual void ShowGameModeDescription(bool show) {}

		public virtual void ShowPlayerDescription(bool show, int index) {}

//		protected void FillPlayerInfoPopup(HUDPlayerInfoPopup popup, PlayerState player) {
//			popup.playerNameAndGuild.text = player.playerName;

//			if (gameState.isCOOPMap) {
//				var xpTable = GameManager.instance.staticData.xpTable;
//				var xpBase = xpTable.GetXPReqForLevel(player.level);
//				var xpNext = xpTable.GetXPReqForLevel(player.level+1);
//				var xpDelta = xpNext - xpBase;
//				var xpCur = player.xp - xpBase;

//				popup.playerLevel.text = Utils.GetLocalizedText("UI.PlayerLevel", player.scaledLevel, xpCur, xpDelta);

//				if (player.scaledLevel > player.level) {
//					popup.playerLevelAdjustTextRoot.SetActive(true);
//					popup.playerLevelAdjustText.text = Utils.GetLocalizedText("UI.Player.Upleveled", player.level);
//				} else if (player.scaledLevel < player.level) {
//					popup.playerLevelAdjustTextRoot.SetActive(true);
//					popup.playerLevelAdjustText.text = Utils.GetLocalizedText("UI.Player.Downleveled", player.level);
//				} else {
//					popup.playerLevelAdjustTextRoot.SetActive(false);
//				}
//			} else {
//				if (popup.playerLevelRoot != null) {
//					popup.playerLevelRoot.SetActive(false);
//				}
//				if (popup.playerLevelAdjustTextRoot != null) {
//					popup.playerLevelAdjustTextRoot.SetActive(false);
//				}
//			}

//			if (popup.playerScore != null) {
//				popup.playerScore.text = player.score.ToString();
//			}

//			popup.playerArmyPct.text = Mathf.CeilToInt(player.health*100f) + "%";

//			if ((player.primaryDeity != null) && (player.secondaryDeity != null)) {
//				popup.deityRoot.SetActive(true);
//				if (player.primaryDeity != player.secondaryDeity) {
//					popup.deityName.text = player.primaryDeity.localizedName + " + " + player.secondaryDeity.localizedName;
//				} else {
//					popup.deityName.text = player.primaryDeity.localizedName;
//				}
//				List<AbilityClass> spells = new List<AbilityClass>();
//				if (gameState.isMPMap) {
//					spells.Add(player.primaryDeity.mpAbilities[0]);
//					spells.Add(player.primaryDeity.mpAbilities[1]);
//					spells.Add(player.secondaryDeity.mpAbilities[2]);
//				} else {
//					player.primaryDeity.GetMaskedSpells(player.primarySpells, spells, 2);
//					player.secondaryDeity.GetMaskedSpells(player.secondarySpells, spells, 1);
//				}

//				if (spells.Count > 0) {
//					FillSpellInfo(popup.spell1, spells[0], player.drop_ilvl);
//				}
//				if (spells.Count > 1) {
//					FillSpellInfo(popup.spell2, spells[1], player.drop_ilvl);
//				}
//				if (spells.Count > 2) {
//					FillSpellInfo(popup.spell3, spells[2], player.drop_ilvl);
//				}
//				FillSpellInfo(popup.relic, player.relic, player.reliciLvl);
//				FillSpellInfo(popup.potion, player.potion, player.potioniLvl);
//			} else {
//				popup.deityRoot.SetActive(false);
//			}
//		}

//		void FillSpellInfo(HUDPlayerSpellInfo panel, AbilityClass spell, int ilvl) {
//#if !DEDICATED_SERVER
//			MetaGame.InventoryGrantAbilityItemClass itemClass = null;

//			if (gameState.isCOOPMap) {
//				itemClass = GameManager.instance.clientInventory.GetUnlockedSpellItem(spell, ilvl);
//			}

//			panel.spellImage.sprite = (itemClass != null) ? itemClass.LoadIcon() : spell.icon.Load();

//			panel.spellDescription.text = (itemClass != null) ? itemClass.localizedName : (spell.localizedName + " - " + spell.localizedType);
//#endif
//		}

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

		//protected static string FormatScoreText(int score, EHUDScoreDisplayFormat format) {
		//	switch (format) {
		//		case EHUDScoreDisplayFormat.Percent:
		//			return score.ToString() + "%";
		//		case EHUDScoreDisplayFormat.Time: {
		//			int secs = score;
		//			int hours = secs / (60*60);
		//			secs -= hours*60*60;
		//			int min = secs / 60;
		//			secs -= min*60;

		//			if (hours > 0) {
		//				return string.Format("{0}:{1:D2}:{2:D2}", hours, min, secs);
		//			} else if (min > 0) {
		//				return string.Format("{0}:{1:D2}", min, secs);
		//			} else {
		//				return string.Format("{0:D2}", secs);
		//			}
		//		}
		//	}

		//	return score.ToString();
		//}
		
		GameObject hudPrefab {
			get {
				//if (gameState.isTeamMap) {
				//	return hudDescription.teamPrefab.gameObject;
				//}
				//return (hudDescription.ffaPrefab != null) ? hudDescription.ffaPrefab.gameObject : null;
				return null;
			}
		}

		public virtual HUDDescription hudDescription {
			get {
				if (_hudDescription == null) {
					_hudDescription = Resources.Load<HUDDescription>("HUDs/" + gameState.gameModeType.FullName);
				}
				return _hudDescription;
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

		//public Minimap minimap {
		//	get {
		//		return _minimap;
		//	}
		//}

		//public HUDAbilityBar abilityBar {
		//	get {
		//		return _abilityBar;
		//	}
		//}

		//public HUDMissionTracker missionTracker {
		//	get {
		//		return _missionTracker;
		//	}
		//}

		public Rect screenBounds {
			get {
				return new Rect(0, 0, Screen.width, Screen.height);
			}
		}
	}
}
