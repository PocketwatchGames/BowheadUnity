// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.Actors.Spells {

	public class SpellLibrary : ScriptableObject {
		public DeityClass[] deities;
		public AbilityClass[] potions;
		public AbilityClass[] relics;
		public AbilityClass defaultRelic;
		public AbilityClass defaultPotion;

		public DeityClass GetValidSecondaryDeity(DeityClass forPrimary, bool canSelectSelf) {
			if (forPrimary.IsValidSecondary(forPrimary, canSelectSelf)) {
				return forPrimary;
			}

			if (deities != null) {
				for (int i = 0; i < deities.Length; ++i) {
					var d = deities[i];
					if (forPrimary.IsValidSecondary(d, canSelectSelf)) {
						return d;
					}
				}
			}

			return null;
		}

		public bool ContainsDeity(DeityClass deity) {
			if (deities != null) {
				for (int i = 0; i < deities.Length; ++i) {
					if (deities[i] != null) {
						if (deities[i] == deity) {
							return true;
						}
					}
				}
			}
			return false;
		}

		public bool ContainsRelic(AbilityClass spell) {
			if (spell == defaultRelic) {
				return true;
			}
			if (relics != null) {
				for (int i = 0; i < relics.Length; ++i) {
					if (relics[i] != null) {
						if (relics[i] == spell) {
							return true;
						}
					}
				}
			}
			return false;
		}

		public bool ContainsPotion(AbilityClass spell) {
			if (spell == defaultPotion) {
				return true;
			}
			if (potions != null) {
				for (int i = 0; i < potions.Length; ++i) {
					if (potions[i] != null) {
						if (potions[i] == spell) {
							return true;
						}
					}
				}
			}
			return false;
		}
	}

}