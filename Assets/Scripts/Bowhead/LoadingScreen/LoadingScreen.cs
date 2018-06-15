// Copyright (c) 2018 Pocketwatch Games, LLC

using UnityEngine;

namespace Bowhead {
	public class LoadingScreen : MonoBehaviour {
		void Start() {
			GameManager.instance.StartAsyncTravel();
		}
	}
}