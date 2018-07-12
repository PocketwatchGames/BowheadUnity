﻿using System.Collections;
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

		protected override void Spawn(WorldItem item, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.Spawn(item, pos, yaw, instigator, owner, team);

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
								Loot loot;
								Money money;
								if ((loot = (item.item as Loot)) != null) {
									loot.count = GameManager.instance.RandomRange(i.count.x, i.count.y);
								}
								else if ((money = (item.item as Money)) != null) {
									money.count = GameManager.instance.RandomRange(i.count.x, i.count.y);
								}
								break;
							}
						}
					}
				}
			}
		}
	}

}

