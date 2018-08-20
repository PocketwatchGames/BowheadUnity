using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/WorldItem")]
    public class WorldItemData : InteractableData<WorldItemData>, ISpawnPointSupport {
		[ClassDropdown(typeof(WorldItem)), SerializeField]
		string _itemClass;

		public GameObject_WRef mapMarker;
		public Client.UI.EMapMarkerStyle mapMarkerStyle;
		public int range;

		public Type itemClass { get; private set; }

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_itemClass)) {
				itemClass = null;
			} else {
				itemClass = Utils.GetType(_itemClass);
				if (itemClass == null) {
					throw new Exception("Unable to find type for " + _itemClass);
				}
			}
		}

		public T Spawn<T>(World world, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) where T: WorldItem {
			var item = (T)world.Spawn(itemClass, null, default(SpawnParameters));
			Spawn(item, pos, yaw, instigator, owner, team);
			return item;
		}

		public Actor Spawn(World world, Vector3 pos, float yaw, Team team) {
			return Spawn<WorldItem>(world, pos, yaw, null, null, team);
		}

		protected virtual void Spawn(WorldItem item, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			item.Spawn(this, pos, yaw, instigator, owner, team);
		}
	}

	// subclass support
	public abstract class WorldItemData<T> : WorldItemData where T : WorldItemData<T> {
		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}

