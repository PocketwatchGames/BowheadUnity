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
		public float waryLimit;
		public float investigateLimit;
		public float visionWeight;
		public float smellWeight;
		public float hearingWeight;
		public float dayVisionDistance;
		public float nightVisionDistance;
		public float visionAngleExponent;
		public float visionDistanceExponent;
		public float visionAngleRange;
		public float visionAngleRangeUp;
		public float visionAngleRangeDown;
		public float hearingDistance;
		public float hearingDistanceExponent;
		public float scentDistance;

		public float waryCooldownTime;
		public float panicCooldownTime;
		public float waryIncreaseAtMaxAwareness;
		public float waryIncreaseAtMaxAwarenessWhilePanicked;
		public ECritterBehaviorType panicBehavior;
		public ItemLoadoutData defaultLoadout;

		public Type critterClass { get; private set; }

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_critterClass)) {
				critterClass = null;
			} else {
				critterClass = Type.GetType(_critterClass);
			}
		}

		public T Spawn<T>(World world, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) where T: Critter {

			var critter = (T)world.Spawn(critterClass, null, default(SpawnParameters));
			critter.Spawn(this, pos, yaw, instigator, owner, team);

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

	};
}