// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {
	public class IceBarrierAreaOfEffectActor : AreaOfEffectActor<IceBarrierAreaOfEffectActor> {

		List<ProjectileActor> _projectiles;
		float _radius;

		public override void Tick() {
			base.Tick();

			if (_projectiles != null) {
				for (int i = _projectiles.Count-1; i >= 0; --i) {
					var p = _projectiles[i];
					if (p.pendingKill || !IsInside(p)) {
						_projectiles.RemoveAt(i);
						Contact(p);
					}
				}
			}
		}

		protected override void OnGameObjectAttached() {
			base.OnGameObjectAttached();

			if (hasAuthority) {
				_radius = go.GetComponent<SphereCollider>().radius;
			}
		}

		protected override void ServerOnTriggerEnter(DamageableActor actor) {
			var projectile = actor as ProjectileActor;
			if (projectile != null) {
				if (projectile.justSpawnedPhysics) {
					if (_projectiles == null) {
						_projectiles = new List<ProjectileActor>();
					}
					_projectiles.Add(projectile);
				} else {
					projectile.HitIceBarrier();
				}
			} else {
				base.ServerOnTriggerEnter(actor);
			}
		}

		protected override void ServerOnTriggerExit(DamageableActor actor) {
			base.ServerOnTriggerExit(actor);
			var projectile = actor as ProjectileActor;
			if (projectile != null) {
				if (_projectiles != null) {
					_projectiles.Remove(projectile);
					Contact(projectile);
				}
			}
		}

		void Contact(ProjectileActor projectile) {
			if (!(dead || pendingKill || disposed) && !(projectile.dead || projectile.pendingKill)) {
				projectile.HitIceBarrier();
			}
		}

		bool IsInside(ProjectileActor projectile) {
			var dd = projectile.go.transform.position - go.transform.position;
			return dd.magnitude < (_radius - projectile.projectileClass.radius);
		}
	}
}