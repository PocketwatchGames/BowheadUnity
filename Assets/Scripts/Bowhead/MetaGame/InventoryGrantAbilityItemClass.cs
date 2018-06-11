// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.MetaGame {
	public enum EGrantAbilityItemType {
		Relic,
		Potion
	}

	public sealed class InventoryGrantAbilityItemClass : InventoryItemClass {
		[SerializeField]
		Actors.Spells.AbilityClass _abilityClass;
		[SerializeField]
		EGrantAbilityItemType _itemType;

		public Actors.Spells.AbilityClass abilityClass {
			get {
				return _abilityClass;
			}
		}

		public EGrantAbilityItemType itemType {
			get {
				return _itemType;
			}
		}

		string baseNameKey {
			get {
				var strings = name.Split('_');
				if (strings.Length > 1) {
					return strings[0] + "_" + strings[1];
				}
				return name;
			}
		}
		string baseNameKey2 {
			get {
				var strings = name.Split('_');
				if (strings.Length > 1) {
					if (itemType == EGrantAbilityItemType.Relic) {
						return strings[0] + "_" + strings[1];
					}
					return "Potion_" + strings[1];
				}
				return name;
			}
		}

		public override string nameKey {
			get {
				if (string.IsNullOrEmpty(customNameKey)) {
					return "ItemDef." + baseNameKey + ".Name";
				}
				return customNameKey;
			}
		}

		public override string descriptionKey {
			get {
				if (string.IsNullOrEmpty(customDescKey)) {
					return "ItemDef." + baseNameKey2 + ".Description";
				}
				return customDescKey;
			}
		}

		public override float descParam1 {
			get {
				return _abilityClass.descParam1;
			}
		}

		public override float descParam2 {
			get {
				return _abilityClass.descParam2;
			}
		}

		public override float descParam3 {
			get {
				return _abilityClass.activeSoulStoneCost;
			}
		}

		public override string steamIconName {
			get {
				if (string.IsNullOrEmpty(customIconName)) {
					var strings = name.Split('_');
					if (strings.Length > 1) {
						if (itemType == EGrantAbilityItemType.Potion) {
							return quality.ToString() + "__" + strings[0] + "Of" + strings[1] + ".png";
						}
						return quality.ToString() + "__" + strings[0] + strings[1] + ".png";
					}
					return base.steamIconName;
				}
				return customIconName;
			}
		}
	}
}