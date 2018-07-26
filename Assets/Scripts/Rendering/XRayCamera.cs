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
		float _yOffset;
		[SerializeField]
		float _backClipOffset;

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
			_xrayClip.SetVector(SHADER_CLIP0, new Vector4(0, -1, 0, -origin.y - _yOffset));
			{
				Vector3 fwd = -transform.forward;
				fwd.y = 0;
				fwd = fwd.normalized;
				Vector4 plane = new Vector4(fwd.x, fwd.y, fwd.z, Vector3.Dot(fwd, origin) - _backClipOffset);
				_xrayClip.SetVector(SHADER_CLIP1, plane);
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