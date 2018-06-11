// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead.MetaGame {
	[Serializable]
	public class GearItemAffix {
		public Actors.Spells.SpellCastRule[] spells;
	}

	public abstract class InventoryGearItemClass : InventoryItemClass {
		[SerializeField]
		ItemStat[] _stats;
		[SerializeField]
		GearItemAffix[] _affixes;
		[SerializeField]
		float _essenceBonus;

		public float SumItemStats(ItemStatClass itemStatClass, int ilvl) {
			float sum = 0f;

			if (_stats != null) {
				for (int i = 0; i < _stats.Length; ++i) {
					var stat = _stats[i];
					if (stat.itemStatClass == itemStatClass) {
						sum += stat.GetScaledValue(ilvl);
					}
				}
			}

			return sum;
		}

		public ItemStat[] stats {
			get {
				return _stats;
			}
		}

		public GearItemAffix[] affixes {
			get {
				return _affixes;
			}
		}

		public float essenceBonus {
			get {
				return _essenceBonus;
			}
		}

	}
}