// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead.Actors {

	[Flags]
	public enum EUnitActionCueSlot {
		HighLeft = 0x1,
		HighRight = 0x2,
		HighCenter = 0x4,
		MidLeft = 0x8,
		MidRight = 0x10,
		MidCenter = 0x20,
		LowLeft = 0x40,
		LowRight = 0x80,
		LowCenter = 0x100
	}

	[Flags]
	public enum EUnitActionCueSlotExplosion {
		HighLeft = 0x1,
		HighRight = 0x2,
		HighCenter = 0x4,
		MidLeft = 0x8,
		MidRight = 0x10,
		MidCenter = 0x20,
		LowLeft = 0x40,
		LowRight = 0x80,
		LowCenter = 0x100,
		ExplosionFront = 0x200,
		ExplosionBack = 0x400
	}

	public static class UnitActionCueSlotExplosionExtensions {
		public static bool IsExplosion(this EUnitActionCueSlotExplosion slot) {
			return (slot == EUnitActionCueSlotExplosion.ExplosionFront) ||
				(slot == EUnitActionCueSlotExplosion.ExplosionBack);
		}
		public static EUnitActionCueSlot Mirror(this EUnitActionCueSlot slot) {
			if (slot == EUnitActionCueSlot.HighLeft) {
				return EUnitActionCueSlot.HighRight;
			}
			if (slot == EUnitActionCueSlot.HighRight) {
				return EUnitActionCueSlot.HighLeft;
			}
			if (slot == EUnitActionCueSlot.MidLeft) {
				return EUnitActionCueSlot.MidRight;
			}
			if (slot == EUnitActionCueSlot.MidRight) {
				return EUnitActionCueSlot.MidLeft;
			}
			if (slot == EUnitActionCueSlot.LowLeft) {
				return EUnitActionCueSlot.LowRight;
			}
			if (slot == EUnitActionCueSlot.LowRight) {
				return EUnitActionCueSlot.LowLeft;
			}
			return slot;
		}
		public static EUnitActionCueSlotExplosion Mirror(this EUnitActionCueSlotExplosion slot) {
			return (EUnitActionCueSlotExplosion)Mirror((EUnitActionCueSlot)slot);
		}
	}

	[Serializable]
	public struct UnitActionCueSet {
		[SerializeField]
		float _probability;
		public UnitActionCue[] cues;

		public float probability {
			get;
			private set;
		}

		public bool hasAnyCues {
			get;
			private set;
		}

		public UnitActionCue Select(float random) {
			return UnitActionCue.SelectCue(cues, random);
		}

		public void OnAfterDeserialize() {
			hasAnyCues = false;
			if (cues != null) {
				for (int i = 0; i < cues.Length; ++i) {
					if (cues[i] != null) {
						hasAnyCues = true;
						break;
					}
				}
			}

			if (hasAnyCues) {
				probability = _probability;
			}
		}

#if UNITY_EDITOR
		public void InitVersion() {
			_probability = 1f;
		}
#endif
	}

	[Serializable]
	public struct UnitExplosionCues {
		public UnitActionCue[] explodeFront;
		public UnitActionCue[] explodeBack;

		public UnitActionCue Select(float random, EUnitActionCueSlotExplosion slot) {
			switch (slot) {
				case EUnitActionCueSlotExplosion.ExplosionFront:
					return UnitActionCue.SelectCue(explodeFront, random);
				case EUnitActionCueSlotExplosion.ExplosionBack:
					return UnitActionCue.SelectCue(explodeBack, random);
			}
			return null;
		}
	}

	[Serializable]
	public struct UnitActionCues {
		public UnitActionCueSet highLeft;
		public UnitActionCueSet highRight;
		public UnitActionCueSet highCenter;
		public UnitActionCueSet midLeft;
		public UnitActionCueSet midRight;
		public UnitActionCueSet midCenter;
		public UnitActionCueSet lowLeft;
		public UnitActionCueSet lowRight;
		public UnitActionCueSet lowCenter;

		float totalp;

		public UnitActionCue Select(float random, float random2, EUnitActionCueSlot validSlots, out EUnitActionCueSlot slot) {
			slot = EUnitActionCueSlot.MidCenter;

			if (totalp <= 0f) {
				return null;
			}

			float p = 0f;

			UnitActionCue cue = null;

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.HighLeft) != 0)) {
				cue = Choose(ref p, highLeft, random, random2, EUnitActionCueSlot.HighLeft, ref slot);
            }

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.HighRight) != 0)) {
				cue = Choose(ref p, highRight, random, random2, EUnitActionCueSlot.HighRight, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.HighCenter) != 0)) {
				cue = Choose(ref p, highCenter, random, random2, EUnitActionCueSlot.HighCenter, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.MidLeft) != 0)) {
				cue = Choose(ref p, midLeft, random, random2, EUnitActionCueSlot.MidLeft, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.MidRight) != 0)) {
				cue = Choose(ref p, midRight, random, random2, EUnitActionCueSlot.MidRight, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.MidCenter) != 0)) {
				cue = Choose(ref p, midCenter, random, random2, EUnitActionCueSlot.MidCenter, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.LowLeft) != 0)) {
				cue = Choose(ref p, lowLeft, random, random2, EUnitActionCueSlot.LowLeft, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.LowRight) != 0)) {
				cue = Choose(ref p, lowRight, random, random2, EUnitActionCueSlot.LowRight, ref slot);
			}

			if ((cue == null) && ((validSlots&EUnitActionCueSlot.LowCenter) != 0)) {
				cue = lowCenter.Select(random2);
				slot = EUnitActionCueSlot.LowCenter;
			}
			
			return cue;
        }

		public UnitActionCue Select(float random, EUnitActionCueSlot validSlots) {
			if (totalp <= 0f) {
				return null;
			}

			UnitActionCue cue = null;

			if ((validSlots&EUnitActionCueSlot.HighLeft) != 0) {
				cue = highCenter.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.HighRight) != 0) {
				cue = highRight.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.HighCenter) != 0) {
				cue = highCenter.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.MidLeft) != 0) {
				cue = midLeft.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.MidRight) != 0) {
				cue = midRight.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.MidCenter) != 0) {
				cue = midCenter.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.LowLeft) != 0) {
				cue = lowLeft.Select(random);
			} else if ((validSlots&EUnitActionCueSlot.LowRight) != 0) {
				cue = lowRight.Select(random);
			} else {
				cue = lowCenter.Select(random);
			}

			return cue;
		}

		UnitActionCue Choose(ref float p, UnitActionCueSet set, float random, float random2, EUnitActionCueSlot inSlot, ref EUnitActionCueSlot setSlot) {
			if (set.hasAnyCues) {
				var x = p + (set.probability / totalp);
				if (random <= x) {
					setSlot = inSlot;
					return set.Select(random2);
				}
				p = x;
			}
			return null;
		}

		public void OnAfterDeserialize() {
			highLeft.OnAfterDeserialize();
			highRight.OnAfterDeserialize();
			highCenter.OnAfterDeserialize();
			midLeft.OnAfterDeserialize();
			midRight.OnAfterDeserialize();
			midCenter.OnAfterDeserialize();
			lowLeft.OnAfterDeserialize();
			lowRight.OnAfterDeserialize();
			lowCenter.OnAfterDeserialize();

			totalp = highLeft.probability;
			totalp += highRight.probability;
			totalp += highCenter.probability;
			totalp += midLeft.probability;
			totalp += midRight.probability;
			totalp += midCenter.probability;
			totalp += lowLeft.probability;
			totalp += lowRight.probability;
			totalp += lowCenter.probability;
		}

