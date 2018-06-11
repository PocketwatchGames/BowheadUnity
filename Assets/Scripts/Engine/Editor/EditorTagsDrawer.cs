// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(EditorTags))]
public class EditorTagsDrawer : PropertyDrawer {

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		int index = 0;
		for (int i = 0; i < Utils.tagNames.Length; ++i) {
			if (property.stringValue == Utils.tagNames[i]) {
				index = i;
				break;
			}
		}

		property.stringValue = Utils.tagNames[EditorGUI.Popup(position, property.displayName, index, Utils.tagNames)];
	}

}
