using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {

	public class StatusEffect {

		#region getdata

		new public StatusEffectData data {
			get;
			private set;
		}
		#endregion

		public float time;
		public float totalTime;

        public static StatusEffect Create(StatusEffectData d, float t)
        {
            var e = new StatusEffect();
            e.Init(d, t);
            return e;
        }

		private void Init(StatusEffectData d, float t) {
            this.data = (StatusEffectData)data;
            time = totalTime = t;
		}

		public void Tick(float dt) {
			time -= dt;
		}
	}

}
