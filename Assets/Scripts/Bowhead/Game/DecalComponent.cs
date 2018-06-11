// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

namespace Bowhead {
	[ExecuteInEditMode]
	public class DecalComponent : MonoBehaviour {

		public Material material;
		public bool permanent;
		Client.Decal _decal;
		Material _material;

		void OnEnable() {
#if UNITY_EDITOR
			if (!EditorApplication.isPlaying) {
				UpdateDecal(true);
			} else
#endif
			if (_decal != null) {
				UpdateDecal(true);
			} else {
				StartCoroutine(CreateDecal());
			}

		}

		protected virtual Material InstanceMaterial(Material original) {
			return original;
		}

		protected virtual void UpdateMaterialColor(Material material) { }

		System.Collections.IEnumerator CreateDecal() {
			yield return null;

			if (material != null) {
				_material = InstanceMaterial(material);
				UpdateMaterialColor(_material);
			}

			if (_material != null) {
				while (_decal == null) {
					var clworld = GameManager.instance.clientWorld;
					if (clworld != null) {
						if (Client.Actors.ClientPlayerController.localPlayer != null) {
							_decal = clworld.NewDecal(0f, DecalUpdate, transform.position, transform.localScale, transform.rotation, _material, true);
							break;
						}
					}
					yield return null;
				}
			}
		}

		void DecalUpdate(Client.Decal d, float dt) {
			UpdateDecal(true);
		}

		void OnDisable() {
			UpdateDecal(false);
		}

		void OnDestroy() {
			if ((_material != null) && (_material != material)) {
				GameObject.Destroy(_material);
			}

			if ((_decal != null) && !permanent) {
				_decal.Dispose();
			}
#if UNITY_EDITOR
			if (_editorDecal != null) {
				if (editorDecals != null) {
					editorDecals.RemoveDecal(_editorDecal);
					editorDecals.Rebuild(false, true);
				}
			}
#endif
		}

		protected void UpdateDecal(bool enable) {
			if (_material != null) {
				UpdateMaterialColor(_material);
			}

			if (_decal != null) {
				_decal.visible = enable && (_material != null);
				_decal.position = transform.position;
				_decal.rotation = transform.rotation;
				_decal.scale = transform.localScale;
			}
#if UNITY_EDITOR
			if (editorDecals != null) {
				bool changed = false;
				bool added = false;
				if (_editorDecal == null) {
					if (material != null) {
						_editorDecal = editorDecals.NewDecal(transform.position, transform.localScale, transform.rotation, _material, gameObject.activeInHierarchy);
						added = true;
					}
                } else {
					bool wasVisible = _editorDecal.visible;
					var pos = _editorDecal.position;
					var rot = _editorDecal.rotation;
					var scale = _editorDecal.scale;
					var mat = _editorDecal.material;

					_editorDecal.visible = enable && (material != null);
					_editorDecal.position = transform.position;
					_editorDecal.rotation = transform.rotation;
					_editorDecal.scale = transform.localScale;
					_editorDecal.material = material;

					changed = (wasVisible != _editorDecal.visible)
						|| (pos != _editorDecal.position)
						|| (rot != _editorDecal.rotation)
						|| (scale != _editorDecal.scale)
						|| (mat != _editorDecal.material);
				}
				if (added || changed) {
					editorDecals.Rebuild(added, changed);
				}
			}
#endif
		}

#if UNITY_EDITOR
		DeferredDecalRenderer.Decal _editorDecal;

		static DeferredDecalRenderer editorDecals;
		static Scene currentScene;

		[InitializeOnLoadMethod]
		static void EditorInit() {
			EditorEvents.OnEditorPlay += OnEditorPlay;
			EditorEvents.OnEditorStop += OnEditorStop;

			if (!EditorApplication.isPlayingOrWillChangePlaymode) {
				SetupEditorDecalPreview();
			}

			EditorApplication.hierarchyWindowChanged += OnHierarchyChanged;
		}

		static void OnEditorPlay() {
			if (editorDecals != null) {
				editorDecals.RemoveAllCameras();
			}
		}

		static void OnEditorStop() {
			SetupEditorDecalPreview();
		}

		static void OnHierarchyChanged() {
			if (currentScene != SceneManager.GetActiveScene()) {
				currentScene = SceneManager.GetActiveScene();
				SetupEditorDecalPreview();
			}
		}

		static void SetupEditorDecalPreview() {
			if (editorDecals != null) {
				editorDecals.RemoveAllCameras();
			} else {
				var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
				go.hideFlags = HideFlags.HideAndDontSave;
				var mesh = go.GetComponent<MeshFilter>().sharedMesh;
				GameObject.DestroyImmediate(go);
				editorDecals = new DeferredDecalRenderer(EDecalRenderMode.Unlit, mesh, "Editor Decals", 0);
			}

			if (editorDecals != null) {
				Camera[] cameras = FindObjectsOfType<Camera>();
				foreach (var c in cameras) {
					if (!c.CompareTag(Tags.UICamera)) {
						editorDecals.AddCamera(c);
					}
				}
			}
		}

		void AddDecalToCamera() {
			if ((Camera.current != null) && !Camera.current.CompareTag(Tags.UICamera) && (editorDecals != null)) {
				editorDecals.AddCamera(Camera.current);
			}
		}

		private void DrawGizmo(bool selected) {
			var col = new Color(0.0f, 0.7f, 1f, 1.0f);
			col.a = selected ? 0.3f : 0.1f;
			Gizmos.color = col;
			Gizmos.matrix = transform.localToWorldMatrix;
			Gizmos.DrawCube(Vector3.zero, Vector3.one);
			col.a = selected ? 0.5f : 0.2f;
			Gizmos.color = col;
			Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
		}

		public void OnDrawGizmos() {
			AddDecalToCamera();
		}

		public void OnDrawGizmosSelected() {
			AddDecalToCamera();
			DrawGizmo(true);
		}

		protected virtual void Update() {
			bool didUpdate = false;

			if (!EditorApplication.isPlayingOrWillChangePlaymode && (_editorDecal != null)) {
				if (gameObject.activeInHierarchy) {
					DecalUpdate(_decal, Time.unscaledDeltaTime);
					didUpdate = true;
				}
			}

			if (!didUpdate) {
				OnEditorUpdate();
			}
		}

		protected virtual void OnEditorUpdate() {}
#endif
	}
}