// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead {
	public class Debris : MonoBehaviour {
		Rigidbody _rb;
		float _ttl;
		float _sink;

		void Awake() {
			_rb = GetComponent<Rigidbody>();
		}

		void Update() {
			var clWorld = GameManager.instance.clientWorld;
			if (clWorld != null) {
				var dt = clWorld.deltaTime;

				if (_ttl > 0) {
					_ttl -= dt;
					if ((_rb != null) && (_ttl <= 0)) {
						_rb.isKinematic = true;
						_rb.detectCollisions = false;
					}
				} else if (_sink > 0) {
					if (_rb != null) {
						_rb.transform.position -= Vector3.up/**Actors.Unit.RAGDOLL_FADEOUT_MOVE_SPEED*/*dt;
					}
					_sink -= dt;
					if (_sink <= 0) {
						Utils.DestroyGameObject(gameObject);
					}
				}
			}
		}

		public void Throw(float ttl, Vector3 velocity) {
			_ttl = ttl;
			_sink = GameManager.instance.ragdollFadeTime;

			transform.SetParent(null, true);

			if (_rb != null) {
				_rb.isKinematic = false;
				_rb.detectCollisions = true;
				_rb.velocity = velocity * 0.5f;
			}
		}
	}
}