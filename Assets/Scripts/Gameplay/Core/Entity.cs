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
		        
		protected virtual void ConstructEntity(EntityData data) {
			this.data = data;
        }
    }

	public abstract class Entity<T, D> : Entity where T : Entity<T, D> where D : EntityData {

		protected override void ConstructEntity(EntityData data) {
			base.ConstructEntity(data);
			this.data = (D)data;
		}

		new public D data {
			get;
			private set;
		}
	}
}