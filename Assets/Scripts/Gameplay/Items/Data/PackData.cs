using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenu(menuName = "ItemData/Pack")]
    public class PackData : ItemData<Pack, PackData> {
        public int slots;
        public GameObject_WRef prefab;
    }
}