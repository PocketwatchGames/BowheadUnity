using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public abstract class ItemData : EntityData {

		public string description;
		public int monetaryValue;


		public Item CreateItem() {
			return _CreateItem();
		}

		protected abstract Item _CreateItem();

		new public static ItemData Get(string name) {
			return DataManager.GetData<ItemData>(name);
		}
	}

	public abstract class ItemData<T, D> : ItemData where T: Item<T, D>, new() where D: ItemData<T, D>, new() {
		new public T CreateItem() {
			T item = new T();
			item.Init(this);
			return item;
		}

		override protected Item _CreateItem() {
			return CreateItem();
		}
		
		new public static D Get(string name) {
			return (D)DataManager.GetData<D>(name);
		}
	}
}
