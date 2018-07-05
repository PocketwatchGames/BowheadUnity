using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
    [CreateAssetMenu(menuName = "ItemData/Money")]
    public sealed class MoneyData : ItemData<Money, MoneyData> {
		public int count;
	}
}