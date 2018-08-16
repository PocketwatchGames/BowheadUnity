// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead {

	[CreateAssetMenu(fileName = "ClientData", menuName = "ClientData")]
	public sealed class ClientData : ScriptableObject {
		
		[Serializable]
		public struct GameSounds {
			
		}

		[Serializable]
		public struct MainMenuSounds {
			
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
		public struct MissionSounds {}

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
		public Material[] bloodDecalMaterials;
		public Material[] explosionDecalMaterials;
		public GameObject loadingCanvasPrefab;
		public GameObject mapFlagIconPrefab;
        public Client.UI.Map worldMapPrefab;
        public Client.UI.Compass compassPrefab;
        public Canvas hudCanvasPrefab;
		public Client.UI.EquipPanel hudEquipPrefab;
		public Client.UI.InventoryHUD hudInventoryPrefab;
		public Client.UI.ButtonHint hudButtonHintPrefab;
		public LockTargetHUD hudLockPrefab;
		public CameraData cameraDataCombat;
		public CameraData cameraDataExplore;
		public Material silhouetteStencil;
		public Material silhouetteFill;
		public Material[] xrayMaterials;
		public Material[] terrainMaterials;
        public DamageIndicator damageIndicatorPrefab;
        public WorldHUD worldHUDPrefab;
		public PawnHUD critterHudPrefab;
        public PlayerHUD playerHudPrefab;
        public DamageHUD damageHUDPrefab;
        public DirectionPreview directionPreviewPrefab;

        public bool isDualAnalogAiming;

		public Canvas dialogDarkenCanvasPrefab;
		public Canvas dialogNormalCanvasPrefab;
		public Client.UI.MessageBox messageBoxPrefab;
		public Client.UI.LineEditDialog defaultLineEditorDialogPrefab;
		public Sounds sounds;
		public ItemQualityColors itemQualityColors;
		public Texture2D pickupCursor;
		public WorldChunkComponent clientTerrainChunkComponent;
	}
}
