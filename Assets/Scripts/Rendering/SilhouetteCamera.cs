// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowhead {
	[RequireComponent(typeof(Camera))]
	public sealed class SilhouetteCamera : ClientMonoBehaviour {

		Camera _camera;
		bool _dirty;
		List<SilhouetteRenderer> _renderers;
		CommandBuffer _stencil;
		CommandBuffer _fill;

		public static SilhouetteCamera instance;

		void Awake() {
			_camera = GetComponent<Camera>();	
			instance = this;
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			instance = null;
		}

		public void AddRenderer(SilhouetteRenderer r) {
			if (_renderers == null) {
				_renderers = new List<SilhouetteRenderer>();
			}
			_renderers.Add(r);
			_dirty = true;
		}

		public void RemoveRenderer(SilhouetteRenderer r) {
			_renderers.Remove(r);
			_dirty = false;
		}

		public void SetDirty() {
			_dirty = true;
		}

		void OnPreRender() {
			if (_dirty) {
				if (_stencil != null) {
					_camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _stencil);
					_stencil.Dispose();
					_stencil = null;
				}
				if (_fill != null) {
					_camera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _fill);
					_fill.Dispose();
					_fill = null;
				}

				if ((_renderers != null) && (_renderers.Count > 0)) {
					_stencil = new CommandBuffer() {
						name = "Silhouette Stencil"
					};
					_fill = new CommandBuffer() {
						name = "Silhouette Fill"
					};

					var stencil = GameManager.instance.clientData.silhouetteStencil;
					var fill = GameManager.instance.clientData.silhouetteFill;

					foreach (var r in _renderers) {
						r.AddRenderer(_stencil, stencil);
						if (r.mode == SilhouetteRenderer.Mode.On) {
							r.AddRenderer(_fill, fill);
						}
					}

					_camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _stencil);
					_camera.AddCommandBuffer(CameraEvent.BeforeImageEffects, _fill);
				}

				_dirty = false;
			}
		}
	}
}