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

	public static class SoundUtils {
		public static void PrecacheSounds(this GameObject go) {
			var sounds = go.GetComponentsInAllChildren<SoundEntity>();
			for (int i = 0; i < sounds.Length; ++i) {
				SoundCue.Precache(sounds[i].soundCue);
			}
		}
		public static void PrecacheWithSounds<T>(WeakAssetRef<T> asset) where T : Component {
			var t = asset.Load();
			if (t != null) {
				t.gameObject.PrecacheSounds();
			}
		}

		public static void PrecacheWithSounds<T>(this WeakAssetRef<T> asset, System.Action<T> f) where T : Component {
			var t = asset.Load();
			if (t != null) {
				t.gameObject.PrecacheSounds();
				f(t);
			}
		}

		public static void PrecacheWithSounds(GameObject_WRef asset) {
			var go = asset.Load();
			if (go != null) {
				go.PrecacheSounds();
			}
		}

		public static void PrecacheWithSounds(GameObject_WRef asset, System.Action<GameObject> f) {
			var go = asset.Load();
			if (go != null) {
				go.PrecacheSounds();
				f(go);
			}
		}
	}
}