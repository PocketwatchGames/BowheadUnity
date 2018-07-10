using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	public class WorldItemMapReveal : WorldItem {

		public override System.Type clientType => typeof(WorldItemMapReveal);
		public override System.Type serverType => typeof(WorldItemMapReveal);

		public override void Spawn(EntityData d, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(d, pos, instigator, owner, team);
			map = new MapReveal() {
				position = new Vector2(pos.x, pos.z),
				radius = 100 // TODO: make this data
			};
		}
	}
}
