using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Pack : Item {

        #region State

        public List<Item> contained = new List<Item>();

        #endregion

        new public PackData Data { get { return GetData<PackData>(); } }
        public static PackData GetData(string dataName) { return DataManager.GetItemData<PackData>(dataName); }

        public override void init(ItemData d, World w) {
            base.init(d, w);

            contained.Clear();
        }
    }
}
