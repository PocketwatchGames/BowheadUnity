using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
	public class InventoryHUD : MonoBehaviour {

		public InventoryPanel equipPanel;
		public InventoryPanel packPanel;
		public InventoryPanel storePanel;
		public GameObject storeGroup;
		public UnityEngine.UI.Text storeName;
		public ItemInfoPanel itemInfoPanel;
		public UnityEngine.UI.Text weightClass;
		public UnityEngine.UI.Text money;
		private int _playerIndex = 1;
		private float _moveX, _moveY;
		public int gridWidth = 4;

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

		// Update is called once per frame
		void Update() {
			int moveX = (int)Utils.SignOrZero(Input.GetAxis("MoveHorizontal" + _playerIndex));
			int moveY = (int)-Utils.SignOrZero(Input.GetAxis("MoveVertical" + _playerIndex));

			if ((_moveX != moveX && moveX != 0) || (_moveY != moveY && moveY != 0)) {
				if (_moveX != moveX) {
					_selectedX += moveX;
					if (_selectedX < 0) {
						_selectedX = gridWidth-1;
						if (_selectedX + _selectedY * gridWidth >= GetPanelSlotCount(_activePanel)) {
							_selectedX = GetPanelSlotCount(_activePanel) % gridWidth - 1;
						}
					} else if (_selectedX >= gridWidth) {
						_selectedX = 0;
					}
				}
				if (_moveY != moveY) {
					int maxPanel = (_merchant != null) ? 2 : 1;
					_selectedY += moveY;
					if (_selectedY < 0) {
						_activePanel--;
						if (_activePanel < 0) {
							_activePanel = maxPanel;
							_selectedY = 0;
						}
						_selectedY = ((GetPanelSlotCount(_activePanel) - 1) / gridWidth);
						if (_selectedX + _selectedY * gridWidth >= GetPanelSlotCount(_activePanel)) {
							_selectedX = GetPanelSlotCount(_activePanel) % gridWidth - 1;
						}
					} else if (_selectedX + _selectedY * gridWidth >= GetPanelSlotCount(_activePanel)) {
						_activePanel++;
						_selectedY = 0;
						if (_activePanel > maxPanel) {
							_activePanel = 0;
						}
						if (_selectedX + _selectedY * gridWidth >= GetPanelSlotCount(_activePanel)) {
							_selectedX = GetPanelSlotCount(_activePanel) % gridWidth - 1;
						}
					}
				}
				Select(_activePanel, _selectedX, _selectedY);
			}
			_moveX = moveX;
			_moveY = moveY;


			if (Input.GetButtonDown("A1")) {
				Use(_activePanel);
			}
			if (Input.GetButtonDown("Y1")) {
				Drop(_activePanel);
			}
			if (Input.GetButtonDown("X1")) {
				_player.SwapWeapons();
			}

		}

		private int GetPanelSlotCount(int p) {
			return GetPanel(p).transform.childCount;
		}

		public void SetMerchant(Pawn merchant) {
			_merchant = merchant;
			storeGroup.SetActive(_merchant != null);
			if (_merchant != null) {
				storePanel.Init(_merchant, 0, _merchant.data.packSize);
			} else {
				if (_activePanel == 2) {
					_activePanel = 0;
					_selectedX = 0;
					_selectedY = 0;
				}
			}

			OnInventoryChange();
		}

		private void OnInventoryChange() {
			equipPanel.OnInventoryChange();
			packPanel.OnInventoryChange();
			storePanel.OnInventoryChange();
		}

		private Item GetSelectedItem(int panel, int x, int y) {
			return null;
		}

		private InventoryPanel GetPanel(int index) {
			if (index == 0) {
				return equipPanel;
			} else if (index == 1) {
				return packPanel;
			}
			return storePanel;
		}
		private void Select(int panel, int x, int y) {
			var p = GetPanel(panel);
			int i = x + y * gridWidth;
			if (p.transform.childCount <= i) {
				return;
			}

			_selectedSlot?.Deselect();

			_selectedSlot = p.transform.GetChild(i)?.GetComponent<InventorySlot>();
			_selectedSlot?.Select();

			if (_selectedSlot.item != null) {
				itemInfoPanel.SetItem(_selectedSlot.item);
			}
			itemInfoPanel.gameObject.SetActive(_selectedSlot.item != null);

		}


		private void Use(int panel) {
			Item item = _selectedSlot.item;
			if (panel == 2) {
				if (_player.money >= item.data.monetaryValue) {
					if (_player.PickUp(ItemData.Get(item.data.name).CreateItem()) ) {
						_player.SetMoney(_player.money - item.data.monetaryValue);
					}
				}
			} else {
				_player.Use(item);
			}
		}
		private void Drop(int panel) {
			if (panel < 2) {
				Item item = _selectedSlot.item;
				if (item != null) {
					if (_merchant != null) {
						_player.RemoveFromInventory(item);
						_player.SetMoney(_player.money + item.data.monetaryValue);
					} else {
						_player.Drop(item);
					}
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
