using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead;

public class ItemInfoPanel : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Text _name;
	[SerializeField]
	UnityEngine.UI.Text _value;
	[SerializeField]
	UnityEngine.UI.Text _description;

	Item _item;

	public void SetItem(Item item) {
		_item = item;
		if (_item != null) {
			_name.text = item.data.name;
			_description.text = item.data.description;
			_value.text = "Value: $" + item.data.monetaryValue;
			Weapon w;
			if ((w = item as Weapon) != null) {
				foreach (var t in w.data.traits) {
					_description.text += "; " + t.description;
				}
			}

		}
	}
}