#if UNITY_EDITOR
		public void InitVersion() {
			highLeft.InitVersion();
			highRight.InitVersion();
			highCenter.InitVersion();
			midLeft.InitVersion();
			midRight.InitVersion();
			midCenter.InitVersion();
			lowLeft.InitVersion();
			lowRight.InitVersion();
			lowCenter.InitVersion();
        }
#endif
	}

	[Serializable]
	public struct UnitActionResponseCues {
		public bool wildcardAny;
		public UnitActionCue[] any;
		public UnitActionCue[] highLeft;
		public UnitActionCue[] highRight;
		public UnitActionCue[] highCenter;
		public UnitActionCue[] midLeft;
		public UnitActionCue[] midRight;
		public UnitActionCue[] midCenter;
		public UnitActionCue[] lowLeft;
		public UnitActionCue[] lowRight;
		public UnitActionCue[] lowCenter;

		public UnitActionCue SelectCue(EUnitActionCueSlot slot, float random) {
			UnitActionCue cue = null;

			switch (slot) {
				case EUnitActionCueSlot.HighLeft:
					cue = UnitActionCue.SelectCue(highLeft, random);
				break;
				case EUnitActionCueSlot.HighRight:
					cue = UnitActionCue.SelectCue(highRight, random);
				break;
				case EUnitActionCueSlot.HighCenter:
					cue = UnitActionCue.SelectCue(highCenter, random);
				break;
				case EUnitActionCueSlot.MidLeft:
					cue = UnitActionCue.SelectCue(midLeft, random);
				break;
				case EUnitActionCueSlot.MidRight:
					cue = UnitActionCue.SelectCue(midRight, random);
				break;
				case EUnitActionCueSlot.MidCenter:
					cue = UnitActionCue.SelectCue(midCenter, random);
				break;
				case EUnitActionCueSlot.LowLeft:
					cue = UnitActionCue.SelectCue(lowLeft, random);
				break;
				case EUnitActionCueSlot.LowRight:
					cue = UnitActionCue.SelectCue(lowRight, random);
				break;
				case EUnitActionCueSlot.LowCenter:
					cue = UnitActionCue.SelectCue(lowCenter, random);
				break;
			}

			if ((cue == null) && wildcardAny) {
				cue = UnitActionCue.SelectCue(any, random);
			}

			return cue;
		}

#if UNITY_EDITOR
		public void InitVersion() {
			wildcardAny = true;
		}
#endif
	}

	[Serializable]
	public struct UnitActionCueAnim {
		public string trigger;
		public float contactTime;
		public float duration;
		public SoundCue[] triggerSounds;
		[NonSerialized]
		[HideInInspector]
		public int triggerID;
	}

	public enum EUnitActionCueAnim {
		Hit,
		Blocked,
		Parried
	}

	public class UnitActionCue : StaticVersionedAssetWithSerializationCallback {
		public float probability;

		public UnitActionCueAnim animHit;
		public UnitActionCueAnim animBlocked;
		public UnitActionCueAnim animParried;
						
#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();
			if (version < 1) {
				probability = 1f;
			}
			version = 1;
		}
