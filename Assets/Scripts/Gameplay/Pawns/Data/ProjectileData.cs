using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "EntityData/Projectile")]
	public class ProjectileData : InteractableData<WorldItemData> {
		public override Type spawnClass => typeof(Projectile);

		public float lifetime;
		public float damage;

	}

}