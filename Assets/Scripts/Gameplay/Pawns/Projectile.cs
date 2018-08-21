﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {


	public class Projectile : Actor {

		#region State

		public WeaponData.ProjectileData data;
		public Team team;
		public Vector3 velocity;
		public Actor owner;
		public Pawn target;

		#endregion

		public override System.Type clientType => typeof(Projectile);
		public override System.Type serverType => typeof(Projectile);

		public Vector3 position { get { return go.transform.position; } set { go.transform.position = value; } }

		public void Spawn(WeaponData.ProjectileData data, Vector3 pos, Vector3 velocity, Pawn target, Actor instigator, Actor owner, Team team) {
			this.data = data;
			AttachExternalGameObject(GameObject.Instantiate(this.data.prefab.Load(), pos, Quaternion.identity, null));
			this.team = team;
			SetLifetime(this.data.lifetime);
			this.velocity = velocity;
			this.owner = owner;
			this.target = target;
		}

		public override void Tick() {
			base.Tick();

			if (pendingKill) {
				return;
			}

			if (!hasAuthority) {
				return;
			}

			float dt = world.deltaTime;
			Vector3 move;
			if (target != null && data.heatSeeking) {
				float speed = velocity.magnitude;
				velocity = (target.position - position).normalized * speed;
			}
			move = velocity * dt;
			RaycastHit hit;
			if (Physics.Raycast(position,move.normalized,out hit,move.magnitude)) {
				var target = hit.transform.FindServerActorUpwards() as Pawn;
				if (target != null) {
					if (target.team != team) {
						target.Hit(this, owner, true);
						Destroy();
						return;
					}
				}
				else {
					// hit the terrain
					Destroy();
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


