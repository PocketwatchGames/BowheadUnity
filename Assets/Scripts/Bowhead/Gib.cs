// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead {

	[Serializable]
	public struct GibBloodSpray {
		public Transform attachPoint;
		public ParticleSystem bloodSprayPrefab;
	}

	[Serializable]
	public struct GibBloodSpray_WRef {
		public string attachPoint;
		public ParticleSystem_WRef bloodSprayPrefab;

		public void Precache() {

		}

		public static void Precache(IList<GibBloodSpray_WRef> blood) {
			if (blood != null) {
				for (int i = 0; i < blood.Count; ++i) {
					blood[i].Precache();
				}
			}
		}
	}

	[Serializable]
	public sealed class Gib_WRef : WeakAssetRef<Gib> { }

	[Serializable]
	public struct GibSpawn {
		public GameObject prefab;
		public Vector3 offset;
	}

	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	public class Gib : WaterSplashes, RagdollController {

		public BloodSplatter blood;
		public float contactSplatterRate;
		public float movingSplatterRate;
		public GibBloodSpray[] bloodSpray;
		public float bloodSprayRate;
		public float minBloodForce;
		public float health;
		[Range(0, 100)]
		public float contactBloodSprayChance;
		[Range(0, 100)]
		public float explosionBloodSprayChance;
		public PhysicalMaterialClass materialClass;
		public SoundCue contactSound;
		public SoundCue spawnSound;
		public List<GibSpawn> spawnOnDeath;
		public GameObject root;

		public bool destroyIfOutOfWorld {
			get;
			set;
		}

		Rigidbody _rb;
		float _nextBleedTime;
		float _nextContactTime;
		float _nextMovingTime;
		bool _onGround;
		float _ragdollFadeDelay;
		float _ragdollFadeTime;
		float _ragdollCheckSleep;
		bool _ragdollFadeOut;
		RefCountedUObj<Material> _m;

		void Awake() {
			_rb = GetComponent<Rigidbody>();
			destroyIfOutOfWorld = true;
        }

		public void Spawn() {
			//gameObject.layer = Layers.Gibs;

			Bleed(100f);

			if (spawnSound != null) {
				GameManager.instance.Play(transform.position, spawnSound);
			}
		}

		protected override void Update() {
			base.Update();

			if (destroyIfOutOfWorld && (_rb.transform.position.y < -50)) {
				Utils.DestroyGameObject(root ?? gameObject);
				return;
			}

			var dt = Time.deltaTime;

			_nextBleedTime -= dt;
			if (_nextBleedTime < 0f) {
				_nextBleedTime = 0f;
			}

			_nextContactTime -= dt;
			if (_nextContactTime < 0f) {
				_nextContactTime = 0f;
			}

			_nextMovingTime -= dt;
			if (_nextMovingTime < 0f) {
				_nextMovingTime = 0f;
			}
						
			if (!_rb.IsSleeping() && _onGround && (_nextMovingTime <= 0f) && (movingSplatterRate > 0f)) {
				_nextMovingTime = movingSplatterRate;
				Splatter();
			}

			if (_ragdollFadeOut) {
				if (_ragdollCheckSleep > 0f) {
					_ragdollCheckSleep -= dt;
					if (_ragdollCheckSleep <= 0f) {
						_rb.isKinematic = true;
					}
				} else {
					if (_ragdollFadeDelay > 0f) {
						_ragdollFadeDelay -= dt;
						if (_ragdollFadeDelay <= 0f) {
							_rb.isKinematic = true;
						}
					} else {
						if (_ragdollFadeTime > 0f) {
							_ragdollFadeTime -= dt;
							transform.position = transform.position - Vector3.up/**Actors.Unit.RAGDOLL_FADEOUT_MOVE_SPEED*/*dt;
						} else {
							Utils.DestroyGameObject(root ?? gameObject);
						}
					}
				}
			}

			_onGround = false;
		}

		void Bleed(float chance) {
			if ((bloodSprayRate > 0f) && (_nextBleedTime <= 0f) && (chance > 0f)) {
				if ((chance >= 100f) || ((GameManager.instance.randomNumber*100) <= chance)) {
					_nextBleedTime = bloodSprayRate;

					if (bloodSpray != null) {
						for (int i = 0; i < bloodSpray.Length; ++i) {
							var blood = bloodSpray[i];
							if ((blood.attachPoint != null) && (blood.bloodSprayPrefab != null)) {
								var prefab = GameObject.Instantiate(blood.bloodSprayPrefab.gameObject);
								if (prefab != null) {
									prefab.transform.parent = blood.attachPoint.transform;
									prefab.transform.localPosition = Vector3.zero;
									prefab.transform.localRotation = Quaternion.identity;
								}
							}
						}
					}
				}
			}
		}

		void Splatter() {
			if (GameManager.instance.clientWorld != null) {
				//GameManager.instance.clientWorld.RenderBloodSplats(transform.position, blood.radius, blood.size, blood.count);
			}
		}

		void OnCollisionEnter(Collision collision) {
			if (GameManager.instance.clientWorld == null) {
				return;
			}

			if ((contactSplatterRate > 0f) && (_nextContactTime <= 0f)) {
				_nextContactTime = contactSplatterRate;
				Splatter();
			}
			Bleed(contactBloodSprayChance);

			if (contactSound != null) {
				GameManager.instance.Play(collision.contacts[0].point, contactSound);
			}

			if (materialClass != null) {
				var q = collision.gameObject.GetComponent<PhysicalMaterialQuery>();
				if (q != null) {
					var m = q.GetMaterialAtPoint(transform.position);
					if (m != null) {
						GameManager.instance.clientWorld.SpawnContactFx(materialClass, m, collision.contacts[0].point, collision.contacts[0].normal);
					}
				}
			}
		}

		void OnCollisionStay(Collision collision) {
			if (GameManager.instance.clientWorld == null) {
				return;
			}

			var isGround = (collision.gameObject.layer == Layers.Terrain);// || (collision.gameObject.layer == Layers.Block);
			_onGround = _onGround || isGround;
		}

		public void AddExplosionForce(float force, Vector3 pos, float ejection) {
			InternalAddExplosionForce(force, pos, ejection);
			if ((health > 0) && (spawnOnDeath.Count > 0)) {
				health -= force;

				if (health <= 0f) {
					for (int i = 0; i < spawnOnDeath.Count; ++i) {
						var spawn = spawnOnDeath[i];
						if (spawn.prefab != null) {
							var go = (GameObject)GameObject.Instantiate(spawn.prefab, transform.position + spawn.offset, transform.rotation);

							var rb = go.GetComponent<Rigidbody>();
							if (rb != null) {
								rb.AddExplosionForce(force, pos, 0, ejection, ForceMode.Force);
							}

							var gib = go.GetComponent<Gib>();
							if (gib != null) {
								gib.Spawn();
							}
						}
					}

					Utils.DestroyGameObject(gameObject);
				}
			}
		}

		public void InternalAddExplosionForce(float force, Vector3 pos, float ejection) {
			_rb.AddExplosionForce(force, pos, 0, ejection, ForceMode.Force);
			if (force >= minBloodForce) {
				Bleed(explosionBloodSprayChance);
			}
		}

		public bool FadeOutRagdoll(float delay, float ttl) {

			if (_ragdollFadeOut) {
				return false;
			}

			if (_rb == null) {
				if (gameObject != null) {
					Utils.DestroyGameObject(gameObject);
				}
				return false;
			}

			if (delay <= 0f) {
				if (ttl <= 0f) {
					Utils.DestroyGameObject(gameObject);
					return true;
				}
			}

			_ragdollFadeDelay = delay;
			_ragdollFadeTime = ttl;
			_ragdollFadeOut = true;

			if (_rb.IsSleeping()) {
				_rb.isKinematic = true;
				_ragdollCheckSleep = 0f;
			} else {
				_ragdollCheckSleep = 1f;// Actors.Unit.RAGDOLL_CHECK_SLEEP_TIME;
			}

			return true;
		}

		public void SetMaterial(Material original, RefCountedUObj<Material> mat) {
			_m = mat;
			_m.AddRef();

			var rs = gameObject.GetComponentsInAllChildren<Renderer>();
			
			for (int i = 0; i < rs.Length; ++i) {
				var r = rs[i];
				if (r.sharedMaterial == original) {
					r.material = mat.obj;
				}
			}
		}

		protected override void OnDestroy() {
			base.OnDestroy();

			if (_m != null) {
				_m.Dispose();
			}
		}

		public double lastRagdollExplosionTime { get; set; }
		public int numConcurrentRagdollExplosions { get; set; }
		public bool ragdollExplosionRateLimited { get; set; }

		public bool ragdollEnabled {
			get {
				return !_ragdollFadeOut;
			}
		}

		public bool disposed {
			get {
				return this == null;
			}
		}
	}
}