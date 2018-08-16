using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventoryHUD : MonoBehaviour {

		public InventorySlot inventorySlotPrefab;
		public InventoryPanel equipPanel;
		public InventoryPanel packPanel;
		public InventoryPanel storePanel;
		public ItemInfoPanel itemInfoPanel;
		public UnityEngine.UI.Text weightClass;
		public UnityEngine.UI.Text money;
		private int _playerIndex = 1;
		private float _moveX, _moveY;

		private Player _player;
		private Pawn _merchant;
		private InventorySlot _selectedSlot;
		private int _activePanel;
		private int _selectedX, _selectedY;

		// Use this for initialization
		void Start() {

		}

		public void Init(Player p) {
			_player = p;

			equipPanel.Init(_player, 0, (int)Player.InventorySlot.PACK);
			packPanel.Init(_player, (int)Player.InventorySlot.PACK, _player.data.packSize);

			OnInventoryChange();
			OnMoneyChange();
			OnWeightClassChange();
			SetMerchant(null);

			_player.OnInventoryChange += OnInventoryChange;
			_player.OnMoneyChange += OnMoneyChange;
			_player.OnWeightClassChange += OnWeightClassChange;

			Select(0, 0, 0);
		}

		private void OnDestroy() {
			_player.OnInventoryChange -= OnInventoryChange;
			_player.OnMoneyChange -= OnMoneyChange;
			_player.OnWeightClassChange -= OnWeightClassChange;
		}

		private void Select(int panel, int x, int y) {
			_selectedSlot?.Deselect();
			_activePanel = panel;
//			_selectedSlot = _activePanel
			_selectedSlot.Select();
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
				_selectedSlot.Deselect();
				selectSlot = selectPos.x + selectPos.y * columns;

				OnSlotSelected();
			}
			_moveX = moveX;
			_moveY = moveY;


			if (Input.GetButtonDown("A1")) {
				Use(_activePanel, _selectedX, _selectedY);
			}
			if (Input.GetButtonDown("X1")) {
				Drop(_activePanel, _selectedX, _selectedY);
			}

		}



		private bool isMerchantSlot(int s) {
			return s >= (int)Player.InventorySlot.PACK + _player.data.packSize;
		}

		public void SetMerchant(Pawn merchant) {
			_merchant = merchant;
			storePanel.gameObject.SetActive(_merchant != null);
			if (_merchant != null) {
				storePanel.Init(_merchant, 0, _merchant.data.packSize);
			}
			OnInventoryChange();
		}

		private void OnInventoryChange() {

		}

		private void Highlight(InventorySlot s) {
			if (item != null) {
				itemInfoPanel.SetItem(item);
			}
			itemInfoPanel.gameObject.SetActive(item != null);
		}
		private void Use(int panel, int x, int y) {
			if (panel == 2) {

			}
			_player.Use(item);
		}
		private void Drop(InventorySlot s) {
			if (_merchant != null) {
				_player.RemoveFromInventory(item);
				_player.SetMoney(_player.money + item.data.monetaryValue);
			} else {
				_player.Drop(item);
			}
		}
		private void Buy(InventorySlot s) {
			if (_player.money >= item.data.monetaryValue) {
				_player.PickUp(ItemData.Get(item.data.name).CreateItem());
				_player.SetMoney(_player.money - item.data.monetaryValue);
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
