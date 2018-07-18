using System;
using UnityEngine;

namespace Bowhead.Actors {

	[CreateAssetMenu(menuName = "EntityData/Projectile")]
	public class ProjectileData : InteractableData<WorldItemData> {
		[SerializeField, ClassDropdown(typeof(Projectile))]
		string _projectileClass;

		public float lifetime;
		public float damage;
		public PawnData.DamageType damageType;

		public Type projectileClass { get; private set; }

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(_projectileClass)) {
				projectileClass = null;
			} else {
				projectileClass = Type.GetType(_projectileClass);
			}
		}

		public virtual T SpawnAndFireProjectile<T>(World world, Vector3 pos, Vector3 velocity, Actor instigator, Actor owner, Team team) where T: Projectile {
			var projectile = (T)world.Spawn(projectileClass, null, default(SpawnParameters));
			projectile.Spawn(this, pos, velocity, instigator, owner, team);
			return projectile;
		}
	}

}