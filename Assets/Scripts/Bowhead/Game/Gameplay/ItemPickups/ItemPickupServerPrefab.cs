// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {

	[RequireComponent(typeof(ActorReference))]
	[RequireComponent(typeof(SphereCollider))]
	[RequireComponent(typeof(Rigidbody))]
	public class ItemPickupServerPrefab : MonoBehaviour {
#if UNITY_EDITOR
		void Reset() {
			gameObject.layer = Layers.Trigger;
			GetComponent<Collider>().isTrigger = true;
			GetComponent<Collider>().enabled = false;
		}
#endif
	}
}