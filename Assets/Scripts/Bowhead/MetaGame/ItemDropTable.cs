// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.MetaGame {
	public sealed class ItemDropTable : ScriptableObject {
		public struct Drop {
			public Drop(DropItemClass itemClass, int ilvl, int iid) {
				this.itemClass = itemClass;
				this.ilvl = ilvl;
				this.iid = iid;
			}

			public DropItemClass itemClass;
			public int ilvl;
			public int iid;
		}

		[Serializable]
		struct LeveledDrops {
			public int min_ilvl;
			public int max_ilvl;
			[Range(0, 100)]
			public float killBonus;
			[Range(0, 100)]
			public float baseChance;
			public int kills;
			public DropTypes[] dropTable;
		}

		[Serializable]
		struct DropTypes {
			public float probablity;
			public IntMath.Vector2i ilvlRange;
			public ItemMetaClass[] types;
		}

		[SerializeField]
		LeveledDrops[] _dropTables;

		public void GetRandomDropItems(int ilvl, int numKills, float bonus, float additional, List<Drop> drops, ref bool killDrop) {

			if (_dropTables == null) {
				return;
			}

			var rand = GameManager.instance.staticData.randomNumberTable;
			var inventoryItemLibrary = GameManager.instance.staticData.inventoryItemLibrary;

			for (int i = 0; i < _dropTables.Length; ++i) {
				var table = _dropTables[i];

				if ((table.min_ilvl > 0) && (ilvl < table.min_ilvl)) {
					continue;
				}
				if ((table.max_ilvl > 0) && (ilvl > table.max_ilvl)) {
					continue;
				}

				var p = rand.randomValue;

				float chance = table.baseChance;

				if (table.kills > 0) {
					double kills = numKills;
					kills = Math.Min(kills / table.kills, 1);
					kills = Math.Pow(kills, 10);
					chance += (float)Math.Min(table.killBonus, kills*table.killBonus);
				}
				
				if ((p*100) <= ((chance*bonus)+additional)) {

					var dropTable = table.dropTable;

					if (dropTable != null) {
						var totalp = 0.0;

						for (int k = 0; k < dropTable.Length; ++k) {
							var t = dropTable[k];
							totalp += t.probablity;
						}

						if (totalp > 0) {
							var z = 0.0;
							var p2 = (double)rand.randomValue;
							for (int k = 0; k < dropTable.Length; ++k) {
								var t = dropTable[k];
								if (t.probablity > 0) {
									z += t.probablity/totalp;
									if (p2 <= z) {
										if ((t.types != null) && (t.types.Length > 0)) {
											var drop_ilvl = Mathf.Max(1, ilvl + Utils.LerpRange(t.ilvlRange.x, t.ilvlRange.y, rand.randomValue));
											var item = inventoryItemLibrary.GetRandomItemOfType(t.types, drop_ilvl, rand.randomValue);
											var id = item.GetiLvlID(drop_ilvl);
											if (id != TransientItemClass.TRANSIENT_ITEM_ID) {
												drop_ilvl = inventoryItemLibrary.GetIDiLvl(id);
											}
											drops.Add(new Drop(item, drop_ilvl, id));
											if (table.kills > 0) {
												killDrop = true;
											}
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
	}
}