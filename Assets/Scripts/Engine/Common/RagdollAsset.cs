// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public interface RagdollState {
	void Ragdoll(SkinnedMeshRenderer renderer, Animator animator, Vector3 velocity);
	void Wake();
	void Sleep();
	bool active { get; }
	bool sleeping { get; }
	Transform root { get; }
}

public interface RagdollController {
	double lastRagdollExplosionTime { get; set; }
	int numConcurrentRagdollExplosions { get; set; }
	bool ragdollExplosionRateLimited { get; set; }
	bool ragdollEnabled { get; }
	bool disposed { get; }
	bool FadeOutRagdoll(float delay, float ttl);
}

[Serializable]
public sealed class RagdollAsset_WRef : WeakAssetRef<RagdollAsset> { }

public class RagdollAsset : ScriptableObject {

	[SerializeField]
	List<Element> elements;

	class InternalRagdollState : RagdollState {
		List<Element> _elements;
		bool _ragdoll;
		bool _sleeping;

		public class Element {
			public Transform transform;
			public Collider collider;
			public Rigidbody rigidBody;
		}

		public InternalRagdollState(List<Element> elements) {
			_elements = elements;
			_sleeping = true;
		}

		public void Ragdoll(SkinnedMeshRenderer renderer, Animator animator, Vector3 velocity) {
			if (_ragdoll) {
				return;
			}

			_sleeping = false;
			_ragdoll = true;
			animator.enabled = false;
			renderer.enabled = false;

			for (int i = 0; i < _elements.Count; ++i) {
				var e = _elements[i];

				if (e.transform.name == "pelvis") {
					root = e.transform;
					renderer.rootBone = e.transform;
				}

				if (e.rigidBody != null) {
					e.rigidBody.isKinematic = false;

					if (velocity.sqrMagnitude > 0.01f) {
						e.rigidBody.AddForce(velocity*3, ForceMode.Impulse);
					}
				}

				if (e.collider != null) {
					e.collider.enabled = true;
				}
			}

			renderer.enabled = true;
		}

		public void Sleep() {
			_sleeping = true;
			for (int i = 0; i < _elements.Count; ++i) {
				var e = _elements[i];
				if (e.rigidBody != null) {
					e.rigidBody.isKinematic = true;
				}
			}
		}

		public void Wake() {
			_sleeping = false;
			for (int i = 0; i < _elements.Count; ++i) {
				var e = _elements[i];
				if (e.rigidBody != null) {
					e.rigidBody.isKinematic = false;
				}
			}
		}

		public bool sleeping {
			get {
				if (_sleeping) {
					return true;
				}

				for (int i = 0; i < _elements.Count; ++i) {
					var e = _elements[i];
					if ((e.rigidBody != null) && !e.rigidBody.IsSleeping()) {
						return false;
					}
				}

				return true;
			}
		}
		
		public bool active {
			get {
				return _ragdoll;
			}
		}

		public Transform root {
			get;
			private set;
		}
	}

#if UNITY_EDITOR
	public static RagdollAsset Create(Transform root, string path) {
		List<Element> elements = new List<Element>();

		var bodies = root.GetComponentsInChildren<Rigidbody>(true);
		foreach (var rb in bodies) {
			var collider = rb.GetComponent<Collider>();
			if (collider != null) {
				var joint = rb.GetComponent<CharacterJoint>();

				var elem = new Element();
				elem.path = Utils.GetChildPath(root, rb.transform);
				elem.collider = ColliderDescription.New(collider);
				elem.rigidBody = RigidbodyDescription.New(rb);
				if (joint != null) {
					elem.joint = CharacterJointDescription.New(joint, root);
				}

				elements.Add(elem);
			}
		}

		RagdollAsset asset = null;

		if (elements.Count > 0) {
			asset = Utils.CreateAsset<RagdollAsset>(path);
			asset.elements = elements;
		}

		return asset;
	}

	public void Rebuild(Transform root) {
		elements = new List<Element>();

		var bodies = root.GetComponentsInChildren<Rigidbody>(true);
		foreach (var rb in bodies) {
			var collider = rb.GetComponent<Collider>();
			if (collider != null) {
				var joint = rb.GetComponent<CharacterJoint>();

				var elem = new Element();
				elem.path = Utils.GetChildPath(root, rb.transform);
				elem.collider = ColliderDescription.New(collider);
				elem.rigidBody = RigidbodyDescription.New(rb);
				if (joint != null) {
					elem.joint = CharacterJointDescription.New(joint, root);
				}

				elements.Add(elem);
			}
		}

		EditorUtility.SetDirty(this);
	}
#endif

