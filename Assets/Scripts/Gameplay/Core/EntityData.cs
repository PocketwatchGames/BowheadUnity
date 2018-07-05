
using UnityEngine;

namespace Bowhead {
	using Entity = Actors.Entity;
	using Team = Actors.Team;

    public abstract class EntityData : StaticVersionedAsset {
		public static EntityData Get(string name) {
			return DataManager.GetData<EntityData>(name);
		}
	}

	public abstract class EntityData<T> : EntityData where T : EntityData<T> {
		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}

	public abstract class SpawnableEntityData : EntityData {
		public abstract System.Type spawnClass { get; }

		public T SpawnEntity<T>(World world, Vector3 pos, Actor instigator, Actor owner, Team team) where T: Entity {
			var entity = (T)world.Spawn(spawnClass, null, default(SpawnParameters));
			entity.Spawn(this, pos, instigator, owner, team);
			return entity;
		}
	}

	public abstract class SpawnableEntityData<T> : SpawnableEntityData where T : SpawnableEntityData<T> {
		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}
