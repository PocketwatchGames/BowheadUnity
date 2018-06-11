// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections;

namespace Bowhead {
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	public class WaterSplashes : ClientMonoBehaviour {
		[Serializable]
		public struct Interaction {
			public GameObject_WRef prefab;
			public bool orientToVelocity;

			public void Precache() {
				WeakAssetRef.Precache(prefab);
			}
		}

		public Interaction enterWaterEffect;
		public Interaction exitWaterEffect;
		public Interaction movingInWaterEffect;
		public float movingInWaterThreshold;
		public Interaction idleInWaterEffect;
		public Vector3 waterEffectOffset;

		int _waterCount;
		Quaternion _velocityOrient;
		Vector3 _lastPos;
		GameObject _movingInstance;
		GameObject _idleInstance;
		Effects _movingEffects;
		Effects _idleEffects;

		bool _isMoving;
		bool _isSubmerged;
		float _moveRateSq;
		CapsuleCollider _capsule;
		BoxCollider _box;

		struct Effects {

			public Effects(GameObject go) {
				_particles = go.GetComponentsInAllChildren<ParticleSystem>();
				if (_particles.Length < 1) {
					_particles = null;
				}

				_sounds = go.GetComponentsInAllChildren<SoundEntity>();
				if (_sounds.Length < 1) {
					_sounds = null;
				}
			}

			public void Play() {
				if (_particles != null) {
					for (int i = 0; i < _particles.Length; ++i) {
						var p = _particles[i];
						p.Play();
					}
				}
				if (_sounds != null) {
					for (int i = 0; i < _sounds.Length; ++i) {
						var s = _sounds[i];
						s.PlayIfNotPlaying();
					}
				}
			}

			public void Stop() {
				if (_particles != null) {
					for (int i = 0; i < _particles.Length; ++i) {
						var p = _particles[i];
						p.Stop();
					}
				}
				if (_sounds != null) {
					for (int i = 0; i < _sounds.Length; ++i) {
						var s = _sounds[i];
						s.Stop();
					}
				}
			}

			ParticleSystem[] _particles;
			SoundEntity[] _sounds;
		}

		protected override void OnStart() {
			base.OnStart();

			_lastPos = transform.position;
			_moveRateSq = movingInWaterThreshold*movingInWaterThreshold;

			if (movingInWaterEffect.prefab != null) {
				var prefab = movingInWaterEffect.prefab.Load();
				if (prefab != null) {
					_movingInstance = Instantiate(prefab);
					_movingInstance.SetActive(false);
					_movingInstance.transform.parent = gameObject.transform;
					_movingEffects = new Effects(_movingInstance);
				}
			}

			if (idleInWaterEffect.prefab != null) {
				var prefab = idleInWaterEffect.prefab.Load();
				if (prefab != null) {
					_idleInstance = Instantiate(prefab);
					_idleInstance.SetActive(false);
					_idleInstance.transform.parent = gameObject.transform;
					_idleEffects = new Effects(_idleInstance);
				}
			}

			_capsule = GetComponent<CapsuleCollider>();
			_box = GetComponent<BoxCollider>();
		}

		protected virtual void Update() {
			if (GameManager.instance.fixedUpdateDidRun) {
				var d = transform.position - _lastPos;
				_lastPos = transform.position;
				if (_waterCount > 0) {
					d.y = 0f;

					if (d.sqrMagnitude > 0.01f) {
						_velocityOrient = Quaternion.LookRotation(d);
					} else {
						_velocityOrient = transform.rotation;
					}

					Update(d.sqrMagnitude >= _moveRateSq, _isMoving);
				}
			}
		}

		void OnTriggerEnter(Collider collider) {
			if (collider.gameObject.layer == Layers.Water) {
				++_waterCount;
				if (_waterCount == 1) {
					Enter();
				}
			}
		}

		void OnTriggerExit(Collider collider) {
			if (collider.gameObject.layer == Layers.Water) {
				--_waterCount;
				if (_waterCount == 0) {
					Exit();
				}
			}
		}

		void Update(bool isMoving, bool wasMoving) {
			if ((_movingInstance != null) || (_idleInstance != null)) {
				var waterContact = GetWaterContactPoint();
				var wasSubmerged = _isSubmerged;
				_isSubmerged = IsSubmerged(waterContact);

				_isMoving = isMoving;
				if ((wasMoving != _isMoving) || (wasSubmerged != _isSubmerged)) {
					if (_movingInstance != null) {
						if (_isMoving && !_isSubmerged) {
							_movingEffects.Play();
						} else {
							_movingEffects.Stop();
						}
					}
					if (_idleInstance != null) {
						if (!_isMoving && !_isSubmerged) {
							_idleEffects.Play();
						} else {
							_idleEffects.Stop();
						}
					}
				}
								
				if (_movingInstance != null) {
					_movingInstance.transform.position = waterContact;
					if (movingInWaterEffect.orientToVelocity) {
						_movingInstance.transform.rotation = _velocityOrient;
					}
				}
				if (_idleInstance != null) {
					_idleInstance.transform.position = waterContact;
					if (idleInWaterEffect.orientToVelocity) {
						_idleInstance.transform.rotation = _velocityOrient;
					}
				}
			}
		}

		void Enter() {
			if (enterWaterEffect.prefab != null) {
				var prefab = enterWaterEffect.prefab.Load();
				if (prefab != null) {
					Instantiate(prefab, GetWaterContactPoint(), enterWaterEffect.orientToVelocity ? _velocityOrient : Quaternion.identity);
				}
			}

			if (_movingInstance != null) {
				_movingInstance.SetActive(true);
			}
			if (_idleInstance != null) {
				_movingInstance.SetActive(true);
			}

			Update(true, false);
		}

		void Exit() {
			if (exitWaterEffect.prefab != null) {
				var prefab = exitWaterEffect.prefab.Load();
				if (prefab != null) {
					Instantiate(prefab, GetWaterContactPoint(), exitWaterEffect.orientToVelocity ? _velocityOrient : Quaternion.identity);
				}
			}

			if (_movingInstance != null) {
				_movingInstance.SetActive(false);
			}
			if (_idleInstance != null) {
				_movingInstance.SetActive(true);
			}
		}

		Vector3 GetWaterContactPoint() {
			RaycastHit hitInfo;
			if (Physics.Raycast(new Ray(transform.position + new Vector3(0, 256, 0f), Vector3.down), out hitInfo, Mathf.Infinity, Layers.WaterMask, QueryTriggerInteraction.Collide)) {
				return hitInfo.point + waterEffectOffset;
			}
			return transform.position + waterEffectOffset;
		}

		bool IsSubmerged(Vector3 contactPoint) {
			if (_capsule != null) {
				var c = _capsule.transform.position + _capsule.center;
				var t = c + new Vector3(0, _capsule.height/2f, 0);
				return contactPoint.y >= t.y;
			} else if (_box != null) {
				var c = _box.transform.position + _box.center;
				var t = c + new Vector3(0, _box.size.y/2f, 0);
				return contactPoint.y >= t.y;
			}
			return false;
		}

		public bool inWater {
			get {
				return _waterCount > 0;
			}
		}
	}
}