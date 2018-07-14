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
            RANGED
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
			public float staminaDrain;
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
			public float castTime;
			public float activeTime;
			public float cooldown;
			public float cooldownNextAttackQueueTime;
			public float range;
			public float radius;
			public float staminaUse;
			public float stepDistance;
			public bool canRunWhileCharging;
			public bool canMoveDuringCooldown;
			public bool canMoveDuringCast;
			public bool canMoveDuringActive;
			public bool unblockable;
			public Spell spell;
			public float spellPower;
			public float waterUse;
			public Actors.ProjectileData projectile;
			public float projectileSpeed;
			public AttackResult attackResult;
			public AttackResult backstabResult;

			[Header("Defend")]
			public bool canDefend;
			public bool canRunWhileDefending;
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

		public AttackData[] attacks;

	}
}