using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenu(menuName = "ItemData/Clothing")]
    public class ClothingData : ItemData<Clothing, ClothingData> {

		public float maxHealthBonus;
		public float maxStaminaBonus;


	}
}