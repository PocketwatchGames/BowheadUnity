// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SoundClip))]
public sealed class SoundClipInspector : Editor {

	SerializedProperty _recycleThreshold;
	SerializedProperty _clips;
	
	void OnEnable() {
		_recycleThreshold = serializedObject.FindProperty("_recycleThreshold");
		_clips = serializedObject.FindProperty("_clips");
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();

		SoundClip clip = (SoundClip)target;
		EditorGUILayout.Slider(_recycleThreshold, 0, 100, "Recycle % Threshold");
		_recycleThreshold.floatValue = Mathf.FloorToInt(_recycleThreshold.floatValue);

		Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
		GUI.Box(dropArea, "Drag audio clips here (hold ctrl to replace all clips)");

		EditorGUILayout.PropertyField(_clips, true);

		DropAreaGUI(dropArea);

		serializedObject.ApplyModifiedProperties();
	}

	private void DropAreaGUI(Rect dropArea) {
		var evt = Event.current;

		if (evt.type == EventType.DragUpdated) {
			if (dropArea.Contains(evt.mousePosition)) {
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
			}
		}

		if (evt.type == EventType.DragPerform) {
			if (dropArea.Contains(evt.mousePosition)) {
				DragAndDrop.AcceptDrag();
				UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences as UnityEngine.Object[];
				var clips = new List<AudioClip>();
				foreach (var obj in draggedObjects) {
					var c = obj as AudioClip;
					if ((c != null) && !clips.Contains(c)) {
						clips.Add(c);
					}
				}

				clips.Sort((a, b) => a.name.CompareTo(b.name));

				SoundClip clip = (SoundClip)target;
				if (evt.control) {
					clip.InspectorSetClips(clips);
				} else {
					clip.InspectorAddClips(clips);
				}
			}
		}
	}

}
