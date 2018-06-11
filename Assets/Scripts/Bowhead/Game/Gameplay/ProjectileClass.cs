// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead.Actors {
	public abstract class ProjectileClass : StaticVersionedAssetWithSerializationCallback {
		public ActorMetaClass metaClass;
		public ActorProperty health;
		public GameObject_WRef clientPrefab;
		public GameObject_WRef serverPrefab;
		public GameObject_WRef mixedPrefab;
		public GameObject_WRef killedByIcePrefab;
		public PhysicalMaterialClass physicalMaterial;
		public bool generatesImpactFx;
		public bool generatesImpactSounds;
		public bool orientToVelocity;
		public float radius;
		[HideInInspector]
		[SerializeField]
		string _projectileClass;

		private Type projectileClass {
			get;
			set;
		}

		public ActorProperty[] properties {
			get;
			private set;
		}

		public override void OnAfterDeserialize() {
			if (health != null) {
				properties = new[] { health };
			} else {
				properties = null;
			}

			if (string.IsNullOrEmpty(_projectileClass)) {
				projectileClass = null;
			} else {
				projectileClass = Type.GetType(_projectileClass);
			}
		}

		public T SpawnAndFireProjectile<T>(Server.Actors.ServerPlayerController player, int level, float damageScale, float spellPower, ActorWithTeam instigator, Vector3 position, Vector3 velocity) where T : ProjectileActor {
			if ((serverPrefab != null) && (clientPrefab != null) && (mixedPrefab != null)) {
				T p = (T)((Actor)instigator).world.Spawn(projectileClass, null, SpawnParameters.defaultParameters);
				p.ConstructProjectileClass(this);
				p.ServerInitActorLevel(level);
				p.ServerFire(player, instigator, position, velocity, damageScale, spellPower);
				return p;
			}
			return null;
		}

		public virtual DamageableActor PredictFriendlyFire(Team team, Vector3 pos) {
			return null;
		}

		public override void ClientPrecache() {
			base.ClientPrecache();
			Utils.PrecacheWithSounds(clientPrefab);
			Utils.PrecacheWithSounds(mixedPrefab);
			Utils.PrecacheWithSounds(killedByIcePrefab);
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			if (version < 1) {
				generatesImpactFx = true;
				generatesImpactSounds = true;
				orientToVelocity = true;
			}
		}
#endif
	}
}
