using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenu(menuName = "ItemData/Armor")]
    public class ArmorData : ItemData<Armor, ArmorData> {
		public List<TraitData> traits = new List<TraitData>();

	}
}