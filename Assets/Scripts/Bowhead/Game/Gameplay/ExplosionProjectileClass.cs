// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.Actors {
	public class ExplosionProjectileClass : ProjectileClass {
		const int VERSION = 1;

		public ExplosionDamageEffect damage;
		public GameObject_WRef explosionPrefab;
		public Vector2 fuseTime;
		[Range(0, 100)]
		public float dudChance;
		public float additionalDudChanceMultiplier;
		[Range(0, 100)]
		public float explodeOnContactChance;
		[Range(0, 100)]
		public float additionalContactChance;
		public bool dudInWater;
		public bool canExplodeInWater;
		public float dragInWater;
		[Range(0, 1)]
		public float velocityChangeInWater;

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			if (version < 1) {
				velocityChangeInWater = 1;
				dragInWater = 1;
				dudInWater = true;
			}

			version = VERSION;
		}
#endif
		
		public override DamageableActor PredictFriendlyFire(Team team, Vector3 pos) {
			/*
			List<DamageableActor> hitActors = new List<DamageableActor>();

			// move the hit pos up slightly so we don't get false line-casts on the ground.
			var groundPos = Utils.PutPositionOnGround(pos);
			if (groundPos.y >= pos.y-0.01f) {
				pos = groundPos + new Vector3(0, 0.05f, 0); // was underground!
			}

			var components = Physics.OverlapSphere(pos, damage.damageClass.explosionMinMaxDistance.y, Layers.GetTeamLayerMask(team.teamNumber));
			if (components.Length > 0) {
				for (int i = 0; i < components.Length; ++i) {
					var c = components[i];
					var a = (DamageableActor)c.transform.FindServerActorUpwards();

					if ((a != null) && !(a.dead || a.pendingKill) && a.IsFriendly(team)) {
						var colliderCenter = c.GetWorldSpaceCenter();

						if ((damage.damageClass.explosionBlockingLayers == 0) || !Physics.Linecast(pos, colliderCenter, damage.damageClass.explosionBlockingLayers.ToLayerMask())) {					
#if UNITY_EDITOR
							Debug.DrawLine(pos, colliderCenter, Color.red, 30f);
#endif
							return a;
						}

#if UNITY_EDITOR
						Debug.DrawLine(pos, colliderCenter, Color.green, 30f);
#endif
					}
				}
			}
			*/

			return null;
		}
	}
}