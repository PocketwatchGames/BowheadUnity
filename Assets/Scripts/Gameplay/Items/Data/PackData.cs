﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenuAttribute(menuName = "ItemData/Pack")]
    public class PackData : ItemData<Pack, PackData> {
        public int slots;
    }
}