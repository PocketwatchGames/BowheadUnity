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
		public override void ServerSpawn(Vector3 pos, EntityData data) {
			base.ServerSpawn(pos, data);
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
		public override void ServerSpawn(Vector3 pos, EntityData data) {
			base.ServerSpawn(pos, data);
			this.data = (D)data;
		}

		new public D data {
			get;
			private set;
		}
	}
}
