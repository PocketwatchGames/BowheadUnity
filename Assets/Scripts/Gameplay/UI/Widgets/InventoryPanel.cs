using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
    public class InventoryPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        [SerializeField]
        InventoryContainer _inventoryContainerPrefab;
		[SerializeField]
		InventorySlot _inventorySlotPrefab;
		[SerializeField]
		Transform _inventoryItems;
		[SerializeField]
		StatusEffectHUD _statusEffectPrefab;


		public int slotMargin = 8;
        private Vector3 slotSize;

		private InventoryContainer _mainContainer;
        private InventorySlot[] _slots = new InventorySlot[Player.MaxInventorySize];

		[SerializeField] GameObject _statusEffectContainer;


		public void Init(Player player) {
            _player = player;
            var r = _inventorySlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

			_player.OnInventoryChange += OnInventoryChange;
			GameManager.instance.clientWorld.StatusEffectAddedEvent += OnStatusEffectAdded;

            _mainContainer = Instantiate(_inventoryContainerPrefab, _inventoryItems, false);

			OnInventoryChange();
		}


		private void OnInventoryChange() {
            Rebuild();
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


		private void Rebuild() {

			foreach (var i in _slots) {
				if (i != null) {
					GameObject.Destroy(i.gameObject);
				}
			}
			System.Array.Clear(_slots, 0, _slots.Length);


			float x = slotMargin;
			int index = 0;
            for (int slot = 1; slot <= (int)Player.InventorySlot.PACK-1; slot++) {
				var item = _player.GetInventorySlot(slot);
				if (item != null) {
					var s = Instantiate(_inventorySlotPrefab, _mainContainer.transform, false);
					s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, 0);
					s.Init((Player.InventorySlot)slot, _player);
					s.SetItem(item);
					_slots[index] = s;
					x += slotSize.x + slotMargin;

					switch ((Player.InventorySlot)slot) {
						case Player.InventorySlot.LEFT_HAND:
							_slots[index].SetButton("LT"); break;
						case Player.InventorySlot.RIGHT_HAND:
							_slots[index].SetButton("RT"); break;
						case Player.InventorySlot.LEFT_RANGED:
							_slots[index].SetButton("LB"); break;
						case Player.InventorySlot.RIGHT_RANGED:
							_slots[index].SetButton("RB"); break;
					}
					index++;
				}
            }

			{
				var s = Instantiate(_inventorySlotPrefab, _mainContainer.transform, false);
				s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, 0);
				s.Init(Player.InventorySlot.PACK, _player);
				_slots[index] = s;
				x += slotSize.x + slotMargin;
				_slots[index].SetButton("Y");
			}

			x += slotMargin;
            _mainContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(x, 54);
        }


    }
}
