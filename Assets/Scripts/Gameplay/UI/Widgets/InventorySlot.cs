using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class InventorySlot : MonoBehaviour {

        private Item _item;
        private Button _button;
        private Image _background;
        public Sprite[] _slotBackgrounds;

        public void Init(Player.InventorySlot slot) {
            _button = transform.GetAnyChildComponent<Button>("Button");
            _background = transform.GetAnyChildComponent<Image>("Background");
            _background.sprite = _slotBackgrounds[Mathf.Min((int)slot, _slotBackgrounds.Length-1)];            
        }

        public void SetItem(Item i) {
            _item = i;
            if (i == null) {
                _button.gameObject.SetActive(false);
            }
            else {
                _button.gameObject.SetActive(true);

				string name;
				Loot loot;
				if ((loot = i as Loot) != null && loot.data.stackSize > 1) {
					_button.GetComponentInChildren<Text>().text = i.data.name + " x" + loot.count;
				}
				else {
					_button.GetComponentInChildren<Text>().text = i.data.name;
				}

			}
        }

        public void Deselect() {
        }
        public void Select() {
            _button.Select();
        }

        private void OnDestroy() {
            gameObject.DestroyAllChildren();
        }
    }
}