// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead {
	[RequireComponent(typeof(Camera))]
	public sealed class XRayCamera : ClientMonoBehaviour {
		[SerializeField]
		float _clipOffset;
		[SerializeField]
		float _viewPlaneRadius;
		[SerializeField]
		float _worldSafeRadius;

		Camera _camera;

		static class ShaderID {
			static public readonly int ClipPlane0 = Shader.PropertyToID("_ClipPlane0");
			static public readonly int ClipPlane1 = Shader.PropertyToID("_ClipPlane1");
			static public readonly int ClipPlane2 = Shader.PropertyToID("_ClipPlane2");
			static public readonly int ClipOrigin = Shader.PropertyToID("_ClipOrigin");
			static public readonly int ClipRegion = Shader.PropertyToID("_ClipRegion");
			static public readonly int WorldOrigin = Shader.PropertyToID("_WorldOrigin");
		}
		
		public static Vector3 origin;
		static XRayCamera _instance;
		
		void Awake() {
			_instance = this;
			_camera = GetComponent<Camera>();
		}

		void OnPreCull() {
			//SetFloat(ShaderID.CylinderSizeSq, _cylinderSize*_cylinderSize);

			var fwd = (origin - transform.position).normalized;
			var fwdNoY = fwd;
			fwdNoY.y = 0;
			fwdNoY.Normalize();

			var left = Vector3.Cross(fwd, Vector3.up).normalized;
			var up = Vector3.Cross(fwd, left).normalized;

			//SetVector(ShaderID.ClipPlane0, new Vector4(fwdNoY.x, fwdNoY.y, fwdNoY.z, Vector3.Dot(fwdNoY, origin) + _fwdClipOffset));
			//SetVector(ShaderID.ClipPlane1, new Vector4(0, -1, 0, -origin.y - _yClipOffset));
			//SetVector(ShaderID.Up, new Vector4(up.x, up.y, up.z, Vector3.Dot(up, origin)));
			//SetVector(ShaderID.Left, new Vector4(left.x, left.y, left.z, Vector3.Dot(left, origin)));

			var projected = (Vector4)_camera.WorldToViewportPoint(origin);
			// clip space
			projected.x = (projected.x*2) - 1;
			projected.y = ((1-projected.y)*2) - 1;
			SetVector(ShaderID.ClipOrigin, projected);

			SetVector(ShaderID.WorldOrigin, origin);
			SetVector(ShaderID.ClipRegion, new Vector4(_viewPlaneRadius*_viewPlaneRadius, _worldSafeRadius, _camera.aspect, 0));
		}

		void SetVector(int name, Vector4 v) {
			var ms = GameManager.instance.clientData.xrayMaterials;
			foreach (var m in ms) {
				m.SetVector(name, v);
			}
		}

		void SetFloat(int name, float f) {
			var ms = GameManager.instance.clientData.xrayMaterials;
			foreach (var m in ms) {
				m.SetFloat(name, f);
			}
		}
	}
}