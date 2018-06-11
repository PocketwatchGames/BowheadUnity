// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.MetaGame {
	public sealed class ItemStat : ScriptableObject {
		[SerializeField]
		float _value;
		[SerializeField]
		ItemStatClass _itemStatClass;

		public float value {
			get {
				return _value;
			}
		}

		public ItemStatClass itemStatClass {
			get {
				return _itemStatClass;
			}
		}

		public float GetScaledValue(int ilvl) {
			return _value * _itemStatClass.GetLevelScaling(ilvl);
		}
	}
}