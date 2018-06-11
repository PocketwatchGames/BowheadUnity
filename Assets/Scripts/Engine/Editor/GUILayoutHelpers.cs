// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using System.Collections.Generic;

public static class GUILayoutHelpers {

	static readonly GUIStyle horzLine;
	static readonly GUIStyle centerLabel;

	static GUILayoutHelpers() {
		horzLine = new GUIStyle(GUI.skin.box);
		horzLine.border.top = 1;
		horzLine.border.bottom = 1;
		horzLine.margin.top = 1;
		horzLine.margin.bottom = 1;
		horzLine.padding.top = 1;
		horzLine.padding.bottom = 1;

		centerLabel = new GUIStyle(GUI.skin.label);
		centerLabel.alignment = TextAnchor.UpperCenter;
	}

	public static void HorzLine() {
		GUILayout.Box(GUIContent.none, horzLine, GUILayout.ExpandWidth(true), GUILayout.Height(1f));
	}

	public static void CenterLabel(string text) {
		GUILayout.Label(text, centerLabel);
	}

	public static Rect VerticalLayout(ref Rect totalPosition) {
		var r = totalPosition;
		r.yMax = r.yMin + EditorGUIUtility.singleLineHeight;
		totalPosition.yMin = r.yMax;
		return r;
	}

	public static bool Foldout(bool foldout, string label, bool toggleOnLabelClick) {
		return EditorGUI.Foldout(EditorGUILayout.GetControlRect(), foldout, label, toggleOnLabelClick);
	}

	public static Vector2 MinMaxSlider(Rect rect, string label, Vector2 range, float min, float max) {
		const int IntBoxWidth = 30;

		var r = EditorGUI.PrefixLabel(rect, new GUIContent(label));
		var workRect = r;

		var oldIndent = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;

		workRect.xMax = workRect.xMin + IntBoxWidth;

		range.x = Mathf.Clamp(EditorGUI.FloatField(workRect, range.x), min, max);
		range.y = Mathf.Max(range.x, range.y);

		workRect = r;
		workRect.xMin += IntBoxWidth + 4;
		workRect.xMax -= IntBoxWidth + 4;

		EditorGUI.MinMaxSlider(workRect, ref range.x, ref range.y, min, max);

		workRect = r;
		workRect.xMin = workRect.xMax - IntBoxWidth;
		range.y = Mathf.Clamp(EditorGUI.FloatField(workRect, range.y), min, max);
		range.x = Mathf.Min(range.x, range.y);

		EditorGUI.indentLevel = oldIndent;
		return range;
	}

	public static Vector2 MinMaxSlider(string label, Vector2 range, float min, float max) {
		return MinMaxSlider(EditorGUILayout.GetControlRect(), label, range, min, max);
	}

	public static IntMath.Vector2i MinMaxSlider(Rect rect, string label, IntMath.Vector2i range, int min, int max) {
		const int IntBoxWidth = 30;

		var r = EditorGUI.PrefixLabel(rect, new GUIContent(label));
		var workRect = r;

		var oldIndent = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;

		workRect.xMax = workRect.xMin + IntBoxWidth;

		range.x = Mathf.Clamp(EditorGUI.IntField(workRect, range.x), min, max);
		range.y = Mathf.Max(range.x, range.y);

		workRect = r;
		workRect.xMin += IntBoxWidth + 4;
		workRect.xMax -= IntBoxWidth + 4;

		float x = range.x;
		float y = range.y;

		EditorGUI.MinMaxSlider(workRect, ref x, ref y, min, max);

		range.x = Mathf.FloorToInt(x);
		range.y = Mathf.FloorToInt(y);

		workRect = r;
		workRect.xMin = workRect.xMax - IntBoxWidth;
		range.y = Mathf.Clamp(EditorGUI.IntField(workRect, range.y), min, max);
		range.x = Mathf.Min(range.x, range.y);

		EditorGUI.indentLevel = oldIndent;

		return range;
	}

	public static IntMath.Vector2i MinMaxSlider(string label, IntMath.Vector2i range, int min, int max) {
		return MinMaxSlider(EditorGUILayout.GetControlRect(), label, range, min, max);
	}
}
