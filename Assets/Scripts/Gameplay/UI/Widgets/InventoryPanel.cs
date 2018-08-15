using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventoryPanel : MonoBehaviour {

		public InventorySlot inventorySlotPrefab;
		public Transform equipSlots;
		public Transform packSlots;
		public Transform storeSlots;
		public ItemInfoPanel itemInfoPanelPrefab;
		public UnityEngine.UI.Text weightClass;
		public UnityEngine.UI.Text money;
		private ItemInfoPanel itemInfoPanel;
		private int _playerIndex = 1;
		private float _moveX, _moveY;
		public int columns = 5;
		public int rows = 4;
		public int merchantSlots = 5;

		private int selectSlot;
		private InventorySlot[] _slots;
		private Player _player;
		private Pawn _merchant;

		// Use this for initialization
		void Start() {

		}

		public void Init(Player p) {
			_player = p;

			itemInfoPanel = GameObject.Instantiate<ItemInfoPanel>(itemInfoPanelPrefab, transform);

			int totalSlots = _player.data.packSize + (int)Player.InventorySlot.PACK + merchantSlots;
			_slots = new InventorySlot[totalSlots];
			for (int i=0;i< totalSlots; i++) {
				Transform parent;
				if (i < (int)Player.InventorySlot.PACK) {
					parent = equipSlots;
				} else if (i < (int)Player.InventorySlot.PACK + _player.data.packSize) {
					parent = packSlots;
				} else {
					parent = storeSlots;
				}
				var s = GameObject.Instantiate<InventorySlot>(inventorySlotPrefab, parent);
				_slots[i] = s;
			}

			OnInventoryChange();
			OnMoneyChange();
			OnWeightClassChange();
			OnSlotSelected();
			SetMerchant(null);

			_player.OnInventoryChange += OnInventoryChange;
			_player.OnMoneyChange += OnMoneyChange;
			_player.OnWeightClassChange += OnWeightClassChange;
		}

		// Update is called once per frame
		void Update() {
			int moveX = (int)Utils.SignOrZero(Input.GetAxis("MoveHorizontal" + _playerIndex));
			int moveY = (int)-Utils.SignOrZero(Input.GetAxis("MoveVertical" + _playerIndex));

			int curRows = (_merchant != null) ? 4 : 3;

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
						selectPos.y = curRows - 1;
					} else if (selectPos.y >= curRows) {
						selectPos.y = 0;
					}
				}
				_slots[selectSlot].Deselect();
				selectSlot = selectPos.x + selectPos.y * columns;

				OnSlotSelected();
			}
			_moveX = moveX;
			_moveY = moveY;

			Item item;
			if (isMerchantSlot(selectSlot)) {
				item = _merchant.GetInventorySlot(selectSlot - ((int)Player.InventorySlot.PACK + _player.data.packSize));
			} else {
				item = _player.GetInventorySlot(selectSlot);
			}
			if (item != null) {
				if (Input.GetButtonDown("A1")) {
					if (isMerchantSlot(selectSlot)) {
						if (_player.money >= item.data.monetaryValue) {
							_player.PickUp(ItemData.Get(item.data.name).CreateItem());
							_player.SetMoney(_player.money - item.data.monetaryValue);
						}
					} else {
						_player.Use(item);
					}
				}
				if (Input.GetButtonDown("X1")) {
					if (_merchant != null) {
						_player.RemoveFromInventory(item);
						_player.SetMoney(_player.money + item.data.monetaryValue);
					} else {
						_player.Drop(item);
					}
				}
			}
		}
		private bool isMerchantSlot(int s) {
			return s >= (int)Player.InventorySlot.PACK + _player.data.packSize;
		}

		public void SetMerchant(Pawn merchant) {
			_merchant = merchant;
			storeSlots.gameObject.SetActive(_merchant != null);
			OnInventoryChange();
		}

		private void OnSlotSelected() {
			_slots[selectSlot].Select();
			var selectedItem = _player.GetInventorySlot(selectSlot);
			if (selectedItem != null) {
				itemInfoPanel.gameObject.SetActive(true);
				itemInfoPanel.SetItem(selectedItem);
			} else {
				itemInfoPanel.gameObject.SetActive(false);
			}
		}

		private void OnInventoryChange() {
			for (int i=0;i<_slots.Length;i++) {
				if (isMerchantSlot(i)) {
					if (_merchant != null) {
						_slots[i].SetItem(_merchant.GetInventorySlot(i- ((int)Player.InventorySlot.PACK + _player.data.packSize)));
					} else {
						_slots[i].SetItem(null);
					}
				} else {
					_slots[i].SetItem(_player.GetInventorySlot(i));
				}
			}
		}

		private void OnMoneyChange() {
			money.text = "OIL: " + _player.money.ToString();
		}

		private void OnWeightClassChange() {
			weightClass.text = _player.weight.ToString();
		}
	}
}
