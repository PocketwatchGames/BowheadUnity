// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(EditorReadonly))]
public class EditorReadonlyDrawer : PropertyDrawer {

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
		return EditorGUI.GetPropertyHeight(property, label, true);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		var old = GUI.enabled;
		GUI.enabled = false;
		EditorGUI.PropertyField(position, property, label, true);
		GUI.enabled = old;
	}

}
