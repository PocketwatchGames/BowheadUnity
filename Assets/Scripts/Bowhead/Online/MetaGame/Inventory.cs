// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.ObjectModel;

namespace Bowhead.Online.MetaGame {
	public class InventoryItem {
		public InventoryItem(ulong iid, int id, Bowhead.MetaGame.InventoryItemClass itemClass, int ilvl, int count) {
			this.iid = iid;
			this.id = id;
			this.ilvl = ilvl;
			this.count = count;
			this.itemClass = itemClass;
		}

		public readonly ulong iid;
		public readonly int id;
		public readonly int ilvl;
		public readonly int count;
		public readonly Bowhead.MetaGame.InventoryItemClass itemClass;
	}

	public interface ImmutableInventory {
		ReadOnlyCollection<InventoryItem> items { get; }
	}
}
