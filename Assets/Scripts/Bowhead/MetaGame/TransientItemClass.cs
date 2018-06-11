// Copyright (c) 2018 Pocketwatch Games LLC.

namespace Bowhead.MetaGame {
	public abstract class TransientItemClass : DropItemClass {
		public const int TRANSIENT_ITEM_ID = 0;

		public sealed override int GetiLvlID(int ilvl) {
			return TRANSIENT_ITEM_ID;
		}
	}

}