using System;
using UnityEngine;

namespace Bowhead {

    [CreateAssetMenu(menuName = "Status Effect")]
    public class StatusEffectData : EntityData {

		new public static StatusEffectData Get(string name) {
			return DataManager.GetData<StatusEffectData>(name);
		}

        public float healthPerSecond;
        public float maxHealthBonus;

        public float staminaPerSecond;
        public float maxStaminaBonus;

        public float damageMultiplier;
		public bool canStack;
    }

    public abstract class StatusEffectData<T, D> : StatusEffectData where T : StatusEffect, new() where D : StatusEffectData<T, D>, new()
    {
        new public static D Get(string name) {
			return (D)DataManager.GetData<D>(name);
		}
	}
}