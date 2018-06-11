// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(EditorFlags))]
public class EditorFlagsDrawer : PropertyDrawer {

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		property.intValue = EditorGUI.MaskField(position, label, property.intValue, property.enumNames);
	}

}
