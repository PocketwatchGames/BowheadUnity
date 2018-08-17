using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenu(menuName = "ItemData/Loot")]
    public class LootData : ItemData<Loot, LootData> {
		public enum UseType {
			None,
			Food,
		}
		public UseType useType;
        public float power;
        public int stackSize;

        public StatusEffectData statusEffect;
        public float statusEffectTime;

	}
}