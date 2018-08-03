// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SoundCue))]
public sealed class SoundCueInspector : Editor {
	

	SerializedProperty _playChance;
	SerializedProperty _volume;
	SerializedProperty _pitch;
	SerializedProperty _reverb;
	SerializedProperty _loop;
	SerializedProperty _clipRef;
	SerializedProperty _audioSourcePrefab;
	SoundClip_WRef _clip;
	SoundClip _loaded;
	AudioSource _source;

	void OnEnable() {
		_playChance = serializedObject.FindProperty("_playChance");
		_volume = serializedObject.FindProperty("_volume");
		_pitch = serializedObject.FindProperty("_pitch");
		_reverb = serializedObject.FindProperty("_reverb");
		_loop = serializedObject.FindProperty("loop");
		_clipRef = serializedObject.FindProperty("clip");
		_audioSourcePrefab = serializedObject.FindProperty("audioSourcePrefab");
		_source = null;

		_clip = (SoundClip_WRef)_clipRef.GetValue();
		_loaded = _clip.Load();
	}

	void OnDisable() {
		EditorApplication.update -= Update;

		if (_source != null) {
			GameObject.DestroyImmediate(_source.gameObject);
			_source = null;
		}
	}

	void Update() {
		if (_clip.inspector_Resource == null) {
			StopPlaying();
        } else if (_source == null) {
			EditorApplication.update -= Update;
			Repaint();
		}
	}

	void StopPlaying() {
		if (_source != null) {
			GameObject.DestroyImmediate(_source.gameObject);
			_source = null;
			EditorApplication.update -= Update;
			Repaint();
		}
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();

		EditorGUILayout.PropertyField(_playChance);

		_volume.vector2Value = GUILayoutHelpers.MinMaxSlider("Volume", _volume.vector2Value, 0, 1);
		_pitch.vector2Value = GUILayoutHelpers.MinMaxSlider("Pitch", _pitch.vector2Value, -3, 3);
		_reverb.vector2Value = GUILayoutHelpers.MinMaxSlider("Reverb", _reverb.vector2Value, 0, 1.1f);

		EditorGUILayout.PropertyField(_loop);
		EditorGUILayout.PropertyField(_clipRef);

		var newClip = _clip.Load();
		if (newClip != _loaded) {
			_loaded = newClip;
			StopPlaying();
		}
		
		var oldPrefab = _audioSourcePrefab.objectReferenceValue;
		EditorGUILayout.PropertyField(_audioSourcePrefab, true);
		if (!ReferenceEquals(oldPrefab, _audioSourcePrefab.objectReferenceValue)) {
			StopPlaying();
		}

		SoundCue self = (SoundCue)target;

		var wasEnabled = GUI.enabled;
		if (_source == null) {
			GUI.enabled = (_loaded != null) && (self.audioSourcePrefab != null);
			if (GUILayout.Button("Play")) {
				string unused;
				var clipToPlay = _loaded.RandomClip(Random.value, out unused);
				if (clipToPlay != null) {
					var go = (GameObject)GameObject.Instantiate(self.audioSourcePrefab.gameObject, Vector3.zero, Quaternion.identity);
					go.hideFlags = HideFlags.HideAndDontSave;

					_source = go.GetComponent<AudioSource>();
					go.AddComponent<AudioSourceGC>();

					_source.spatialBlend = 0f;
					_source.clip = clipToPlay;
					_source.volume = self.RandomVolume(Random.value);
					_source.pitch = self.RandomPitch(Random.value);
					_source.reverbZoneMix = self.RandomReverb(Random.value);
					_source.loop = _loop.boolValue;
					_source.Play();

					EditorApplication.update += Update;
				}
			}
		} else {
			GUI.enabled = true;
			if (GUILayout.Button("Stop")) {
				StopPlaying();
			}
		}
		GUI.enabled = wasEnabled;

		serializedObject.ApplyModifiedProperties();
	}
}
