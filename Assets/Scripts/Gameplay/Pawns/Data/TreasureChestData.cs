using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/TreasureChest")]
    public class TreasureChestData : WorldItemData<TreasureChestData>, ISpawnPointSupport {
		[Serializable]
		struct LootItem {
			public ItemData itemClass;
			[MinMaxSlider(1, 100)]
			public IntMath.Vector2i count;
			public float probability;
		};

		[SerializeField]
		LootItem[] _items;

		protected override void Spawn(WorldItem item, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(item, pos, instigator, owner, team);

			if ((_items != null) && (_items.Length > 0)) {
				float totalp = 0f;

				foreach (var i in _items) {
					if (i.itemClass != null) {
						totalp += i.probability;
					}
				}

				if (totalp > 0) {
					var p = 0f;
					var r = GameManager.instance.randomNumber;

					foreach (var i in _items) {
						if ((i.probability > 0) && (i.itemClass != null)) {
							p += i.probability/totalp;
							if (r <= p) {
								item.item = i.itemClass.CreateItem();
								break;
							}
						}
					}
				}
			}
		}
	}

}

