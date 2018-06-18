using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {

    [Serializable]
    public sealed class WorldItem_WRef : WeakAssetRef<WorldItem> { }

    [CreateAssetMenuAttribute(menuName = "EntityData/WorldItem")]
    public class WorldItemData : EntityData {
        public WorldItem_WRef prefab;

    }
}
