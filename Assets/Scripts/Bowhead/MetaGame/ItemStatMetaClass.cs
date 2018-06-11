// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.MetaGame {
	public sealed class ItemStatMetaClass : ObjectMetaClass<ItemStatMetaClass> {
		[SerializeField]
		string _name;
		[SerializeField]
		Color _color;

		public Color color {
			get {
				return _color;
			}
		}

		public string localizedName {
			get {
				if (!string.IsNullOrEmpty(_name)) {
					return Utils.GetLocalizedText(_name);
				}
				if (parent != null) {
					return parent.localizedName;
				}
				return "<missing item metaclass name>";
			}
		}
	}
}
