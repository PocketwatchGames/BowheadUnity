using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

	public class Horse : Critter {

		public override System.Type serverType => typeof(Horse);
		public override System.Type clientType => typeof(Horse);

		// horses don't panic.
		protected override void UpdatePanic(float dt) { }
		protected override void OnAudioEvent(Pawn origin, float loudness) {}
		protected override void OnHit(Pawn attacker) {}
	}
}