	public RagdollState AddRagdollComponents(Transform root, bool addActorRefs) {
		RagdollState state = null;

		if ((elements != null) && (elements.Count > 0)) {
			List<InternalRagdollState.Element> ragdollBones = new List<InternalRagdollState.Element>();

			// first pass does colliders/rigid bodies because joints need to reference RBs
			for (int i = 0; i < elements.Count; ++i) {
				var e = elements[i];
				var t = root.Find(e.path);
				if (t != null) {
					var collider = e.collider.AddCollider(t.gameObject);
					
					var rb = e.rigidBody.AddRigidBody(t.gameObject);
					rb.isKinematic = true;
					rb.useGravity = true;
					rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

					InternalRagdollState.Element bone = new InternalRagdollState.Element();
					bone.collider = collider;
					bone.rigidBody = rb;
					bone.transform = t;
					ragdollBones.Add(bone);

					if (addActorRefs) {
						var r = t.gameObject.AddComponent<ActorReference>();
						r.FindUpwards();
					}
				}
			}

			for (int i = 0; i < elements.Count; ++i) {
				var e = elements[i];
				var t = root.Find(e.path);
				if (t != null) {
					e.joint.AddCharacterJoint(t.gameObject, root);
				}
			}

			if (ragdollBones.Count > 0) {
				state = new InternalRagdollState(ragdollBones);
			}
		}

		return state;
	}

	[Serializable]
	struct Element {
		public string path;
		public ColliderDescription collider;
		public RigidbodyDescription rigidBody;
		public CharacterJointDescription joint;
	}
	
	enum ColliderType {
		None,
		Box,
		Cylinder,
		Capsule,
		Sphere
	}

	enum CapsuleDirection {
		X,
		Y,
		Z
	}

	[Serializable]
	struct ColliderDescription {
		public ColliderType colliderType;
		public CapsuleDirection capsuleDirection;
		public bool isTrigger;
		public Vector3 center;
		public Vector3 size;

		public Collider AddCollider(GameObject go) {
			switch (colliderType) {
				case ColliderType.Box: {
					var box = go.AddComponent<BoxCollider>();
					box.isTrigger = isTrigger;
					box.center = center;
					box.size = size;
					return box;
				}
				case ColliderType.Sphere: {
					var sphere = go.AddComponent<SphereCollider>();
					sphere.isTrigger = isTrigger;
					sphere.center = center;
					sphere.radius = size.x;
					return sphere;
				}
				case ColliderType.Capsule: {
					var capsule = go.AddComponent<CapsuleCollider>();
					capsule.isTrigger = isTrigger;
					capsule.center = center;
					capsule.radius = size.x;
					capsule.height = size.y;
					capsule.direction = (int)capsuleDirection;
					return capsule;
				}
			}

			return null;
		}

		public static ColliderDescription New(Collider c) {
			if (c != null) {
				if (c is BoxCollider) {
					return New((BoxCollider)c);
				} else if (c is SphereCollider) {
					return New((SphereCollider)c);
				} else {
					return New((CapsuleCollider)c);
				}
			}

			return new ColliderDescription();
		}

		public static ColliderDescription New(BoxCollider box) {
			var d = new ColliderDescription();
			d.colliderType = ColliderType.Box;
			d.isTrigger = box.isTrigger;
			d.center = box.center;
			d.size = box.size;
			return d;
		}

		public static ColliderDescription New(SphereCollider sphere) {
			var d = new ColliderDescription();
			d.colliderType = ColliderType.Sphere;
			d.isTrigger = sphere.isTrigger;
			d.center = sphere.center;
			d.size.x = sphere.radius;
			return d;
		}

		public static ColliderDescription New(CapsuleCollider capsule) {
			var d = new ColliderDescription();
			d.colliderType = ColliderType.Capsule;
			d.capsuleDirection = (CapsuleDirection)capsule.direction;
			d.isTrigger = capsule.isTrigger;
			d.center = capsule.center;
			d.size.x = capsule.radius;
			d.size.y = capsule.height;
			return d;
		}
	}

	[Serializable]
	struct RigidbodyDescription {
		public bool exists;
		public float mass;
		public Vector3 center;
		public float drag;
		public float angularDrag;
		RigidbodyInterpolation interpolation;
		CollisionDetectionMode collisionDetection;
		RigidbodyConstraints constraints;

		public Rigidbody AddRigidBody(GameObject go) {
			Rigidbody body = null;
			if (exists) {
				body = go.AddComponent<Rigidbody>();
				body.mass = mass;
				body.centerOfMass = center;
				body.drag = drag;
				body.angularDrag = angularDrag;
				body.interpolation = interpolation;
				body.collisionDetectionMode = collisionDetection;
				body.constraints = constraints;
			}
			return body;
		}

		public static RigidbodyDescription New(Rigidbody body) {
			var d = new RigidbodyDescription();
			d.exists = true;
			d.mass = body.mass;
			d.center = body.centerOfMass;
			d.drag = body.drag;
			d.angularDrag = body.angularDrag;
			d.interpolation = body.interpolation;
			d.collisionDetection = body.collisionDetectionMode;
			d.constraints = body.constraints;
			return d;
		}
	}

