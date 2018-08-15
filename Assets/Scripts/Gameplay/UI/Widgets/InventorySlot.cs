using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventorySlot : MonoBehaviour {

		private Item _item;
		private Image _background;
		private Player _player;

		public Button button;
		public Sprite[] _slotBackgrounds;


		public void Init(Player.InventorySlot slot, Player p) {
			_player = p;
			_background = transform.GetAnyChildComponent<Image>("Background");
			_background.sprite = _slotBackgrounds[Mathf.Min((int)slot, _slotBackgrounds.Length - 1)];
//			button.gameObject.SetActive(false);
		}


		public void SetItem(Item i) {
			_item = i;
			if (i == null) {
				button.GetComponentInChildren<Text>().text = "";
				//				button.gameObject.SetActive(false);
			} else {
//				button.gameObject.SetActive(true);

				string name;
				Loot loot;
				if ((loot = i as Loot) != null && loot.data.stackSize > 1) {
					button.GetComponentInChildren<Text>().text = i.data.displayName + " x" + loot.count;
				} else {
					button.GetComponentInChildren<Text>().text = i.data.displayName;
				}

			}
		}

		public void Deselect() {
		}
		public void Select() {
			button.Select();
		}

		private void OnDestroy() {
			gameObject.DestroyAllChildren();
		}
	}
}
