// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {

	[RequireComponent(typeof(Collider))]
	public class WaterVolume : MonoBehaviour {

		List<Actors.DamageableActor> _touching = new List<Actors.DamageableActor>();

		void Awake() {
			gameObject.layer = Layers.Water;

			var collider = GetComponent<Collider>();
			collider.isTrigger = true;

		}

		void Reset() {
			gameObject.layer = Layers.Water;
		}

		void OnTriggerEnter(Collider collider) {
			//var layer = collider.gameObject.layer;
			//if (((layer < Layers.Team1) ||
			//	(layer > Layers.Team4Projectiles)) && (layer != Layers.NoSelfContactProjectiles)) {
			//	return;
			//}

			if (GameManager.instance.serverWorld != null) {
				var actor = collider.transform.FindServerActorUpwards() as Actors.DamageableActor;
				if ((actor != null) && !actor.pendingKill) {
					if (!_touching.Contains(actor)) {
						_touching.Add(actor);
						++actor.waterVolumeCount;
					}
				}
			}

			if (GameManager.instance.clientWorld != null) {
				var actor = collider.transform.FindClientActorUpwards() as Actors.DamageableActor;
				if ((actor != null) && !actor.pendingKill) {
					if (!_touching.Contains(actor)) {
						_touching.Add(actor);
						++actor.waterVolumeCount;
					}
				}
			}
		}

		void OnTriggerExit(Collider collider) {
			//var layer = collider.gameObject.layer;
			//if (((layer < Layers.Team1) ||
			//	(layer > Layers.Team4Projectiles)) && (layer != Layers.NoSelfContactProjectiles)) {
			//	return;
			//}

			if (GameManager.instance.serverWorld != null) {
				var actor = collider.transform.FindServerActorUpwards() as Actors.DamageableActor;
				if ((actor != null) && _touching.Remove(actor)) {
					if (!actor.pendingKill) {
						--actor.waterVolumeCount;
					}
				}
			}

			if (GameManager.instance.clientWorld != null) {
				var actor = collider.transform.FindClientActorUpwards() as Actors.DamageableActor;
				if ((actor != null) && _touching.Remove(actor)) {
					if (!actor.pendingKill) {
						--actor.waterVolumeCount;
					}
				}
			}
		}
	}
}
