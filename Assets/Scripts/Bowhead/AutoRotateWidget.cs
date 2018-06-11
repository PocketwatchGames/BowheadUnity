// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	[RequireComponent(typeof(RectTransform))]
	public class AutoRotateWidget : MonoBehaviour {

		AutoRotateWidgetCamera _camera;
		RectTransform _widgetTransform;

		void Start() {
			BindCamera();
			_widgetTransform = GetComponent<RectTransform>();
		}

		void LateUpdate() {
			BindCamera();
			if (_camera != null) {
				_widgetTransform.rotation = _camera.widgetRotation;
			}
		}

		void BindCamera() {
			if (_camera == null) {
				var go = GameObject.FindGameObjectWithTag(Tags.MainCamera);
				if (go == null) {
					return;
				}
				_camera = go.GetComponent<AutoRotateWidgetCamera>();
			}
		}
	}
}