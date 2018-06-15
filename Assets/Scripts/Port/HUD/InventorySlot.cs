using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Port {
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
                _button.GetComponentInChildren<Text>().text = i.Data.name;
            }
        }

        public void Deselect() {
        }
        public void Select() {
            _button.Select();
        }
    }
}