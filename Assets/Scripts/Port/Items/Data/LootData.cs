using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    [CreateAssetMenuAttribute]
    public class LootData : ItemData {
        public delegate bool UseFn(Item item, Actor actor);
        public UseFn use;
        public float power;
        public int stackSize;
    }

}