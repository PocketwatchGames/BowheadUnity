using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventoryPanel : MonoBehaviour {

		public InventorySlot inventorySlotPrefab;
		public Transform equipSlots;
		public Transform packSlots;
		public ItemInfoPanel itemInfoPanelPrefab;
		private ItemInfoPanel itemInfoPanel;
		private int _playerIndex = 1;
		private float _moveX, _moveY;
		public int columns = 5;
		public int rows = 3;

		private int selectSlot;
		private InventorySlot[] _slots;
		private Player _player;

		// Use this for initialization
		void Start() {

		}

		public void Init(Player p) {
			_player = p;

			itemInfoPanel = GameObject.Instantiate<ItemInfoPanel>(itemInfoPanelPrefab, transform);

			_slots = new InventorySlot[_player.data.packSize + (int)Player.InventorySlot.PACK-1];
			for (int i=0;i<_player.data.packSize + (int)Player.InventorySlot.PACK - 1;i++) {
				var parent = (i < (int)Player.InventorySlot.PACK) ? equipSlots : packSlots;
				var s = GameObject.Instantiate<InventorySlot>(inventorySlotPrefab, parent);
				_slots[i] = s;
			}
			OnInventoryChange();

			_player.OnInventoryChange += OnInventoryChange;
		}

		// Update is called once per frame
		void Update() {
			int moveX = (int)Utils.SignOrZero(Input.GetAxis("MoveHorizontal" + _playerIndex));
			int moveY = (int)-Utils.SignOrZero(Input.GetAxis("MoveVertical" + _playerIndex));

			if ((_moveX != moveX && moveX != 0) || (_moveY != moveY && moveY != 0)) {
				Vector2Int selectPos = new Vector2Int(selectSlot % columns, selectSlot / columns);
				if (_moveX != moveX) {
					selectPos.x += moveX;
					if (selectPos.x < 0) {
						selectPos.x = columns - 1;
					} else if (selectPos.x >= columns) {
						selectPos.x = 0;
					}
				}
				if (_moveY != moveY) {
					selectPos.y += moveY;
					if (selectPos.y < 0) {
						selectPos.y = rows - 1;
					} else if (selectPos.y >= rows) {
						selectPos.y = 0;
					}
				}
				_slots[selectSlot].Deselect();
				selectSlot = selectPos.x + selectPos.y * columns;
				_slots[selectSlot].Select();

				var selectedItem = _player.GetInventorySlot(selectSlot);
				if (selectedItem != null) {
					itemInfoPanel.gameObject.SetActive(true);
					itemInfoPanel.SetItem(selectedItem);
				} else {
					itemInfoPanel.gameObject.SetActive(false);
				}
			}
			_moveX = moveX;
			_moveY = moveY;

			if (Input.GetButtonDown("A1")) {
				_player.Use(_player.GetInventorySlot(selectSlot));
			}
			if (Input.GetButtonDown("X1")) {
				_player.Drop(_player.GetInventorySlot(selectSlot));
			}
		}

		private void OnInventoryChange() {
			for (int i=0;i<_slots.Length;i++) {
				_slots[i].SetItem(_player.GetInventorySlot(i));
			}
		}
	}
}
