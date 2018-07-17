using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {


	public abstract class StatusEffect<T, D> : StatusEffect where T : StatusEffect<T, D> where D : StatusEffectData {

		protected override void ConstructEntity(EntityData data) {
			base.ConstructEntity(data);
			this.data = (D)data;
		}

		new public D data {
			get;
			private set;
		}
	}

	public class StatusEffect : Entity {
		public override System.Type clientType => typeof(StatusEffect);
		public override System.Type serverType => typeof(StatusEffect);


		#region getdata

		protected override void ConstructEntity(EntityData data) {
			base.ConstructEntity(data);
			this.data = (StatusEffectData)data;
		}

		new public StatusEffectData data {
			get;
			private set;
		}
		#endregion

		public float time;
		public float totalTime;

		public static StatusEffect Spawn(StatusEffectData d, float t) {
			var e = new StatusEffect();
			e.ConstructEntity(d);
			e.time = e.totalTime = t;
			return e;
		}

		public void Tick(float dt) {
			time -= dt;
		}
	}

}
