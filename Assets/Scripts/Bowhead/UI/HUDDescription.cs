// Copyright (c) 2018 Pocketwatch Games, LLC.

using UnityEngine;

namespace Bowhead.Client.UI {
	
	public enum EHUDScoreDisplayFormat {
		Number,
		Time,
		Percent
	}

	public class HUDDescription : ScriptableObject {
		public string gameTitle;
		public string gameDescription;
		public Sprite gameIcon;
		public Sprite resourceIcon;
		public Sprite resourceIcon2;
		public EHUDScoreDisplayFormat scoreFormat;
		public EHUDScoreDisplayFormat score2Format;
		//public HUDFFAWidgets ffaPrefab;
		//public HUDTeamWidgets teamPrefab;
		public bool pulseTeamOnResourceLoss;
		public bool pulseTeamOnResource2Loss;
		public Color initialOverlayColor;
		public bool spellBarInitiallyHidden;

		public string localizedGameTitle {
			get {
				return Utils.GetLocalizedText(gameTitle ?? "Untitled");
			}
		}

		public string localizedGameDescription {
			get {
				return Utils.GetLocalizedText(gameDescription ?? "No Description");
			}
		}
	}
}