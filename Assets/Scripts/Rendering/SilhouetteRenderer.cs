// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bowhead {
	public sealed class SilhouetteRenderer : MonoBehaviour {
		public enum Mode {
			Off,
			Blocking,
			On
		};

		Renderer[] _rs;
		int[] _subMeshCount;
		Mode _mode;

		public void AddRenderer(CommandBuffer cmdBuffer, Material m) {
			if (_rs == null) {
				_rs = GetComponentsInChildren<Renderer>();
				_subMeshCount = new int[_rs.Length];

				for (int i = 0; i < _rs.Length; ++i) {
					_subMeshCount[i] = _rs[i].sharedMaterials.Length;
				}
			}

			for (int i = 0; i < _rs.Length; ++i) {
				var r = _rs[i];
				var count = _subMeshCount[i];
				for (int k = 0; k < count; ++k) {
					cmdBuffer.DrawRenderer(r, m, k);
				}
			}
		}

		public void SetDirty() {
			_rs = null;
			_subMeshCount = null;
			if (isActiveAndEnabled) {
				SilhouetteCamera.instance.SetDirty();
			}
		}

		void OnEnable() {
			SilhouetteCamera.instance.AddRenderer(this);	
		}

		void OnDisable() {
			if (SilhouetteCamera.instance != null) {
				SilhouetteCamera.instance.RemoveRenderer(this);
			}
		}

		public Mode mode {
			get {
				return _mode;
			}
			set {
				if (_mode != value) {
					_mode = value;
					enabled = _mode != Mode.Off;
					if (enabled) {
						SetDirty();
					}
				}
			}
		}
	}
}