// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.MetaGame {
	public enum ESocketItemType {
		Rune,
		Gem_Red,
		Gem_Green,
		Gem_Blue,
		Gem_Yellow
	}

	public sealed class InventorySocketItemClass : InventoryGearItemClass {
		[SerializeField]
		ESocketItemType _socket;
		[SerializeField]
		bool _canUnsocket;

		public ESocketItemType socket {
			get {
				return _socket;
			}
		}

		public bool canUnsocket {
			get {
				return _canUnsocket;
			}
		}

		string baseNameKey {
			get {
				var strings = name.Split('_');
				if (strings.Length > 3) {
					return strings[0] + "_" + strings[1];
				}
				return strings[0];
			}
		}

		string baseNameKey2 {
			get {
				var strings = name.Split('_');
				if (strings.Length > 3) {
					if (socket == ESocketItemType.Rune) {
						return strings[1];
					}
				}
				return strings[0];
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

		public override string steamIconName {
			get {
				if (string.IsNullOrEmpty(customIconName)) {
					if (socket == ESocketItemType.Rune) {
						var strings = name.Split('_');
						if (strings.Length > 1) {
							return quality.ToString() + "__" + strings[0] + strings[1] + ".png";
						}
						return base.steamIconName;
					} else {
						var strings = name.Split('_');
						return quality.ToString() + "__" + strings[0] + ".png";
					}
				}
				return customIconName;
			}
		}
	}
}