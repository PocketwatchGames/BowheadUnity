// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead {

	[RequireComponent(typeof(Camera))]
	[RequireComponent(typeof(GUILayer))]
	[RequireComponent(typeof(AudioListener))]
	public class SpectatorCamera : MonoBehaviour {

		new Camera camera;
		GUILayer guiLayer;
		AudioListener listener;

		void Awake() {
			camera = GetComponent<Camera>();
			guiLayer = GetComponent<GUILayer>();
			listener = GetComponent<AudioListener>();

			camera.enabled = false;
			guiLayer.enabled = false;
			listener.enabled = false;
		}

		public void Spectate() {
			camera.enabled = true;
			guiLayer.enabled = true;
			listener.enabled = true;
		}
	}
}
