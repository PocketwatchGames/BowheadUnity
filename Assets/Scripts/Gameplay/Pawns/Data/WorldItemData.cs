using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/WorldItem")]
    public class WorldItemData : InteractableData<WorldItemData>, ISpawnPointSupport {
		[ClassDropdown(typeof(WorldItem)), SerializeField]
		string _itemClass;

		public Type itemClass { get; private set; }

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_itemClass)) {
				itemClass = null;
			} else {
				itemClass = Type.GetType(_itemClass);
			}
		}

		public T Spawn<T>(World world, Vector3 pos, Actor instigator, Actor owner, Team team) where T: WorldItem {
			var item = (T)world.Spawn(itemClass, null, default(SpawnParameters));
			item.Spawn(this, pos, instigator, owner, team);
			return item;
		}

		public Actor Spawn(World world, Vector3 pos, Team team) {
			return Spawn<WorldItem>(world, pos, null, null, team);
		}
	}

}

