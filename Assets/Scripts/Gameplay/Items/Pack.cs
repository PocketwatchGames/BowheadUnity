using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    using Pawn = Actors.Pawn;
    public class Pack : Item<Pack, PackData> {

        #region State

        public List<Item> contained = new List<Item>();

        #endregion

        private GameObject _mesh;

        public override void Init(ItemData d) {
            base.Init(d);
			contained.Clear();
        }

        public override void OnSlotChange(int newSlot, Pawn owner) {
            if (_mesh != null) {
                GameObject.Destroy(_mesh);
            }
            if (data.prefab != null && owner != null && newSlot >= 0) {
                var prefab = data.prefab.Load();
                _mesh = GameObject.Instantiate(prefab, owner.go.transform, false);
            }
        }

    }
}
