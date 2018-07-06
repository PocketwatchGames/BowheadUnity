using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	public abstract class Interactable : Entity {
		// This field is a HACK and is null on clients
		public Server.BowheadGame gameMode {
			get;
			private set;
		}

		#region getdata
		protected override void ConstructEntity(EntityData data) {
			base.ConstructEntity(data);
			this.data = (InteractableData)data;
			gameMode = (Server.BowheadGame)((Server.ServerWorld)world).gameMode;
		}
			
		new public InteractableData data {
			get;
			private set;
		}
		#endregion
	}

	public abstract class Interactable<T, D> : Interactable where T : Interactable<T, D> where D : InteractableData {

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
