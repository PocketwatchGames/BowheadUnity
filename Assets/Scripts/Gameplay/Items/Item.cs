using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead {
    public abstract class Item {
        public ItemData data {
			get;
			private set;
		}

		public virtual void Init(ItemData data) {
			this.data = data;
		}

        virtual public void Tick(float dt, Actors.Pawn actor) {}
        public virtual void OnSlotChange(int newSlot, Actors.Pawn owner) {}
    }

	public abstract class Item<T, D> : Item where T : Item<T, D> where D : ItemData {
		
		public override void Init(ItemData data) {
			base.Init(data);
			this.data = (D)data;
		}

		new public D data {
			get;
			private set;
		}
	}
}