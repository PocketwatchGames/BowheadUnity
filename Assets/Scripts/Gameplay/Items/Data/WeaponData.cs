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
        public class AttackData {
            public float castTime;
			public float activeTime;
            public float cooldown;
            public float attackRange;
            public float attackRadius;
            public float knockback;
            public float attackDamage;
            public float defendPower;
            public float defendDamageAbsorb;
            public float defendStaminaUse;
			public float defendAngleRange;
            public float staminaUse;
            public float stepDistance;
            public float staminaDrain;
            public float stunPower;
            public float stunPowerBackstab;
            public float attackDamageBackstab;
			public bool canMoveDuringCooldown;
			public bool canMoveDuringCast;
			public bool canMoveDuringActive;
			public bool interrupt;
			public bool unblockable;
			public bool defendInterrupt;
			public Spell spell;
			public float spellPower;
			public float waterUse;
			public Actors.ProjectileData projectile;
			public float projectileSpeed;
		}

		public GameObject_WRef prefab;
        public Hand hand;
        public float chargeTime;
		public float parryTime;
		public bool canRunWhileCharging;
		public float moveSpeedChargeDelay = 0.2f;
        public Vector3 weaponSize;
		public AttackData[] attacks;
		public AttackData parry;

    }
}