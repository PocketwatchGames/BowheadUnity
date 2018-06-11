// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class AudioSourceFadeOutAndStop : MonoBehaviour {
	float _dt;
	float _volume;
	AudioSource _source;

	void Awake() {
		_source = GetComponent<AudioSource>();
		_volume = _source.volume;
	}

	void Update() {
		if (_source.isPlaying) {
			if (fadeTime > 0f) {
				_dt += Time.deltaTime;
				var t = 1f - Mathf.Clamp01(_dt / fadeTime);
				if (t > 0f) {
					_source.volume = _volume * t;
				} else {
					_source.Stop();
					Destroy(this);
				}
			}
		} else {
			Destroy(this);
		}
	}

	public float fadeTime {
		get;
		set;
	}
}
