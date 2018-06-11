// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead {

	public class ClientData : ScriptableObject {
		[Serializable]
		public struct AnnouncerSounds {
			public SoundCue _5MinRemaining;
			public SoundCue _1MinRemaining;
			public SoundCue _30SecRemaining;
			public SoundCue gameOn;
			public SoundCue gameOver;
			public SoundCue gameOverStinger;
			public SoundCue gameStartCampaign;
			public SoundCue gameStartHorde;
			public SoundCue victory;
			public SoundCue overtime;
			public SoundCue overtimeStinger;
			public SoundCue flagCaptured;
			public SoundCue flagLost;
			public SoundCue flagContested;
			public SoundCue flagUnderAttack;
			public SoundCue flagDestroyed;
			public SoundCue playerEliminated;
			public SoundCue teamEliminated;
			public SoundCue casualty;
			public SoundCue casualties;
			public SoundCue massCasualties;
			public SoundCue unitsReceived;
			public SoundCue unitsDetached;
			public SoundCue waveComplete;
			public SoundCue assassinsSpawnedStinger;
		}

		[Serializable]
		public struct GameSounds {
			public AnnouncerSounds announcer;
			public Actors.Spells.AbilitySounds abilitySounds;
			public SoundCue soulStoneAwarded;
			public SoundCue enterResurrectMode;
			public SoundCue eliteSeen;
			public SoundCue endMatchScreenShow;
			public SoundCue endMatchScreenClose;
		}

		[Serializable]
		public struct MainMenuSounds {
			public SoundCue menuTransition;
			public SoundCue lobbyPanelTransition;
			public SoundCue matchStart;
			public SoundCue matchAbort;
			public SoundCue roomJoined;
			public SoundCue roomLeft;
			public SoundCue playerJoinedRoom;
			public SoundCue playerLeftRoom;
			public SoundCue lobbyJoined;
			public SoundCue lobbyLeft;
			public SoundCue lobbyCreateError;
			public SoundCue playerJoinedLobby;
			public SoundCue playerLeftLobby;
			public SoundCue mainMenuStingers;
			public SoundCue mainMenuIntro;
			public SoundCue[] tierSelected;
			public SoundCue[] difficultySelected;
		}

		[Serializable]
		public struct SettingsMenuSounds {
			public SoundCue beginBindKey;
			public SoundCue didBindKey;
			public SoundCue cancelBindKey;
		}

		[Serializable]
		public struct UIDropDownListSounds {
			public SoundCue open;
			public SoundCue close;
			public SoundCue hover;
			public SoundCue select;
		}

		[Serializable]
		public struct UISpinnerSounds {
			public SoundCue up;
			public SoundCue down;
		}

		[Serializable]
		public struct UIAccordianSounds {
			public SoundCue fold;
			public SoundCue unfold;
			public SoundCue select;
		}

		[Serializable]
		public struct UIWidgetSounds {
			public UIDropDownListSounds dropDown;
			public UISpinnerSounds spinner;
			public UIAccordianSounds accordian;
			public SoundCue buttonClick;
		}

		[Serializable]
		public struct MissionSounds {
			public SoundCue objectiveAdded;
			public SoundCue objectiveSuccess;
			public SoundCue objectiveFailed;
			public SoundCue objectiveIncremented;
		}

		[Serializable]
		public struct UISounds {
			public MainMenuSounds mainMenu;
			public SettingsMenuSounds settings;
			public UIWidgetSounds widgets;
			public UITradingSounds trading;
			public MissionSounds missions;
			public SoundCue recvChatMsg;
			public SoundCue sendChatMsg;
		}

		[Serializable]
		public struct UITradingSounds {
			public SoundCue primarySpellSelected;
			public SoundCue secondarySpellSelected;
			public SoundCue gemSocketed;
			public SoundCue gemUnsocketed;
			public SoundCue runeSocketed;
			public SoundCue runeUnsocketed;
			public SoundCue itemVendored;
			public SoundCue dragGem;
			public SoundCue dragRune;
		}

		[Serializable]
		public struct Sounds {
			public UISounds ui;
			public GameSounds game;
		}

		[Serializable]
		public struct ItemQualityColors {
			public Color trash;
			public Color common;
			public Color rare;
			public Color epic;
			public Color legendary;
		}

		public Mesh decalUnitCube;
		public Material formationDecalMaterial;
		public Material formationInvalidDecalMaterial;
		public Material formationWaypointDecalMaterial;
		public Material formationFeedbackDecalMaterial;
		public Material attackLocationDecalMaterial;
		public GameObject orientDecal;
		public Material[] bloodDecalMaterials;
		public Material[] explosionDecalMaterials;
		public Texture2D[] bloodSplats;
		public SoundCue presetSaved;
		public SoundCue presetRecalled;
		public GameObject loadingCanvasPrefab;
		public GameObject minimapPrefab;
		public Canvas hudCanvasPrefab;
		public Canvas dialogDarkenCanvasPrefab;
		public Canvas dialogNormalCanvasPrefab;
		public Client.UI.MessageBox messageBoxPrefab;
		public Client.UI.LineEditDialog defaultLineEditorDialogPrefab;
		public Sprite playerConnecting;
		public Sprite playerUnknownDeity;
		public Sprite playerGenerating;
		public Sounds sounds;
		public ItemQualityColors itemQualityColors;
		public Texture2D pickupCursor;
	}
}
