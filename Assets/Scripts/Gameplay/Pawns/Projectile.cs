﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {


	public class Projectile : Interactable<Projectile, ProjectileData> {

		#region State

		public Team team;
		public Vector3 velocity;

		#endregion

		public override System.Type clientType => typeof(Projectile);
		public override System.Type serverType => typeof(Projectile);

		public Vector3 position { get { return go.transform.position; } set { go.transform.position = value; } }

		public void Spawn(EntityData data, Vector3 pos, Vector3 velocity, Actor instigator, Actor owner, Team team) {
			base.ConstructEntity(data);
			AttachExternalGameObject(GameObject.Instantiate(this.data.prefab.Load(), pos, Quaternion.identity, null));
			this.team = team;
			SetLifetime(this.data.lifetime);
			this.velocity = velocity;
		}

		public override void Tick() {
			base.Tick();

			if (!hasAuthority) {
				return;
			}

			float dt = world.deltaTime;
			var move = velocity * dt;
			RaycastHit hit;
			if (Physics.Raycast(position,move.normalized,out hit,move.magnitude)) {
				var target = (Pawn)hit.transform.FindServerActorUpwards();
				if (target != null) {
					if (target.team != team) {
						target.damage(data.damage);
						GameObject.Destroy(go);
						return;
					}
				}
				else {
					// hit the terrain
					GameObject.Destroy(go);
					return;
				}
			}

			SetPosition(position + move);
		}

		public virtual void SetPosition(Vector3 p, float interpolateTime = 0) {
			position = p;
		}

	}
}