#endif

		public UnitActionCueAnim GetCueAnim(EUnitActionCueAnim anim) {
			switch (anim) {
				case EUnitActionCueAnim.Hit:
					return animHit;
				case EUnitActionCueAnim.Blocked:
					return animBlocked;
			}
			return animParried;
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(animHit.trigger)) {
				animHit.triggerID = 0;
			} else {
				animHit.triggerID = Animator.StringToHash(animHit.trigger);
			}

			if (string.IsNullOrEmpty(animBlocked.trigger)) {
				animBlocked.triggerID = 0;
			} else {
				animBlocked.triggerID = Animator.StringToHash(animBlocked.trigger);
			}

			if (string.IsNullOrEmpty(animParried.trigger)) {
				animParried.triggerID = 0;
			} else {
				animParried.triggerID = Animator.StringToHash(animParried.trigger);
			}
		}

		public static UnitActionCue SelectCue(UnitActionCue[] cues, float random) {
			if (cues == null) {
				return null;
			}

			float totalp = 0f;
			for (int i = 0; i < cues.Length; ++i) {
				var c = cues[i];
				if (c != null) {
					totalp += c.probability;
				}
			}

			if (totalp <= 0f) {
				return null;
			}

			float p = 0f;
			for (int i = 0; i < cues.Length; ++i) {
				var c = cues[i];
				if (c != null) {
					var x = p + (c.probability/totalp);
					if (random <= x) {
						return c;
					}
					p = x;
				}
			}

			return null;
		}
	}
}