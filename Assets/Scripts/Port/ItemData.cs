using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    [CreateAssetMenuAttribute]
    public class ItemData : EntityData {

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

        public delegate bool UseFn(Item item, Actor actor);

        public Item.ItemType itemType;
        public int slots;
        public float chargeTime;
        public float power;
        public Vector3 weaponSize;
        public AttackData[] attacks;
        public AttackData[] parries;

        public UseFn use;


    }

}