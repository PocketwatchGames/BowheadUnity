// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System;

[CustomPropertyDrawer(typeof(WeakAssetRef), true)]
public class WeakAssetRefDrawer : PropertyDrawer {

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		var weakRef = (WeakAssetRef)property.GetValue();

		if (weakRef == null) {
			// may be in an array that's being resized.
			return;
		}

		var editorPath = property.FindPropertyRelative("_editorPath");
		var assetPath = property.FindPropertyRelative("_assetPath");

		UnityEngine.Object curObj = null;
		if (!string.IsNullOrEmpty(editorPath.stringValue)) {
			curObj = AssetDatabase.LoadAssetAtPath(editorPath.stringValue, weakRef.inspector_assetType);
		}

		var newObj = EditorGUI.ObjectField(position, property.displayName, curObj, weakRef.inspector_assetType, false);
		if (newObj != curObj) {
			if (newObj != null) {
				var newEditorPath = AssetDatabase.GetAssetPath(newObj);
				var newAssetPath = Utils.GetResourceRelativePath(newEditorPath);

				if (string.IsNullOrEmpty(newAssetPath)) {
					EditorUtility.DisplayDialog("Error", "Assets referenced in this field must be inside a Resources folder.", "OK");
				} else {
					editorPath.stringValue = newEditorPath;
					assetPath.stringValue = newAssetPath;
					weakRef.InspectorUpdate(newEditorPath, newAssetPath);
				}

			} else {
				editorPath.stringValue = string.Empty;
				assetPath.stringValue = string.Empty;
				weakRef.InspectorUpdate(string.Empty, string.Empty);
			}
		}
	}

	public static bool WeakRefField(string displayName, WeakAssetRef weakRef) {

		var editorPath = weakRef.inspectorEditorPath;
		var assetPath = weakRef.inspectorAssetPath;

		UnityEngine.Object curObj = null;
		if (!string.IsNullOrEmpty(editorPath)) {
			curObj = AssetDatabase.LoadAssetAtPath(editorPath, weakRef.inspector_assetType);
		}

		bool changed = false;

		var newObj = EditorGUILayout.ObjectField(displayName, curObj, weakRef.inspector_assetType, false);
		if (newObj != curObj) {
			if (newObj != null) {
				var newEditorPath = AssetDatabase.GetAssetPath(newObj);
				var newAssetPath = Utils.GetResourceRelativePath(newEditorPath);

				if (string.IsNullOrEmpty(newAssetPath)) {
					EditorUtility.DisplayDialog("Error", "Assets referenced in this field must be inside a Resources folder.", "OK");
				} else {
					weakRef.InspectorUpdate(newEditorPath, newAssetPath);
					changed = true;
				}

			} else {
				weakRef.InspectorUpdate(string.Empty, string.Empty);
				changed = true;
			}
		}

		return changed;
	}
}

public class WeakAssetRefProcessor : AssetPostprocessor {

	static bool autoUpdateWeakRefs = true;

	static WeakAssetRefProcessor() {
		EditorApplication.delayCall += () => {
			Menu.SetChecked("Bowhead/Auto Update Weak Refs", true);
		};
	}

	[MenuItem("Bowhead/Auto Update Weak Refs")]
	static void AutoUpdateWeakRefsMenu() {
		autoUpdateWeakRefs = !autoUpdateWeakRefs;
		Menu.SetChecked("Bowhead/Auto Update Weak Refs", autoUpdateWeakRefs);
	}

	static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
		if (!autoUpdateWeakRefs) {
			return;
		}

