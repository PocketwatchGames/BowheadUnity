using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {

    [CreateAssetMenuAttribute(menuName = "ItemData/Weapon")]
    public class WeaponData : ItemData<Weapon, WeaponData> {
        public enum Hand {
            LEFT,
            RIGHT,
            BOTH,
            RANGED
        }

        [System.Serializable]
        public class AttackData {
            public float castTime;
            public float cooldown;
            public float attackRange;
            public float attackRadius;
            public float knockback;
            public float attackDamage;
            public float defendPower;
            public float defendDamageAbsorb;
            public float defendStaminaUse;
            public float staminaUse;
            public float stepDistance;
            public float staminaDrain;
            public float stunPower;
            public float stunPowerBackstab;
            public float attackDamageBackstab;
        }

        public GameObject_WRef prefab;
        public Hand hand;
        public float chargeTime;
        public Vector3 weaponSize;
        public AttackData[] attacks;
        public AttackData[] parries;
    }
}