using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class InventorySlot : MonoBehaviour {

		[SerializeField]
		private ButtonImage _buttonHint;
		[SerializeField]
		private Image _useTimer;

        private Item _item;
        private Button _button;
        private Image _background;
		private Player _player;

		public Sprite[] _slotBackgrounds;


		public void Init(Player.InventorySlot slot, Player p) {
			_player = p;
			_button = transform.GetAnyChildComponent<Button>("Button");
			_background = transform.GetAnyChildComponent<Image>("Background");
			_background.sprite = _slotBackgrounds[Mathf.Min((int)slot, _slotBackgrounds.Length - 1)];
			_button.gameObject.SetActive(false);
			_buttonHint.gameObject.SetActive(false);
			SetTimer(0);

			if (slot == Player.InventorySlot.PACK) {
				_button.gameObject.SetActive(true);
				_button.GetComponentInChildren<Text>().text = "Pack";
			}
		}

		public void SetButton(string b) {
			_buttonHint.gameObject.SetActive(b != null);
			if (b != null) {
				_buttonHint.SetButton(b);
			}
		}

		public void SetTimer(float t) {
			if (t == 0) {
				_useTimer.gameObject.SetActive(false);
			}
			else {
				_useTimer.gameObject.SetActive(true);
				_useTimer.rectTransform.localScale = new Vector3(1, t, 1);
			}
		}



		public void SetItem(Item i) {
			_item = i;
			if (i == null) {
				_button.gameObject.SetActive(false);
			} else {
				_button.gameObject.SetActive(true);

				string name;
				Loot loot;
				if ((loot = i as Loot) != null && loot.data.stackSize > 1) {
					_button.GetComponentInChildren<Text>().text = i.data.displayName + " x" + loot.count;
				} else {
					_button.GetComponentInChildren<Text>().text = i.data.displayName;
				}

			}
		}


        private void OnDestroy() {
            gameObject.DestroyAllChildren();
        }
    }
}