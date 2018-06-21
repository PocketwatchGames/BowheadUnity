// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead {

	public class ClientData : ScriptableObject {
		
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
		public GameObject minimapPrefab;
		public Canvas hudCanvasPrefab;
		public Client.UI.UIController hudUIController;
		public Canvas dialogDarkenCanvasPrefab;
		public Canvas dialogNormalCanvasPrefab;
		public Client.UI.MessageBox messageBoxPrefab;
		public Client.UI.LineEditDialog defaultLineEditorDialogPrefab;
		public Sounds sounds;
		public ItemQualityColors itemQualityColors;
		public Texture2D pickupCursor;
		public World_ChunkComponent clientTerrainChunkComponent;
	}
}
