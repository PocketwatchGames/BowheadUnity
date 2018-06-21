// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {

	public sealed class UITextTooltip : UIElementTooltip {
		public string text;
		[SerializeField]
		TextTooltipWidget _prefab;

		protected override Graphic CreateTooltip() {
			var tooltip = Instantiate(_prefab);
			tooltip.SetText(Utils.GetLocalizedText(text));
			return tooltip.graphic;
		}
	}
}
