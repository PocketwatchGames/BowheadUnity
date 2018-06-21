using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    public class Pack : Item<Pack, PackData> {

        #region State

        public List<Item> contained = new List<Item>();

        #endregion

        public override void Init(ItemData d) {
            base.Init(d);
			contained.Clear();
        }
    }
}
