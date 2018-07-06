using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "EntityData/Projectile")]
	public class ProjectileData : InteractableData<WorldItemData> {
		public float lifetime;
		public float damage;
	}

}