using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {

    [CreateAssetMenu(menuName = "ItemData/Weapon")]
    public class WeaponData : ItemData<Weapon, WeaponData> {
        public enum Hand {
            LEFT,
            RIGHT,
            BOTH,
        }

		[System.Serializable]
		public class ProjectileData {
			public float speed;
			public float lifetime;
			public float damage;
			public float stun;
			public bool heatSeeking;
			public bool autoAimYaw;
			public bool autoAimPitch;
			public Actors.PawnData.DamageType damageType;
			public StatusEffectData statusEffect;
			public float statusEffectTime;
			public GameObject_WRef prefab;

		}

		[System.Serializable]
		public class AttackResult {
			public float knockback;
			public float knockbackTime;
			public float damage;
			public float stun;
			public bool interrupt;
			public bool unblockable;
			public Actors.PawnData.DamageType damageType;
			public StatusEffectData statusEffect;
			public float statusEffectTime;
			public float loudness;
		}

		[System.Serializable]
		public class DefendResult : AttackResult {
			public float stunAbsorb;
			public float damageAbsorb;
			public float staminaUse;
		}

		[System.Serializable]
		public class AttackData {
			[Header("Attack")]
			public float chargeTime;
			public float maxCharge;
			public float castTime;
			public float activeTime;
			public float cooldown;
			public float cooldownNextAttackQueueTime;
			public float range;
			public float radius;
			public float staminaUse;
			public float stepDistance;
			public float moveSpeedWhileCharging;
			public float moveSpeedWhileFullyCharged;
			public bool canMoveDuringCooldown;
			public bool canMoveDuringCast;
			public bool canMoveDuringActive;
			public bool interruptOnHit;
			public bool canBackstab;
			public bool canTarget;

			[Header("AttackResults")]
			public AttackResult attackResultDirectHit;
			public AttackResult attackResultGlancingBlow;
			public AttackResult attackResultBackstab;
			public List<ProjectileData> projectiles = new List<ProjectileData>();
			public StatusEffectData statusEffect;
			public float statusEffectTime;

		}

		public GameObject_WRef prefab;
        public Hand hand;
		public float staminaRechargeTime = 1;
		public float staminaRechargePause;
		public float staminaUseDuringCharge;
		public float moveSpeedChargeDelay = 0.2f;
		public List<TraitData> traits = new List<TraitData>();

		public AttackData[] attacks;

		[Header("Defend")]
		public float blockAngleRange;
		public DefendResult blockResultDirectHit;
		public DefendResult blockResultGlancingBlow;
		public float sprintSpeed;
		public float jabChargeTime;
	}
}