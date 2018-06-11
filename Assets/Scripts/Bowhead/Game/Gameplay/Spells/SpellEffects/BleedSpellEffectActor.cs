// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {
	public sealed class BleedSpellEffectActor : SpellEffectActor<BleedSpellEffectActor> {

		float _nextBleed;
		int _bleedCount;
		bool _done;

		public override void Tick() {
			base.Tick();

			if (!hasAuthority && !_done) {
				_nextBleed -= world.deltaTime;
				if (_nextBleed <= 0) {
					_nextBleed = effectClass.bleedRate;
					++_bleedCount;
					_done = (effectClass.bleedCount > 0) && (_bleedCount >= effectClass.bleedCount);
					Bleed();
				}
			}
		}

		void Bleed() {
			var locations = target.attachmentLocations;
			var flags = effectClass.flags;

			if ((flags&BleedSpellEffectClass.EBleedFlags.Head) != 0) {
				Bleed(locations.head);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.Chest) != 0) {
				Bleed(locations.chest);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.Waist) != 0) {
				Bleed(locations.waist);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.LHand) != 0) {
				Bleed(locations.leftHand);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.RHand) != 0) {
				Bleed(locations.rightHand);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.LFoot) != 0) {
				Bleed(locations.leftFoot);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.RFoot) != 0) {
				Bleed(locations.rightFoot);
			}
			if ((flags&BleedSpellEffectClass.EBleedFlags.Feet) != 0) {
				Bleed(locations.feet);
			}
		}

		void Bleed(Transform t) {
			if (t != null) {
				target.SpawnBloodSpray(t, t.position, Quaternion.identity);
			}
		}

		public new BleedSpellEffectClass effectClass {
			get {
				return (BleedSpellEffectClass)base.effectClass;
			}
		}
	}
}