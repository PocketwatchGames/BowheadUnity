// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {
	public sealed class BleedSpellEffectClass : SpellEffectClass {

		[Flags]
		public enum EBleedFlags {
			Head = 0x1,
			Chest = 0x2,
			Waist = 0x4,
			LFoot = 0x8,
			RFoot = 0x10,
			LHand = 0x20,
			RHand = 0x40,
			Feet = 0x80
		}

		[SerializeField]
		EBleedFlags _flags;
		[SerializeField]
		int _bleedCount;
		[SerializeField]
		float _bleedRate;

#if UNITY_EDITOR
		protected override void OnInitVersion() {
			if (version < 1) {
				_flags = EBleedFlags.Chest;
				_bleedCount = 1;
				_bleedRate = 0;
				spellEffectActorClassString = typeof(BleedSpellEffectActor).FullName;
			}
			base.OnInitVersion();
		}
#endif

		public EBleedFlags flags {
			get {
				return _flags;
			}
		}

		public int bleedCount {
			get {
				return _bleedCount;
			}
		}

		public float bleedRate {
			get {
				return _bleedRate;
			}
		}
	}
}