// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Reflection;

namespace Bowhead {

	public class CameraPreRenderCallback : MonoBehaviour {
		public delegate void Callback();

		List<Callback> _callbacks = new List<Callback>();

		void Awake() {
			SceneManager.sceneLoaded += LevelWasLoaded;
		}

		public void AddCallback(Callback cb) {
			_callbacks.Add(cb);
		}

		public void RemoveCallback(Callback cb) {
			_callbacks.Remove(cb);
		}

		void OnPreRender() {
			for (int i = 0; i < _callbacks.Count; ++i) {
				_callbacks[i]();
			}
		}

		void OnDestroy() {
			SceneManager.sceneLoaded -= LevelWasLoaded;
		}

		void LevelWasLoaded(Scene scene, LoadSceneMode mode) {
			if (mode == LoadSceneMode.Single) {
				_callbacks.Clear();
			}
		}
	}
}