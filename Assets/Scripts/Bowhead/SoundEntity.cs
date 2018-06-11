// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public class SoundEntity : MonoBehaviour {

		public SoundCue soundCue;
		public bool playOnAwake;
		public bool positional;

		AudioSource _source;

		public void OnEnable() {
			if (playOnAwake) {
				PlayIfNotPlaying();
			}
		}

		public void OnDisable() {
			if (!positional) {
				Stop();
			}
		}

		public void Play() {
			if (soundCue != null) {
				if (GameManager.instance.inMainMenu || ((GameManager.instance.clientWorld != null) && !GameManager.instance.clientWorld.isTraveling)) {
					var src = positional ? GameManager.instance.Play(transform.position, soundCue) : GameManager.instance.Play(gameObject, soundCue);
					if (src != null) {
						_source = src;
					}
				}
			}
		}

		public void PlayIfNotPlaying() {
			if (_source == null) {
				Play();
			}
		}

		public void Stop() {
			if (_source != null) {
				Utils.DestroyGameObject(_source.gameObject);
				_source = null;
			}
		}

#if UNITY_EDITOR
			void Reset() {
			playOnAwake = true;
		}
#endif

	}
}