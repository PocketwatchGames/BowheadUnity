using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "EntityData/Critter")]
	public class CritterData : PawnData<CritterData>, ISpawnPointSupport {
		[Header("Critter"), ClassDropdown(typeof(Critter)), SerializeField]
		string _critterClass;

		public GameObject_WRef prefab;
		public float silhouetteDistanceThreshold;
		public bool canMount;
		public ItemLoadoutData defaultLoadout;

		[Header("Aggro")]
		public float waryLimit;
		public float investigateLimit;
		public float dayVisionDistance;
		public float nightVisionDistance;
		public float visionAngleExponent;
		public float visionDistanceExponent;
		public float visionMotionWeight;
		public float visionAngleRange;
		public float visionAngleRangeUp;
		public float visionAngleRangeDown;
		public float hearingDistance;
		public float hearingDistanceExponent;
		public float waryCooldownTime;
		public float panicCooldownTime;
		public float waryIncreaseAtMaxAwareness;
		public float waryIncreaseAtMaxAwarenessWhilePanicked;
		public List<Critter.ECritterBehaviorType> behaviors = new List<Critter.ECritterBehaviorType>();

		public Type critterClass { get; private set; }

		float _silhouetteDistanceSq;

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_critterClass)) {
				critterClass = null;
			} else {
				critterClass = Utils.GetType(_critterClass);
				if (critterClass == null) {
					throw new Exception("Unable to find type for " + _critterClass);
				}
			}

			_silhouetteDistanceSq = silhouetteDistanceThreshold*silhouetteDistanceThreshold;
		}

		public T Spawn<T>(World world, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) where T: Critter {

			var critter = (T)world.Spawn(critterClass, null, default(SpawnParameters));
			critter.Spawn(this, 0, pos, yaw, instigator, owner, team);

			if (defaultLoadout != null) {
				var loot = defaultLoadout.loot;
				if ((loot != null) && (loot.Length > 0)) {
					for (int i = 0; i<loot.Length; ++i) {
						var item = loot[i].CreateItem();
						critter.loot[i] = item;
					}
				}

				var inventory = defaultLoadout.inventory;
				if ((inventory != null) && (inventory.Length > 0)) {
					for (int i = 0; i<inventory.Length; ++i) {
						var item = inventory[i].CreateItem();
						critter.SetInventorySlot(i, item);
					}
				}
			}

			return critter;

		}

		public Actor Spawn(World world, Vector3 pos, float yaw, Team team) {
			return Spawn<Critter>(world, pos, yaw, null, null, team);
		}

		public float silhouetteDistanceThreadholdSq => _silhouetteDistanceSq;
	};
}