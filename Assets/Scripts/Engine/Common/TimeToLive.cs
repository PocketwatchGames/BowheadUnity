// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

public class TimeToLive : MonoBehaviour {

	public float timeToLive;
	
	void Update () {
		timeToLive -= Time.deltaTime;
		if (timeToLive <= 0f) {
			Utils.DestroyGameObject(gameObject);
		}
	}
}
