// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bowhead.Client.UI {

	[RequireComponent(typeof(Graphic))]
	public abstract class UIElementTooltip : Tooltip, IPointerEnterHandler, IPointerExitHandler {
		public float delay;

		public void OnPointerEnter(PointerEventData eventData) {
			Show(delay);
		}

		public void OnPointerExit(PointerEventData eventData) {
			Hide();
		}
	}
}
