// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(AudioSource))]
public class AudioSourceGC : MonoBehaviour {

	AudioSource _source;
	bool _didPlay;

	void Awake() {
		_source = GetComponent<AudioSource>();
#if UNITY_EDITOR
		if (!UnityEditor.EditorApplication.isPlaying) {
			UnityEditor.EditorApplication.update += Update;
		}
#endif
	}

	void OnDestroy() {
#if UNITY_EDITOR
		UnityEditor.EditorApplication.update -= Update;
#endif
	}

	void Update() {
		if (!_didPlay) {
			_didPlay = _source.isPlaying;
		}

		if (_didPlay && !_source.isPlaying) {
#if UNITY_EDITOR
			if (UnityEditor.EditorApplication.isPlaying) {
				Utils.DestroyGameObject(gameObject);
			} else {
				UnityEditor.EditorApplication.update -= Update;
				DestroyImmediate(gameObject);
			}
#else
			Utils.DestroyGameObject(gameObject);
#endif
		}
	}
}