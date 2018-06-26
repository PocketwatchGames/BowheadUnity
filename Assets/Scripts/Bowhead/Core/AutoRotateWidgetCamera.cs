// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public class AutoRotateWidgetCamera : MonoBehaviour {

		[HideInInspector]
		[System.NonSerialized]
		public Quaternion widgetRotation;

		void LateUpdate() {
			widgetRotation = Quaternion.AngleAxis(transform.rotation.eulerAngles.y, Vector3.up) * Quaternion.AngleAxis(transform.rotation.eulerAngles.x, -Vector3.left);
		}
	}
}