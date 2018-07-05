using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {


	public class Projectile : Interactable<Projectile, ProjectileData> {

		#region State

		public Team team;
		public float lifetime;
		public Vector3 velocity;

		#endregion

		public override System.Type clientType => typeof(Projectile);
		public override System.Type serverType => typeof(Projectile);

		public Vector3 position { get { return go.transform.position; } set { go.transform.position = value; } }

		// This field is a HACK and is null on clients
		public Server.BowheadGame gameMode {
			get;
			private set;
		}

		public override void Spawn(EntityData data, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(data, pos, instigator, owner, team);
			this.data = (ProjectileData)data;
			gameMode = (Server.BowheadGame)((Server.ServerWorld)world).gameMode;
			AttachExternalGameObject(GameObject.Instantiate(this.data.prefab.Load(), pos, Quaternion.identity, null));
			this.team = team;

			lifetime = this.data.lifetime;
		}

		#region getdata
		new public ProjectileData data {
			get;
			private set;
		}
		#endregion

		protected override void OnGameObjectAttached() {
			base.OnGameObjectAttached();
		}

		public override void Tick() {
			base.Tick();

			if (!hasAuthority) {
				return;
			}

			float dt = world.deltaTime;
			lifetime -= dt;
			if (lifetime <= 0) {
				GameObject.Destroy(go);
				return;
			}
			else {
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
		}

		public virtual void SetPosition(Vector3 p, float interpolateTime = 0) {
			position = p;
		}

	}
}


