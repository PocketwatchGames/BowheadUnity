// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	[RequireComponent(typeof(Rigidbody))]
	public class BreakIntoPieces : MonoBehaviour {
		[SerializeField]
		SoundCue _sound;
		[SerializeField]
		Debris[] _debris;

		Rigidbody _rb;

		void Awake() {
			_rb = GetComponent<Rigidbody>();
		}
		
		public void Break(float ttl) {
			GameManager.instance.Play(transform.position, _sound);

			if (_debris != null) {
				var velocity = _rb.velocity;

				for (int i = 0; i < _debris.Length; ++i) {
					var db = _debris[i];
					if (db != null) {
						db.Throw(ttl, velocity);
					}
				}
			}

			Utils.DestroyComponent(this);
		}
	}
}