	[Serializable]
	struct SerializableSoftJointLimitSpring {
		public float damper;
		public float spring;

		public static implicit operator SoftJointLimitSpring(SerializableSoftJointLimitSpring spring) {
			var z = new SoftJointLimitSpring();
			z.damper = spring.damper;
			z.spring = spring.spring;
			return z;
		}

		public static implicit operator SerializableSoftJointLimitSpring(SoftJointLimitSpring spring) {
			var z = new SerializableSoftJointLimitSpring();
			z.damper = spring.damper;
			z.spring = spring.spring;
			return z;
		}
	}

	[Serializable]
	struct SerializableSoftJointLimit {
		public float bounciness;
		public float contactDistance;
		public float limit;

		public static implicit operator SoftJointLimit(SerializableSoftJointLimit limit) {
			var z = new SoftJointLimit();
			z.bounciness = limit.bounciness;
			z.contactDistance = limit.contactDistance;
			z.limit = limit.limit;
			return z;
		}

		public static implicit operator SerializableSoftJointLimit(SoftJointLimit limit) {
			var z = new SerializableSoftJointLimit();
			z.bounciness = limit.bounciness;
			z.contactDistance = limit.contactDistance;
			z.limit = limit.limit;
			return z;
		}
	}

	[Serializable]
	struct CharacterJointDescription {
		public bool exists;
		public string connectedBody;
		public Vector3 anchor;
		public Vector3 axis;
		public Vector3 swingAxis;
		public Vector3 connectedAnchor;
		public bool autoConfigureConnected;
		public SerializableSoftJointLimitSpring twistLimitSpring;
		public SerializableSoftJointLimit lowTwistLimit;
		public SerializableSoftJointLimit highTwistLimit;
		public SerializableSoftJointLimitSpring swingLimitSpring;
		public SerializableSoftJointLimit swing1Limit;
		public SerializableSoftJointLimit swing2Limit;
		public bool enableProjection;
		public float projectionDistance;
		public float projectionAngle;
		public float breakForce;
		public float breakTorque;
		public bool enableCollision;
		public bool enablePreprocessing;

		public CharacterJoint AddCharacterJoint(GameObject go, Transform root) {
			CharacterJoint joint = null;
			if (exists) {
				if (!string.IsNullOrEmpty(connectedBody)) {
					var t = root.Find(connectedBody);
					if (t != null) {
						joint = go.AddComponent<CharacterJoint>();
						joint.connectedBody = t.GetComponent<Rigidbody>();
						Assert.IsNotNull(joint.connectedBody);
					}
				} else {
					joint = go.AddComponent<CharacterJoint>();
					joint.connectedBody = null;
				}
				joint.anchor = anchor;
				joint.axis = axis;
				joint.autoConfigureConnectedAnchor = autoConfigureConnected;
				joint.connectedAnchor = connectedAnchor;
				joint.swingAxis = swingAxis;
				joint.twistLimitSpring = twistLimitSpring;
				joint.lowTwistLimit = lowTwistLimit;
				joint.highTwistLimit = highTwistLimit;
				joint.swingLimitSpring = swingLimitSpring;
				joint.swing1Limit = swing1Limit;
				joint.swing2Limit = swing2Limit;
				joint.enableProjection = enableProjection;
				joint.projectionDistance = projectionDistance;
				joint.projectionAngle = projectionAngle;
				joint.breakForce = breakForce;
				joint.breakTorque = breakTorque;
				joint.enableCollision = enableCollision;
				joint.enablePreprocessing = enablePreprocessing;
			}
			return joint;
		}

		public static CharacterJointDescription New(CharacterJoint joint, Transform root) {
			var d = new CharacterJointDescription();
			d.exists = true;
			d.connectedBody = (joint.connectedBody != null) ? root.GetChildPath(joint.connectedBody.transform) : "";
			d.anchor = joint.anchor;
			d.axis = joint.axis;
			d.autoConfigureConnected = joint.autoConfigureConnectedAnchor;
			d.connectedAnchor = joint.connectedAnchor;
			d.swingAxis = joint.swingAxis;
			d.twistLimitSpring = joint.twistLimitSpring;
			d.lowTwistLimit = joint.lowTwistLimit;
			d.highTwistLimit = joint.highTwistLimit;
			d.swingLimitSpring = joint.swingLimitSpring;
			d.swing1Limit = joint.swing1Limit;
			d.swing2Limit = joint.swing2Limit;
			d.enableProjection = joint.enableProjection;
			d.projectionDistance = joint.projectionDistance;
			d.projectionAngle = joint.projectionAngle;
			d.breakForce = joint.breakForce;
			d.breakTorque = joint.breakTorque;
			d.enableCollision = joint.enableCollision;
			d.enablePreprocessing = joint.enablePreprocessing;
			return d;
        }
	}
}
