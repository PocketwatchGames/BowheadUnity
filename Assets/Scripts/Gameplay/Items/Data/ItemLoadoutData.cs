using UnityEngine;

namespace Bowhead {
	[CreateAssetMenu(menuName = "ItemData/Loadout")]
	public sealed class ItemLoadoutData : ScriptableObject {
		public LootData[] loot;
		public ItemData[] inventory;
	}
}