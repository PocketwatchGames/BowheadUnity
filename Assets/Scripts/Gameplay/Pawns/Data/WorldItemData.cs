using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/WorldItem")]
    public class WorldItemData : InteractableData<WorldItemData> {
		public override Type spawnClass => typeof(WorldItem);
	}

}

