using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenuAttribute(menuName = "ItemData/Loot")]
    public class LootData : ItemData<Loot, LootData> {
        public delegate bool UseFn(Item item, Actor actor);
        public UseFn use;
        public float power;
        public int stackSize;
    }
}