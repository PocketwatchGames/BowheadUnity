// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead.Actors.Spells {

	public class DeityClass : StaticVersionedAsset {
		public const int SWAP_SPELLS_FLAG = 0x40000000;

		public Sprite_WRef icon;
		public Sprite_WRef icon2;
		public Sprite_WRef portrait;
		public SoundCue selectedSound;
		public Color color;
		public Progression progression;
		public AbilityClass[] mpAbilities;
		public DeityClass[] excludedSecondaries;

		bool _precached;

		[Serializable]
		public struct Unlock {
			public float xpNeeded;
			public AbilityClass spell;
			public bool hasPreReq;
		}

		[Serializable]
		public struct ProgressionLine {
			public Unlock[] unlocks;
		}

		[Serializable]
		public struct Progression {
			public ProgressionLine[] lines;
		}

		public bool IsValidSecondary(DeityClass deity, bool canSelectSelf) {
			if (deity == this) {
				return canSelectSelf;
			}

			if ((excludedSecondaries != null) && (excludedSecondaries.Length > 0)) {
				for (int i = 0; i < excludedSecondaries.Length; ++i) {
					if (excludedSecondaries[i] == deity) {
						return false;
					}
				}
			}

			return true;
		}

		public void GetMaskedSpells(int mask, List<AbilityClass> out_spells, int max) {

			var bit = 1;
			var num = 0;

			if (progression.lines != null) {
				for (int i = 0; i < progression.lines.Length; ++i) {
					var line = progression.lines[i];
					if (line.unlocks != null) {
						for (int k = 0; k < line.unlocks.Length; ++k) {
							var unlock = line.unlocks[k];

							if ((bit&mask) != 0) {
								if (unlock.spell != null) {
									out_spells.Add(unlock.spell);
									++num;
									if (num >= max) {
										break;
									}
								}
							}

							bit <<= 1;
						}
					}
					if (num >= max) {
						break;
					}
				}
			}

			if ((mask & SWAP_SPELLS_FLAG) != 0) {
				if (out_spells.Count > 1) {
					var t = out_spells[0];
					out_spells[0] = out_spells[1];
					out_spells[1] = t;
				}
			}
		}

		public int ValidateMask(int mask, int xp, int req, int invalid) {
			var valid = mask & SWAP_SPELLS_FLAG;
			var bit = 1;
			var num = 0;

			mask &= ~(SWAP_SPELLS_FLAG | invalid);

			var xpFrac = xp / (float)GameManager.instance.staticData.xpTable.deityMaxXP;

			if (progression.lines != null) {
				for (int i = 0; i < progression.lines.Length; ++i) {
					var line = progression.lines[i];
					var prereq = false;

					if (line.unlocks != null) {
						for (int k = 0; k < line.unlocks.Length; ++k) {
							var unlock = line.unlocks[k];

							if ((mask&bit) != 0) {
								if (unlock.spell != null) {
									if (xpFrac >= unlock.xpNeeded) {
										if (!unlock.hasPreReq || prereq) {
											valid |= bit;
											++num;
											if (num >= req) {
												return valid;
											}
										}
									}
								}
							}

							prereq = ((valid|invalid)&bit) != 0;
							bit <<= 1;
						}
					}
				}

				bit = 1;

				{
					// must have the number required
					for (int i = 0; i < progression.lines.Length; ++i) {
						var line = progression.lines[i];

						var prereq = false;

						if (line.unlocks != null) {
							for (int k = 0; k < line.unlocks.Length; ++k) {
								var unlock = line.unlocks[k];
								if (((valid&bit) == 0) && ((invalid&bit) == 0) && (xpFrac >= unlock.xpNeeded) && (!unlock.hasPreReq || prereq)) {
									valid |= bit;
									++num;
									if (num >= req) {
										return valid;
									}
								}

								prereq = ((valid|invalid)&bit) != 0;
								bit <<= 1;
							}
						}
					}
				}
			}

			return valid;
		}

		public bool HasUnlocked(int xp, int invalid, int req) {
			var num = 0;
			var bit = 1;
			var valid = 0;

			var xpFrac = xp / (float)GameManager.instance.staticData.xpTable.deityMaxXP;

			// must have the number required
			for (int i = 0; i < progression.lines.Length; ++i) {
				var line = progression.lines[i];
				var prereq = false;

				if (line.unlocks != null) {
					for (int k = 0; k < line.unlocks.Length; ++k) {
						var unlock = line.unlocks[k];
						if ((((valid|invalid)&bit) == 0) && (xpFrac >= unlock.xpNeeded) && (!unlock.hasPreReq || prereq)) {
							valid |= bit;
							++num;
							if (num >= req) {
								return true;
							}
						}

						prereq = ((valid|invalid)&bit) != 0;
						bit <<= 1;
					}
				}
			}

			return false;
		}

		public override void ClientPrecache() {
			if (!_precached) {
				_precached = true;
				base.ClientPrecache();
				WeakAssetRef.Precache(icon);
				WeakAssetRef.Precache(icon2);
				WeakAssetRef.Precache(portrait);

				if (progression.lines != null) {
					for (int i = 0; i < progression.lines.Length; ++i) {
						var line = progression.lines[i];
						if (line.unlocks != null) {
							for (int k = 0; k < line.unlocks.Length; ++k) {
								var unlock = line.unlocks[k];
								if (unlock.spell != null) {
									unlock.spell.ClientPrecache();
								}
							}
						}
					}
				}
			}
		}

		public void ClientPrecache(IList<DeityClass> classes) {
			if (classes != null) {
				for (int i = 0; i < classes.Count; ++i) {
					var c = classes[i];
					if (c != null) {
						c.ClientPrecache();
					}
				}
			}
		}

		public string localizedName {
			get {
				return Utils.GetLocalizedText("UI.HUD.Deity.Name." + name);
			}
		}

		public string localizedDescription {
			get {
				return Utils.GetLocalizedText("UI.HUD.Deity.Description." + name);
			}
		}

		public bool isMPDeity {
			get {
				return (mpAbilities != null) && (mpAbilities.Length > 0);
			}
		}
	}
}
