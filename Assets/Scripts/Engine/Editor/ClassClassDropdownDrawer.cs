// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(ClassDropdown))]
public class ClassDropdownDrawer : PropertyDrawer {

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

		var attr = (ClassDropdown)attribute;

		var wasEnabled = GUI.enabled;


		if (attr.Readonly) {
			GUI.enabled = false;
			property.stringValue = attr.types[0].FullName;
		}

		ClassDropDownHelper.ShowClassDropDown(position, attr.types, property.displayName, property);
		GUI.enabled = wasEnabled;
	}
}
