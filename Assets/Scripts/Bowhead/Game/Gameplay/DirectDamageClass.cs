// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections.Generic;

namespace Bowhead.Actors {

	[Serializable]
	public class DirectDamageEffect {
		public DirectDamageClass damageClass;
		public float baseDamageAmount;

		public void ClientPrecache() {
			if (damageClass != null) {
				damageClass.ClientPrecache();
			}
		}
	}

	[Serializable]
	public struct DirectDamageEffects {
		public DirectDamageEffect self;
		public DirectDamageEffect target;

		public void ClientPrecache() {
			self.ClientPrecache();
			target.ClientPrecache();
		}

		public static void ClientPrecache(IList<DirectDamageEffects> effects) {
			if (effects != null) {
				for (int i = 0; i < effects.Count; ++i) {
					effects[i].ClientPrecache();
				}
			}
		}
	}

	public class DirectDamageClass : DamageClass {
		const int VERSION = 1;

		[Serializable]
		public class Direct : Channel {}
		
		public Direct[] damage;

		bool _precached;

		public override void ClientPrecache() {
			base.ClientPrecache();

			if (!_precached) {
				_precached = true;
				Channel.ClientPrecache(damage);
			}
		}

#if UNITY_EDITOR
		protected override void InitVersion() {
			base.InitVersion();

			if (damage != null) {
				for (int i = 0; i < damage.Length; ++i) {
					InitChannel(damage[i]);					
				}
			}

			version = VERSION;
		}
#endif
	}
}