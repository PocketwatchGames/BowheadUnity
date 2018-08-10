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
			StatusEffect,
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
			public float stepDistance;
			public float moveSpeedWhileCharging;
			public float moveSpeedWhileFullyCharged;
			public bool canMoveDuringCooldown;
			public bool canMoveDuringCast;
			public bool canMoveDuringActive;
			public bool unblockable;
			public bool interruptOnHit;
			public bool canBackstab;
			public AttackResult attackResult;
			public AttackResult backstabResult;

		}

		public GameObject_WRef prefab;
        public Hand hand;
		public float moveSpeedChargeDelay = 0.2f;
		public List<TraitData> traits = new List<TraitData>();

		public AttackData[] attacks;

		[Header("Spells")]
		public Spell spell;
		public float spellPower;
		public StatusEffectData statusEffect;
		public float statusEffectTime;
		public float waterUse;
		public bool canTarget;
		public Actors.ProjectileData projectile;
		public float projectileSpeed;

		[Header("Defend")]
		public float blockAngleRange;
		public DefendResult blockResult;

		public float sprintSpeed;

		public float jabChargeTime;
	}
}