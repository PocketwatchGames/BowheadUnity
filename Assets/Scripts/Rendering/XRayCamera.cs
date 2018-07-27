// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
	[RequireComponent(typeof(Camera))]
	public sealed class XRayCamera : ClientMonoBehaviour {
		[SerializeField]
		Material _xrayClip;
		[SerializeField]
		float _clipOffset;

		Camera _camera;

		int SHADER_CLIP0;
		int SHADER_CLIP1;

		static XRayCamera _instance;
		
		void Awake() {
			_instance = this;
			_camera = GetComponent<Camera>();

			SHADER_CLIP0 = Shader.PropertyToID("_ClipPlane0");
			SHADER_CLIP1 = Shader.PropertyToID("_ClipPlane1");
		}

		void OnPreCull() {
			Vector3 fwd = -transform.forward;
			fwd.y = 0;
			
			if (fwd.x != 0) {
				var n = 1 * Mathf.Sign(fwd.x);
				_xrayClip.SetVector(SHADER_CLIP0, new Vector4(n, 0, 0, (origin.x * n) + _clipOffset));
			} else {
				_xrayClip.SetVector(SHADER_CLIP0, new Vector4(0, 0, 0, 1));
			}

			if (fwd.z != 0) {
				var n = 1 * Mathf.Sign(fwd.z);
				_xrayClip.SetVector(SHADER_CLIP1, new Vector4(0, 0, n, (origin.z * n) + _clipOffset));
			} else {
				_xrayClip.SetVector(SHADER_CLIP1, new Vector4(0, 0, 0, 1));
			}
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			_instance = null;
		}

		public Vector3 origin;

		public static XRayCamera instance => _instance;
	}
}