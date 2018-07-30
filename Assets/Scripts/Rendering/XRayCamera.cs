// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowhead {
	[RequireComponent(typeof(Camera))]
	public sealed class XRayCamera : ClientMonoBehaviour {
		[SerializeField]
		float _fwdClipOffset;
		[SerializeField]
		float _yClipOffset;
		[SerializeField]
		float _cylinderSize;

		Camera _camera;

		static class ShaderID {
			static public readonly int ClipPlane0 = Shader.PropertyToID("_ClipPlane0");
			static public readonly int ClipPlane1 = Shader.PropertyToID("_ClipPlane1");
			static public readonly int Up = Shader.PropertyToID("_Up");
			static public readonly int Left = Shader.PropertyToID("_Left");
			static public readonly int CylinderSizeSq = Shader.PropertyToID("_CylinderSizeSq");
		}

		static readonly Color CLEAR_COLOR = new Color(0, 0, 0, 0);

		bool _dirty;
		CommandBuffer _cmdBuff;
		List<Renderer> _renderList;
		
		static XRayCamera _instance;
		
		void Awake() {
			_instance = this;
			_camera = GetComponent<Camera>();
		}

		void OnPreCull() {
			//Vector3 fwd = -transform.forward;

			//if (fwd.x != 0) {
			//	var n = 1 * Mathf.Sign(fwd.x);
			//	_xrayClip.SetVector(ShaderID.ClipPlane0, new Vector4(n, 0, 0, (origin.x * n) + _clipOffset));
			//} else {
			//	_xrayClip.SetVector(ShaderID.ClipPlane0, new Vector4(0, 0, 0, 1));
			//}

			//if (fwd.z != 0) {
			//	var n = 1 * Mathf.Sign(fwd.z);
			//	_xrayClip.SetVector(ShaderID.ClipPlane1, new Vector4(0, 0, n, (origin.z * n) + _clipOffset));
			//} else {
			//	_xrayClip.SetVector(ShaderID.ClipPlane1, new Vector4(0, 0, 0, 1));
			//}

			SetFloat(ShaderID.CylinderSizeSq, _cylinderSize*_cylinderSize);

			var fwd = (origin - transform.position).normalized;
			var fwdNoY = fwd;
			fwdNoY.y = 0;
			fwdNoY.Normalize();

			var left = Vector3.Cross(fwd, Vector3.up).normalized;
			var up = Vector3.Cross(fwd, left).normalized;

			SetVector(ShaderID.ClipPlane0, new Vector4(fwdNoY.x, fwdNoY.y, fwdNoY.z, Vector3.Dot(fwdNoY, origin) + _fwdClipOffset));
			SetVector(ShaderID.ClipPlane1, new Vector4(0, -1, 0, -origin.y - _yClipOffset));
			SetVector(ShaderID.Up, new Vector4(up.x, up.y, up.z, Vector3.Dot(up, origin)));
			SetVector(ShaderID.Left, new Vector4(left.x, left.y, left.z, Vector3.Dot(left, origin)));
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

		//void OnPreRender() {
		//	if (_dirty) {
		//		UpdateCommandBuffer();
		//		_dirty = false;
		//	}
		//}
		
		void UpdateCommandBuffer() {
			if (_cmdBuff == null) {
				_cmdBuff = new CommandBuffer() {
					name = "XRay"
				};
				_camera.AddCommandBuffer(CameraEvent.BeforeImageEffectsOpaque, _cmdBuff);
			} else {
				_cmdBuff.Clear();
			}

			//var xrayClip = GameManager.instance.clientData.xrayClip;

			//foreach (var r in _renderList) {
			//	_cmdBuff.DrawRenderer(r, xrayClip);
			//}			
		}

		bool Equals(RenderTextureDescriptor x, RenderTextureDescriptor y) {
			return (x.width == y.width) && (x.height == y.height) && (x.msaaSamples == y.msaaSamples);
		}

		RenderTextureDescriptor GetDescriptor() {
			RenderTextureDescriptor descriptor;

			var targetTexture = _camera.targetTexture;
			if (targetTexture != null) {
				descriptor = targetTexture.descriptor;
			} else {
				descriptor = new RenderTextureDescriptor(_camera.pixelWidth, _camera.pixelHeight, RenderTextureFormat.ARGB32, 24);
			}

			descriptor.colorFormat = RenderTextureFormat.ARGB32;
			descriptor.sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear;
			descriptor.useMipMap = false;
			descriptor.msaaSamples = 1;

			return descriptor;
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			_instance = null;
		}

		public Vector3 origin;

		public static void AddXRayRenderer(Renderer r) {
			//if (instance != null) {
			//	instance._renderList = instance._renderList ?? new List<Renderer>();
			//	instance._renderList.Add(r);
			//	instance._dirty = true;
			//}
		}

		public static void RemoveXRayRenderer(Renderer r) {
			//if ((instance != null) && (instance._renderList != null)) {
			//	instance._dirty = instance._renderList.Remove(r) || instance._dirty;
			//}
		}

		public static XRayCamera instance => _instance;
	}
}