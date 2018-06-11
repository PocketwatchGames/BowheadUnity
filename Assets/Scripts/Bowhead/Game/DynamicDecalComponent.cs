// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

namespace Bowhead {
	public class DynamicDecalComponent : DecalComponent {

		public Color color;

		int COLOR;

		void Awake() {
			COLOR = Shader.PropertyToID("_Color");
		}

		protected override void UpdateMaterialColor(Material material) {
			material.SetColor(COLOR, color);
		}

		protected override Material InstanceMaterial(Material original) {
			return new Material(original);
		}

#if UNITY_EDITOR
		protected override void OnEditorUpdate() {
			base.OnEditorUpdate();
			UpdateDecal(true);
		}

		void Reset() {
			color = Color.white;
		}
#else
		void Update() {
			UpdateDecal(true);
		}
#endif

	}
}