// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.Client.UI {

	public sealed class TMP_TextTooltipWidget : TextTooltipWidget {
		[SerializeField]
		TMPro.TextMeshProUGUI _text;

		public override void SetText(string text) {
			_text.text = text;
		}
	}
}