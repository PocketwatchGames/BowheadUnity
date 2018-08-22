using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class EquipSlot : MonoBehaviour {

		[SerializeField]
		private ButtonImage _buttonHint;
		[SerializeField]
		private Image _useTimer;
		[SerializeField]
		private Text _altSlot;
		[SerializeField]
		private Text _text;
		[SerializeField]
		private Image _background;

		private Item _item, _alt;
		private Player _player;

		public Sprite[] _slotBackgrounds;


		public void Init(Player p, int slotBackground) {
			_player = p;
			_background.sprite = _slotBackgrounds[Mathf.Min(slotBackground, _slotBackgrounds.Length - 1)];
			_text.gameObject.SetActive(false);
			_buttonHint.gameObject.SetActive(false);
			SetTimer(0);
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
				_useTimer.rectTransform.localScale = new Vector3(1, Mathf.Clamp01(t), 1);
			}
		}

		private void Update() {
			if (_item != null) {
				SetTimer(1.0f - (_item as Weapon).stamina);
				_useTimer.color = (_item as Weapon).stunned ? Color.red * 0.8f : Color.black * 0.8f;
			}
		}

		public void SetItem(Item i, Item alt) {
			_item = i;
			_alt = alt;

			if (_item == null) {
				_text.gameObject.SetActive(false);
			} else {
				_text.gameObject.SetActive(true);
				_text.text = _item.data.displayName;
			}

			if (_alt == null) {
				_altSlot.text = "";
			} else {
				_altSlot.text = _alt.data.displayName;
			}


		}


		private void OnDestroy() {
            gameObject.DestroyAllChildren();
        }
    }
}