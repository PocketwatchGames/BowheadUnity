// Copyright (c) 2018 Pocketwatch Games LLC.
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

public delegate void EditorStateChanged();

[InitializeOnLoad]
public class EditorEvents {

	static bool isPlaying;

	public static EditorStateChanged OnEditorPlay;
	public static EditorStateChanged OnEditorStop;
	
	static EditorEvents() {
		EditorApplication.update += EditorTick;
	}

	static void EditorTick() {
		if (isPlaying != EditorApplication.isPlaying) {
			isPlaying = EditorApplication.isPlaying;
			if (isPlaying) {
				if (OnEditorPlay != null) {
					OnEditorPlay();
				}
			} else {
				if (OnEditorStop != null) {
					OnEditorStop();
				}
			}
		}
	}
}
#endif