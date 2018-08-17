using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
    public class EquipPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        [SerializeField]
		Transform _mainContainer;
		[SerializeField]
		EquipSlot _equipSlotPrefab;
		[SerializeField]
		StatusEffectHUD _statusEffectPrefab;


		public int slotMargin = 8;
        private Vector3 slotSize;

        private EquipSlot[] _slots = new EquipSlot[Player.MaxInventorySize];

		[SerializeField] GameObject _statusEffectContainer;


		public void Init(Player player) {
            _player = player;
            var r = _equipSlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

			_player.OnInventoryChange += OnInventoryChange;
			GameManager.instance.clientWorld.StatusEffectAddedEvent += OnStatusEffectAdded;
			_player.OnInputMethodChange += OnInputMethodChange;
			_player.OnSwapWeapon += OnSwapWeapon;

			OnInventoryChange();
		}


		private void OnInventoryChange() {
            Rebuild();
		}

		private void OnDestroy() {
			_player.OnInventoryChange -= OnInventoryChange;
			_player.OnInputMethodChange -= OnInputMethodChange;
			_player.OnSwapWeapon -= OnSwapWeapon;
		}

		private void OnStatusEffectAdded(Pawn target, StatusEffect e) {
            if (target != _player) {
                return;
            }

			foreach (var s in GetComponentsInChildren<StatusEffectHUD>()) {
				if (s.statusEffectType == e.data) {
					s.Init(e);
					return;
				}
			}

			var hud = Instantiate<StatusEffectHUD>(_statusEffectPrefab, _statusEffectContainer.transform);
			hud.Init(e);
		}

		private void OnSwapWeapon() {
			SetSlot((_player.activeWeapon == 0) ? Player.InventorySlot.LEFT_HAND : Player.InventorySlot.CLOTHING, 0);
			SetSlot((_player.activeWeapon == 0) ? Player.InventorySlot.RIGHT_HAND : Player.InventorySlot.SPELL, 1);
		}

		private void SetSlot(Player.InventorySlot slot, int index) {
			var item = _player.GetInventorySlot((int)slot);
			_slots[index].SetItem(item);		
		}

		private void AddSlot(Player.InventorySlot slot, string button, ref int index) {
			var item = _player.GetInventorySlot((int)slot);
			if (item != null) {
				var s = Instantiate(_equipSlotPrefab, _mainContainer.transform, false);
				s.Init(slot, _player);
				s.SetItem(item);
				_slots[index] = s;
				_slots[index].SetButton(button);
				index++;
			}

		}

		private void OnInputMethodChange() 
		{
			Rebuild();
		}

		private void Rebuild() {

			foreach (var i in _slots) {
				if (i != null) {
					GameObject.Destroy(i.gameObject);
				}
			}
			System.Array.Clear(_slots, 0, _slots.Length);


			float x = slotMargin;
			int index = 0;

			AddSlot(Player.InventorySlot.LEFT_HAND, _player.GetButtonHint("LT"), ref index);
			AddSlot(Player.InventorySlot.RIGHT_HAND, _player.GetButtonHint("RT"), ref index);

			{
				var s = Instantiate(_equipSlotPrefab, _mainContainer.transform, false);
				s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, 0);
				s.Init(Player.InventorySlot.PACK, _player, "Pack");
				_slots[index] = s;
				_slots[index].SetButton(_player.GetButtonHint("Y"));
			}

            _mainContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(x, 54);
        }

		

    }
}
