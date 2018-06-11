// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class SoundCue : VersionedObject {
	const int VERSION = 2;

	[SerializeField]
	[Range(0, 100)]
	float _playChance;
	[SerializeField]
	Vector2 _volume;
	[SerializeField]
	Vector2 _pitch;
	[SerializeField]
	Vector2 _reverb;
	public bool loop;
	[SerializeField]
	SoundClip_WRef clip;
	public AudioSource audioSourcePrefab;

	string _lastKey;

	public void Precache() {
		clip.Load();	
	}

	public AudioClip RandomClip(float random, float random2) {
		if ((_playChance > 0f) && ((random*100) <= _playChance)) {
			var x = clip.Load();
			if (x != null) {
				return x.RandomClip(random2, out _lastKey);
			}
		}
		_lastKey = null;
		return null;
	}

	public float RandomVolume(float random) {
		return Mathf.Lerp(_volume.x, _volume.y, random);
	}

	public float RandomPitch(float random) {
		return Mathf.Lerp(_pitch.x, _pitch.y, random);
	}

	public float RandomReverb(float random) {
		return Mathf.Lerp(_reverb.x, _reverb.y, random);
	}

	public static void Precache(SoundCue cue) {
		if (cue != null) {
			cue.Precache();
		}
	}

	public string lastClipKey {
		get {
			return _lastKey;
		}
	}

#if UNITY_EDITOR

	protected override void InitVersion() {
		base.InitVersion();

		if (version < 1) {
			_playChance = 100;
		}
		if (version < 2) {
			_volume = Vector2.one;
			_pitch = Vector2.one;
			_reverb = Vector2.one;
		}

		version = VERSION;
	}

	[MenuItem("Assets/Create/Engine/Sound Cue")]
	static void CreateAsset() {
		Utils.CreateAsset<SoundCue>();
	}
#endif
}