using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

	public abstract class Entity : Actor {

		[Replicated(Condition = EReplicateCondition.InitialOnly)]
        StaticAssetRef<EntityData> _data;

        public EntityData data {
			get;
			private set;
		}
		        
		public virtual void Spawn(EntityData data, Vector3 pos, Actor instigator, Actor owner, Team team) {
			this.data = data;
        }
    }

	public abstract class Entity<T, D> : Entity where T : Entity<T, D> where D : EntityData {

		public override void Spawn(EntityData data, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(data, pos, instigator, owner, team);
			this.data = (D)data;
		}

		new public D data {
			get;
			private set;
		}
	}
}