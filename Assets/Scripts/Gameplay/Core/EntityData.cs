
using UnityEngine;

namespace Bowhead {
	using Entity = Actors.Entity;
	using Team = Actors.Team;

    public abstract class EntityData : StaticVersionedAssetWithSerializationCallback {
		
		public static EntityData Get(string name) {
			return DataManager.GetData<EntityData>(name);
		}

		protected override void InitVersion() {
			base.InitVersion();
		}
	}

	public abstract class EntityData<T> : EntityData where T : EntityData<T> {
		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}
