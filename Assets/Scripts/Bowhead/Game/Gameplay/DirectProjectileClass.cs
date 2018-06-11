// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.Actors {
	public enum EProjectileBreak {
		Disabled,
		BreakOnDefaultTerrainBlocker,
		BreakOnActor,
		Always
	}

	public class DirectProjectileClass : ProjectileClass {
		const int VERSION = 1;

		public DirectDamageEffect damage;
		public Spells.AreaOfEffectClass groundHitAOE;
		public bool canProcGroundAOEInWater;
		[MinMaxSlider(0, 2)]
		public Vector2 penetration;
		[MinMaxSlider(0, 999)]
		public Vector2 impactForce;
		public float damageBonusPerMeterTraveled;

		public bool embed;
		public EProjectileBreak _break;

		public override void ClientPrecache() {
			base.ClientPrecache();
			damage.ClientPrecache();
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			version = VERSION;
		}
#endif
	}
}