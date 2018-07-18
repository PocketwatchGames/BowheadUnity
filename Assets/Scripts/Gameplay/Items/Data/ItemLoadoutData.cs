using UnityEngine;

namespace Bowhead {
	[CreateAssetMenu(menuName = "ItemData/Loadout")]
	public sealed class ItemLoadoutData : ScriptableObject {
		public ItemData[] loot;
		public ItemData[] inventory;
	}
}