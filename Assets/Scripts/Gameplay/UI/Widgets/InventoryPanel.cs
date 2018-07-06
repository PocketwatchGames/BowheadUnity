using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class InventoryPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        [SerializeField]
        InventoryContainer _inventoryContainerPrefab;
        [SerializeField]
        InventorySlot _inventorySlotPrefab;
		[SerializeField]
		Transform _inventoryItems;

        public int slotMargin = 8;
        private Vector3 slotSize;

        public int inventorySelected = 0;

		private float _dpadXAxis = 0;
		private float _dpadYAxis = 0;
		private bool _rearranging;

		private InventoryContainer _mainContainer;
        private List<InventoryContainer> _packContainers = new List<InventoryContainer>();
        private InventorySlot[] _slots = new InventorySlot[Player.MaxInventorySize];

		[SerializeField] MonoBehaviour _buttonHints;
		[SerializeField] ButtonHint _buttonHintUp;
		[SerializeField] ButtonHint _buttonHintDown;
		[SerializeField] ButtonHint _buttonHintLeftRight;

        public void Init(Player player) {
            _player = player;
            var r = _inventorySlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

            _player.OnInventoryChange += OnInventoryChange;

            _mainContainer = Instantiate(_inventoryContainerPrefab, _inventoryItems, false);
            Rebuild();

			_buttonHintUp.SetButton("^");
			_buttonHintUp.SetHint("Drop/Rearrange");
			_buttonHintDown.SetButton("v");
			_buttonHintDown.SetHint("Use");
			_buttonHintLeftRight.SetButton("< >");
			_buttonHintLeftRight.SetHint("Select");
        }

        private void OnInventoryChange() {
            Rebuild();

            while (_player.GetInventorySlot(inventorySelected) == null) {
                inventorySelected--;
                if (inventorySelected <= 0) {
                    inventorySelected = 0;
                    break;
                }
            }
			OnInventorySelected();

		}

		private void Rebuild() {

			foreach (var s in _slots) {
				if (s != null) {
					GameObject.Destroy(s.gameObject);
				}
			}
			foreach (var p in _packContainers) {
				GameObject.Destroy(p.gameObject);
			}
			_packContainers.Clear();
			System.Array.Clear(_slots, 0, _slots.Length);


			float x = slotMargin;
            for (int slot = 0; slot <= (int)Player.InventorySlot.PACK-1; slot++) {
                var s = Instantiate(_inventorySlotPrefab, _mainContainer.transform, false);
                s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, 0);
                s.Init((Player.InventorySlot)slot);
                s.SetItem(_player.GetInventorySlot(slot));
                _slots[slot] = s;
                x += slotSize.x + slotMargin;
            }
            x += slotMargin;
            _mainContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(x, 54);

			_slots[1].SetButton("LT");
			_slots[2].SetButton("RT");
			_slots[3].SetButton("LB");
			_slots[4].SetButton("RB");

			int packSlotsRemaining = 0;
            InventoryContainer curPackContainer = null;
            for (int slot = (int)Player.InventorySlot.PACK; slot < Player.MaxInventorySize; slot++) {
                var item = _player.GetInventorySlot(slot);
                if (packSlotsRemaining == 0) {
                    var p = item as Pack;
                    if (p == null) {
                        break;
                    }
                    packSlotsRemaining = p.data.slots + 1;
                    curPackContainer = Instantiate(_inventoryContainerPrefab, _inventoryItems, false);
                    curPackContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, 0);
                    _packContainers.Add(curPackContainer);
                    x = slotMargin;
                }

                var s = Instantiate(_inventorySlotPrefab, curPackContainer.transform, false);
                s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, 0);
                s.Init((Player.InventorySlot)slot);
                s.SetItem(_player.GetInventorySlot(slot));
                _slots[slot] = s;
                x += slotSize.x + slotMargin;
                packSlotsRemaining--;

                if (packSlotsRemaining == 0) {
                    curPackContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(x, 54);
                }
            }

            SelectInventory(inventorySelected);


        }

        private void Update() {

			bool selectLeft = false;
			bool selectRight = false;
			//selectLeft = Input.GetButtonDown("SelectLeft");
			//selectRight = Input.GetButtonDown("SelectRight");

			float dpa = Input.GetAxis("DPadX");
			if (Utils.SignOrZero(dpa) != Utils.SignOrZero(_dpadXAxis)) {
				_dpadXAxis = dpa;
				selectLeft = _dpadXAxis < 0;
				selectRight = _dpadXAxis > 0;
			}

			if (selectLeft) {
				SelectPreviousInventory();
			}
			else if (selectRight) {
				SelectNextInventory();
			}

			bool use = false;
			bool drop = false;
			//selectLeft = Input.GetButtonDown("SelectLeft");
			//selectRight = Input.GetButtonDown("SelectRight");

			float dpy = Input.GetAxis("DPadY");
			if (Utils.SignOrZero(dpy) != Utils.SignOrZero(_dpadYAxis)) {
				_dpadYAxis = dpy;
				drop = _dpadYAxis > 0;
				use = _dpadYAxis < 0;
			}

			if (drop) {
				var item = _player.GetInventorySlot(inventorySelected);
				if (item != null) {
					_player.Drop(item);
					Rebuild();
				}
			}
			if (use) {
				var item = _player.GetInventorySlot(inventorySelected);
				if (item != null) {
					_player.Use(item);
					Rebuild();
				}
			}
		}


		void SelectPreviousInventory() {
            if (_rearranging) {
                RearrangeLeft();
            }
            else {
                int s = inventorySelected;
                for (int i = 0; i < Player.MaxInventorySize; i++) {
                    s--;
                    if (s < 0) {
                        s = Player.MaxInventorySize - 1;
                    }
                    if (_player.GetInventorySlot(s) != null) {
                        break;
                    }
                }
                SelectInventory(s);
            }
        }

        void SelectNextInventory() {
            if (_rearranging) {
                RearrangeRight();
            }
            else {
                int s = inventorySelected;
                for (int i = 0; i < Player.MaxInventorySize; i++) {
                    s++;
                    if (s >= Player.MaxInventorySize) {
                        s = 0;
                    }
                    if (_player.GetInventorySlot(s) != null) {
                        break;
                    }
                }
                SelectInventory(s);
            }
        }

        void SelectInventory(int i) {
            if (_slots[i] != null) {
                _slots[i].Deselect();
            }
            inventorySelected = i;
            if (_slots[i] != null) {
                _slots[i].Select();
            }
			OnInventorySelected();

		}

		void OnInventorySelected() {

		}

		private void RearrangeLeft() {
            var curItem = _player.GetInventorySlot(inventorySelected);
            if (curItem != null) {
                Pack pack;
                if ((pack = curItem as Pack) != null) {
                    int newSlot = inventorySelected - 1;
                    while (newSlot >= 0 && !(_player.GetInventorySlot(newSlot) is Pack)) {
                        newSlot--;
                    }
                    if (newSlot >= 0) {
                        List<Item> newInventory = new List<Item>();
                        for (int i = 0; i < newSlot; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        for (int i = 0; i < pack.data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i + inventorySelected));
                        }
                        for (int i = newSlot; i < Player.MaxInventorySize; i++) {
                            if (i < inventorySelected || i > inventorySelected + pack.data.slots) {
                                newInventory.Add(_player.GetInventorySlot(i));
                            }
                        }
                        int index = 0;
                        foreach (var i in newInventory) {
                            _player.SetInventorySlot(index++, i);
                        }
                        inventorySelected = newSlot;
                    }
                }
                else {
                    int newSlot = inventorySelected - 1;
                    for (; newSlot > (int)Player.InventorySlot.PACK; newSlot--) {
                        var itemInNewSlot = _player.GetInventorySlot(newSlot);
                        Pack p2;
                        if (itemInNewSlot is Pack) {
                            continue;
                        }
                        _player.SetInventorySlot(inventorySelected, itemInNewSlot);
                        _player.SetInventorySlot(newSlot, curItem);
                        inventorySelected = newSlot;
                        return;

                    }
                }
            }

        }

        private void RearrangeRight() {
            var curItem = _player.GetInventorySlot(inventorySelected);
            if (curItem != null) {
                Pack pack;
                if ((pack = curItem as Pack) != null) {
                    int newSlot = inventorySelected + 1;
                    while (newSlot < Player.MaxInventorySize && !(_player.GetInventorySlot(newSlot)is Pack)) {
                        newSlot++;
                    }
                    if (newSlot < Player.MaxInventorySize) {
                        List<Item> newInventory = new List<Item>();
                        for (int i = 0; i < inventorySelected; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        Pack p2 = _player.GetInventorySlot(newSlot) as Pack;
                        for (int i = newSlot; i < newSlot + p2.data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        for (int i = 0; i < pack.data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i + inventorySelected));
                        }
                        for (int i = newSlot + p2.data.slots + 1; i < Player.MaxInventorySize; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        int index = 0;
                        foreach (var i in newInventory) {
                            _player.SetInventorySlot(index++, i);
                        }
                        inventorySelected = newSlot;
                    }
                }
                else {
                    int lastPackSlot;
                    int curPackSlotsRemaining = 0;
                    for (lastPackSlot = (int)Player.InventorySlot.PACK; lastPackSlot < Player.MaxInventorySize; lastPackSlot++) {
                        var item = _player.GetInventorySlot(lastPackSlot);

                        Pack p;
                        if ((p = item as Pack) != null) {
                            curPackSlotsRemaining = p.data.slots;
                            continue;
                        }
                        if (curPackSlotsRemaining == 0) {
                            if (inventorySelected < (int)Player.InventorySlot.PACK) {
                                return;
                            }
                            lastPackSlot--;
                            break;
                        }
                        curPackSlotsRemaining--;
                        if (item == null && lastPackSlot > inventorySelected) {
                            break;
                        }
                    }
                    int oldSlot = inventorySelected;
                    int newSlot = oldSlot + 1;
                    if (oldSlot < (int)Player.InventorySlot.PACK) {
                        // if we are moving an equipped item to the pack, bump everything right
                        int emptySlot = (int)Player.InventorySlot.PACK + 1;
                        for (; emptySlot < lastPackSlot; emptySlot++) {
                            if (_player.GetInventorySlot(emptySlot) == null) {
                                break;
                            }
                        }
                        int lastEmptySlot = emptySlot;
                        for (int curSlot = emptySlot - 1; curSlot > (int)Player.InventorySlot.PACK; curSlot--) {
                            if (_player.GetInventorySlot(curSlot) is Pack) {
                                continue;
                            }
                            _player.SetInventorySlot(lastEmptySlot, _player.GetInventorySlot(curSlot));
                            lastEmptySlot = curSlot;
                        }
                        inventorySelected = (int)Player.InventorySlot.PACK + 1;
                        _player.SetInventorySlot((int)Player.InventorySlot.PACK + 1, curItem);
                        _player.SetInventorySlot(oldSlot, null);
                    }
                    else {
                        for (; newSlot <= lastPackSlot; newSlot++) {
                            var itemInNewSlot = _player.GetInventorySlot(newSlot);
                            if (itemInNewSlot is Pack) {
                                continue;
                            }
                            _player.SetInventorySlot(oldSlot, itemInNewSlot);
                            _player.SetInventorySlot(newSlot, curItem);
                            inventorySelected = newSlot;
                            return;

                        }
                    }

                }
            }

        }
    }
}
