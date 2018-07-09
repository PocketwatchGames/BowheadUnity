using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemInfoPanel : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Text _name;
	[SerializeField]
	UnityEngine.UI.Text _description;

	Bowhead.Item _item;

	public void SetItem(Bowhead.Item item) {
		_item = item;
		if (_item != null) {
			_name.text = item.data.name;
			_description.text = item.data.description;
		}
	}
}
