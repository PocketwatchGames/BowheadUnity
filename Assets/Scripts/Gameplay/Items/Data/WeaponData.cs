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
            RANGED,
			ARMOR
        }

		public enum Spell {
			None,
			Heal,
		}


		[System.Serializable]
		public class AttackResult {
			public float knockback;
			public float damage;
			public float stun;
			public bool interrupt;
			public Actors.PawnData.DamageType damageType;
			public StatusEffectData statusEffect;
			public float statusEffectTime;
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
			public float dodgeSpeed;
			public float dodgeTime;
			public float stepDistance;
			public float moveSpeedWhileCharging;
			public float moveSpeedWhileFullyCharged;
			public bool canMoveDuringCooldown;
			public bool canMoveDuringCast;
			public bool canMoveDuringActive;
			public bool unblockable;
			public bool interruptOnHit;
			public bool canBackstab;

			public Spell spell;
			public float spellPower;
			public float waterUse;
			public bool canTarget;
			public Actors.ProjectileData projectile;
			public float projectileSpeed;
			public AttackResult attackResult;
			public AttackResult backstabResult;

			[Header("Defend")]
			public bool canDefend;
			public float defendAngleRange;
			public DefendResult defendResult;

			[Header("Parry")]
			public bool canParry;
			public float parryTime;
			public DefendResult parryResult;

		}

		public GameObject_WRef prefab;
        public Hand hand;
		public float moveSpeedChargeDelay = 0.2f;
		public float maxHealthBonus;
		public float maxStaminaBonus;

		public AttackData[] attacks;

	}
}