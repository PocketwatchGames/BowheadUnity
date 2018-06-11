// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace Bowhead.Client.UI {

	[RequireComponent(typeof(Graphic))]
	public abstract class TooltipWidget : MonoBehaviourEx {
		Graphic _g;

		void Awake() {
			_g = GetComponent<Graphic>();
		}
		
		public Graphic graphic {
			get {
				return _g;
			}
		}
	}
}
