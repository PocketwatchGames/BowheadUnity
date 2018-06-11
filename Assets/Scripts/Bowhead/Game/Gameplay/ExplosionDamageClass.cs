// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace Bowhead.Actors {
	[Serializable]
	public class ExplosionDamageEffect {
		public ExplosionDamageClass damageClass;
		public float baseDamageAmount;
	}

	public class ExplosionDamageClass : DamageClass {
		const int VERSION = 1;

		[Serializable]
		public class Explosion : Channel {}

		public enum EFalloff {
			Linear,
			Exponential
		}

		public EFalloff explosionFalloff;
		public Vector2 explosionMinMaxDistance;
		public Vector2 explosionMinMaxDamageScale;
		
		[EditorFlags]
		public ELayers explosionTargetLayers;
		[EditorFlags]
		public ELayers explosionBlockingLayers;
		public Explosion[] damage;

		public EFalloff shockwaveFalloff;
		[EditorFlags]
		public ELayers shockwaveLayers;
		public Vector2 shockwaveDistance;
		public Vector2 shockwaveScale;
		public float ejectionModifier;

		bool _precached;

		public override void ClientPrecache() {
			base.ClientPrecache();

			if (!_precached) {
				_precached = true;
				Channel.ClientPrecache(damage);
			}
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			if (version < 1) {
				explosionTargetLayers = ELayers.Units;
				explosionBlockingLayers = ELayers.Block|ELayers.Terrain|ELayers.Default;
				shockwaveLayers = ELayers.Ragdoll;
				explosionMinMaxDamageScale.x = 1f;
				explosionMinMaxDamageScale.y = 0f;
			}

			if (damage != null) {
				for (int i = 0; i < damage.Length; ++i) {
					InitChannel(damage[i]);
				}
			}

			version = VERSION;
		}
#endif
	}
}