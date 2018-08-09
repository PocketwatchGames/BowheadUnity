using System;
using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {

    [CreateAssetMenu(menuName = "Status Effect")]
    public class StatusEffectData : EntityData {

		new public static StatusEffectData Get(string name) {
			return DataManager.GetData<StatusEffectData>(name);
		}

        public float healthPerSecond;
		public float staminaPerSecond;
		public float waterPerSecond;

		public bool canStack;

		public List<TraitData> traits = new List<TraitData>();
    }

    public abstract class StatusEffectData<T, D> : StatusEffectData where T : StatusEffect, new() where D : StatusEffectData<T, D>, new()
    {
        new public static D Get(string name) {
			return (D)DataManager.GetData<D>(name);
		}
	}
}