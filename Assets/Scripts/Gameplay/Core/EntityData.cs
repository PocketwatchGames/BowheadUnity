
namespace Bowhead {
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

}
