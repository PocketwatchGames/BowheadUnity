// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(MinMaxSlider))]
public class MinMaxSliderDrawer : PropertyDrawer {

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		var attr = (MinMaxSlider)attribute;

		if (fieldInfo.FieldType == typeof(Vector2)) {
			property.vector2Value = GUILayoutHelpers.MinMaxSlider(position, label.text, property.vector2Value, attr.min, attr.max);
		} else {
			var boxed = property.GetValue();
			if (boxed != null) {
				var v = (IntMath.Vector2i)property.GetValue();
				var newV = GUILayoutHelpers.MinMaxSlider(position, label.text, v, Mathf.FloorToInt(attr.min), Mathf.FloorToInt(attr.max));

				if (newV != v) {
					property.SetValue(newV);
					EditorUtility.SetDirty(property.serializedObject.targetObject);
				}
			}
		}
	}

}
