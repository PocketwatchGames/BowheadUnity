// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSystemGC : MonoBehaviour {

	ParticleSystem _ps;

	void Start () {
		_ps = GetComponent<ParticleSystem>();
	}
	
	void Update () {
		if (_ps != null) {
			if (!_ps.isPlaying) {
				Utils.DestroyGameObject(gameObject);
			}
		}
	}
}
