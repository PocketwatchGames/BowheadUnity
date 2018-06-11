// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.MetaGame {
	public sealed class ItemStatClass : ScriptableObject {
		[SerializeField]
		ItemStatMetaClass _metaClass;
		[SerializeField]
		XPCurve _lvlScale;

		public ItemStatMetaClass metaClass {
			get {
				return _metaClass;
			}
		}

		public float GetLevelScaling(int toLevel) {
			if (_lvlScale != null) {
				return GameManager.instance.staticData.xpTable.Eval(_lvlScale, toLevel);
			}
			return 1f;
		}
	}
}