		if ((deletedAssets.Length > 0) || (movedAssets.Length > 0)) {

			bool anyDeleted = false;
			bool anyMoved = false;

			{
				
				foreach (var path in deletedAssets) {
					if (path.Contains("Resources/")) {
						anyDeleted = true;
						break;
					}
				}

				if (!anyDeleted) {
					foreach (var path in movedFromAssetPaths) {
						if (path.Contains("Resources/")) {
							anyMoved = true;
							break;
						}
					}
				}
			}

			if (!(anyMoved || anyDeleted)) {
				return;
			}

			LoadClasses();

			if (_classes == null) {
				return;
			}

			var paths = AssetDatabase.GetAllAssetPaths();

			if (paths.Length > 0) {
				var progress = new EditorProgressBar("Bowhead: Updating Asset References...", 1);
				var subProgress = new EditorProgressBar(null, paths.Length);

				foreach (var path in paths) {
					if ((path.Contains(".asset") || path.Contains(".prefab")) && !path.StartsWith("Assets/ThirdParty/")) {
						subProgress.Step(path);

						var objs = AssetDatabase.LoadAllAssetsAtPath(path);

						foreach (var obj in objs) {
							if (obj != null) {
								WeakRefField field;
								if (_classes.TryGetValue(obj.GetType(), out field)) {

									bool deleted = false;

									if (anyDeleted) {
										for (int i = 0; i < deletedAssets.Length; ++i) {
											if (FixupDelete(obj, field, deletedAssets[i])) {
												deleted = true;
											}
										}
									}

									bool moved = false;

									if (anyMoved) {
										for (int i = 0; i < movedAssets.Length; ++i) {
											if (FixupMove(obj, field, movedFromAssetPaths[i], movedAssets[i])) {
												moved = true;
											}
										}
									}

									if (moved || deleted) {
										EditorUtility.SetDirty(obj);
									}
								}
							}
						}
					} else {
						subProgress.Step(null);
					}
					
				}

				progress.Step(null);
				EditorUtility.UnloadUnusedAssetsImmediate();
			}
		}
	}

	static bool FixupMove(object obj, WeakRefField field, string src, string dst) {
		bool moved = false;

		foreach (var weakRefs in field.container.weakRefs) {
			var wref = (WeakAssetRef)weakRefs.GetValue(obj);
			if (wref != null) {
				if (wref.InspectorConditionalAssetMoved(src, dst)) {
					moved = true;
				}
			}
		}

		// descend into other container fields.
		foreach (var subField in field.subFields) {
			var val = subField.field.GetValue(obj);
			if (val != null) {
				moved = FixupMove(val, subField, src, dst) || moved;
			}
		}

		return moved;
	}

	static bool FixupDelete(object obj, WeakRefField field, string src) {
		bool deleted = false;

		foreach (var weakRefs in field.container.weakRefs) {
			var wref = (WeakAssetRef)weakRefs.GetValue(obj);
			if (wref != null) {
				if (wref.InspectorConditionalAssetDeleted(src)) {
					deleted = true;
				}
			}
		}

		// descend into other container fields.
		foreach (var subField in field.subFields) {
			var val = subField.field.GetValue(obj);
			if (val != null) {
				deleted = FixupDelete(val, subField, src) || deleted;
			}
		}

		return deleted;
	}

	static Dictionary<Type, WeakRefField> _classes;

	class WeakRefContainer {
		public List<FieldInfo> weakRefs = new List<FieldInfo>();
	}

	class WeakRefField {
		public FieldInfo field;
		public WeakRefContainer container;
		public List<WeakRefField> subFields = new List<WeakRefField>();
	}

	static void LoadClasses() {
		if (_classes != null) {
			return;
		}

		_classes = new Dictionary<Type, WeakRefField>();
		var containers = new Dictionary<Type, WeakRefContainer>();
		var inspected = new HashSet<Type>();

		var assemblies = AppDomain.CurrentDomain.GetAssemblies();
		
		foreach (var a in assemblies) {
			foreach (var t in a.GetTypes()) {
				if (typeof(ScriptableObject).IsAssignableFrom(t) ||
					typeof(MonoBehaviour).IsAssignableFrom(t)) {
					Descend(t, inspected, containers, null, null);
				}
			}
		}
	}

	static void Descend(Type type, HashSet<Type> inspected, Dictionary<Type, WeakRefContainer> containers, WeakRefField outer, FieldInfo outerField) {
		if (type.IsAbstract) {
			return;
		}

		if ((outer != null) && typeof(UnityEngine.Object).IsAssignableFrom(type)) {
			// don't descend into member fields that point to unity objects
			// if they are assets they will be dealt with as root level
			// types
			return;
		}

		var field = new WeakRefField();
		field.field = outerField;

		if (!inspected.Add(type)) {
			if (containers.TryGetValue(type, out field.container)) {
				if (outer != null) {
					outer.subFields.Add(field);
				}
			}
			return;
		}

		field.container = new WeakRefContainer();

		for (var innerType = type; (innerType != null) && (innerType != typeof(object)); innerType = innerType.BaseType) {
			var fields = innerType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);

			foreach (var f in fields) {
				if (typeof(WeakAssetRef).IsAssignableFrom(f.FieldType)) {
					field.container.weakRefs.Add(f);
				} else {
					Descend(f.FieldType, inspected, containers, field, f);
				}
			}
		}

		if ((field.container.weakRefs.Count > 0) || (field.subFields.Count > 0)) {
			if (field.container.weakRefs.Count > 0) {
				containers.Add(type, field.container);
			}

			if (outer != null) {
				outer.subFields.Add(field);
			} else {
				_classes.Add(type, field);
			}
		}
	}
}