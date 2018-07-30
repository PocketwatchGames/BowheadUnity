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
		UnityEngine.UI.Text _money;
		[SerializeField]
		UnityEngine.UI.Text _weight;
		[SerializeField]
		StatusEffectHUD _statusEffectPrefab;


		public int slotMargin = 8;
        private Vector3 slotSize;

        public int inventorySelected = 0;

		const float _useTime  = 0.5f;
		private float _dpadXAxis = 0;
		private float _dpadYAxis = 0;
		private bool _rearranging;
        private float _useTimer;

		private InventoryContainer _mainContainer;
        private List<InventoryContainer> _packContainers = new List<InventoryContainer>();
        private InventorySlot[] _slots = new InventorySlot[Player.MaxInventorySize];

		[SerializeField] MonoBehaviour _buttonHints;
		[SerializeField] ButtonHint _buttonHintUp;
		[SerializeField] ButtonHint _buttonHintDown;
		[SerializeField] ButtonHint _buttonHintLeftRight;

		[SerializeField] ItemInfoPanel _itemInfo;
		[SerializeField] GameObject _statusEffectContainer;


		public void Init(Player player) {
            _player = player;
            var r = _inventorySlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

			_player.OnMoneyChange += onMoneyChange;
			_player.OnInventoryChange += OnInventoryChange;
			GameManager.instance.clientWorld.StatusEffectAddedEvent += OnStatusEffectAdded;

            _mainContainer = Instantiate(_inventoryContainerPrefab, _inventoryItems, false);

			_buttonHintUp.SetButton("^");
			_buttonHintDown.SetButton("v");
			_buttonHintLeftRight.SetButton("< >");

			SetRearranging(false);

			OnInventoryChange();
		}

		private void onMoneyChange() {
			_money.text = _player.money.ToString();
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
			SelectInventory(inventorySelected);
			OnInventorySelected();

			_weight.text = _player.weight.ToString();
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
                    curPackContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, (_rearranging && slot == inventorySelected) ? 15 : 0);
                    _packContainers.Add(curPackContainer);
                    x = slotMargin;
                }

                var s = Instantiate(_inventorySlotPrefab, curPackContainer.transform, false);
                s.GetComponent<RectTransform>().anchoredPosition = new Vector2(x + slotSize.x / 2, (_rearranging && slot == inventorySelected) ? 15 : 0);
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

		private void SetRearranging(bool r) {
			_rearranging = r;
			if (_rearranging) {
				_buttonHintLeftRight.SetHint("Move Item");
				_buttonHintDown.SetHint("Cancel");

				if (_player.tradePartner != null) {
					_buttonHintUp.SetHint("Offer for Sale");
				}
				else {
					_buttonHintUp.SetHint("Drop");
				}
				_itemInfo.gameObject.SetActive(true);
				_itemInfo.SetItem(_player.GetInventorySlot(inventorySelected));
			}
			else {
				_buttonHintLeftRight.SetHint("Select");
				_buttonHintDown.SetHint("Use");
				if (_player.tradePartner != null) {
					_buttonHintUp.SetHint("Sell");
				}
				else {
					_buttonHintUp.SetHint("Info");
				}
				_itemInfo.gameObject.SetActive(false);
			}
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

			float dpy = Input.GetAxis("DPadY");

			bool usePressed = dpy < 0;
			bool useDown = usePressed && !(_dpadYAxis < 0);
			bool dropPressed = dpy > 0;
			bool dropReleased = !dropPressed && (_dpadYAxis > 0);
			_dpadYAxis = dpy;


			if (dropReleased) {
				var item = _player.GetInventorySlot(inventorySelected);
				if (item != null) {
					if (_rearranging) {
						_player.Drop(item);
						SetRearranging(false);
					}
					else {
						SetRearranging(true);
					}
					Rebuild();
				}
			}
			if (usePressed) {
				var item = _player.GetInventorySlot(inventorySelected);
				if (item != null) {
					if (_rearranging) {
                        if (usePressed)
                        {
                            SetRearranging(false);
                            Rebuild();
                        }
                    }
                    else {
						if (_useTimer > 0 || useDown) {
							_useTimer += Time.deltaTime;
							if (_useTimer > _useTime) {
								_player.Use(item);
								_useTimer = 0;
								Rebuild();
							}
						}
					}
				}
			}
			else {
				_useTimer = 0;
			}

			for (int i=0;i<_slots.Length;i++) {
				if (_slots[i] == null) {
					continue;
				}
				if (inventorySelected == i) {
					_slots[i].SetTimer(_useTimer / _useTime);
				}
				else {
					_slots[i].SetTimer(0);
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
						SelectInventory(newSlot);
                    }
                }
                else {
                    int newSlot = inventorySelected - 1;
                    for (; newSlot > (int)Player.InventorySlot.PACK; newSlot--) {
                        var itemInNewSlot = _player.GetInventorySlot(newSlot);
                        if (itemInNewSlot is Pack) {
                            continue;
                        }
                        _player.SetInventorySlot(inventorySelected, itemInNewSlot);
                        _player.SetInventorySlot(newSlot, curItem);
						SelectInventory(newSlot);
						Rebuild();
						return;

                    }
                }
            }
			Rebuild();

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
						SelectInventory(newSlot);
						Rebuild();
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
						SelectInventory((int)Player.InventorySlot.PACK + 1);
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
							SelectInventory(newSlot);
							Rebuild();
							return;

                        }
                    }
					
                }
				Rebuild();
			}

		}
    }
}
