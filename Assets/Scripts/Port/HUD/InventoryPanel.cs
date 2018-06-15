using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class InventoryPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        [SerializeField]
        InventoryContainer _inventoryContainerPrefab;
        [SerializeField]
        InventorySlot _inventorySlotPrefab;

        public int slotMargin = 8;
        private Vector3 slotSize;

        public int inventorySelected = 0;
        public float dropTimer;
        public float dropTime = 0.5f;

        private InventoryContainer _mainContainer;
        private List<InventoryContainer> _packContainers = new List<InventoryContainer>();

        public void Init(Player player) {
            _player = player;
            var r = _inventorySlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

            _player.onInventoryChange += onInventoryChange;

            _mainContainer = Instantiate(_inventoryContainerPrefab, transform, false);
            Rebuild();
        }

        private void onInventoryChange() {
            _mainContainer.transform.DestroyAllChildren();
            foreach (var p in _packContainers) {
                GameObject.Destroy(p);
            }
            _packContainers.Clear();
            Rebuild();

            while (_player.GetInventorySlot(inventorySelected) == null) {
                inventorySelected--;
                if (inventorySelected <= 0) {
                    inventorySelected = 0;
                    break;
                }
            }
        }

        private void Rebuild() {
            for (int slot = 0; slot <= (int)Player.InventorySlot.RIGHT_HAND; slot++) {
                var s = Instantiate(_inventorySlotPrefab, _mainContainer.transform, false);
                s.GetComponent<RectTransform>().anchoredPosition = new Vector2(slot * (slotSize.x + slotMargin) + slotMargin + slotSize.x / 2, 0);
                s.Init((Player.InventorySlot)slot);
                s.SetItem(_player.GetInventorySlot(slot));
            }
        }

        private void Update() {
            var selectDir = Input.GetAxisRaw("SelectInventory");
            if (selectDir < 0) { 
                SelectPreviousInventory();
            }
            else if (selectDir > 0) {
                SelectNextInventory();
            }
            if (Input.GetButton("Use")) {
                dropTimer = dropTimer + Time.deltaTime;
            }
            else {
                if (Input.GetButtonUp("Use")) {
                    var item = _player.GetInventorySlot(inventorySelected);
                    if (item != null) {
                        if (dropTimer >= dropTime) {
                            _player.drop(item);
                        }
                        else {
                            _player.use(item);
                        }
                    }
                }
                dropTimer = 0;
            }
        }


        void SelectPreviousInventory() {
            if (dropTimer >= dropTime) {
                RearrangeLeft();
            }
            else {
                for (int i = 0; i < Player.MAX_INVENTORY_SIZE; i++) {
                    inventorySelected--;
                    if (inventorySelected < 0) {
                        inventorySelected = Player.MAX_INVENTORY_SIZE - 1;
                    }
                    if (_player.GetInventorySlot(inventorySelected) != null) {
                        break;
                    }
                }
                dropTimer = 0;
            }
        }

        void SelectNextInventory() {
            if (dropTimer >= dropTime) {
                RearrangeRight();
            }
            else {
                for (int i = 0; i < Player.MAX_INVENTORY_SIZE; i++) {
                    inventorySelected++;
                    if (inventorySelected >= Player.MAX_INVENTORY_SIZE) {
                        inventorySelected = 0;
                    }
                    if (_player.GetInventorySlot(inventorySelected) != null) {
                        break;
                    }
                }
                dropTimer = 0;
            }
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
                        for (int i = 0; i < pack.Data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i + inventorySelected));
                        }
                        for (int i = newSlot; i < Player.MAX_INVENTORY_SIZE; i++) {
                            if (i < inventorySelected || i > inventorySelected + pack.Data.slots) {
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
                    while (newSlot < Player.MAX_INVENTORY_SIZE && !(_player.GetInventorySlot(newSlot)is Pack)) {
                        newSlot++;
                    }
                    if (newSlot < Player.MAX_INVENTORY_SIZE) {
                        List<Item> newInventory = new List<Item>();
                        for (int i = 0; i < inventorySelected; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        Pack p2 = _player.GetInventorySlot(newSlot) as Pack;
                        for (int i = newSlot; i < newSlot + p2.Data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i));
                        }
                        for (int i = 0; i < pack.Data.slots + 1; i++) {
                            newInventory.Add(_player.GetInventorySlot(i + inventorySelected));
                        }
                        for (int i = newSlot + p2.Data.slots + 1; i < Player.MAX_INVENTORY_SIZE; i++) {
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
                    for (lastPackSlot = (int)Player.InventorySlot.PACK; lastPackSlot < Player.MAX_INVENTORY_SIZE; lastPackSlot++) {
                        var item = _player.GetInventorySlot(lastPackSlot);

                        Pack p;
                        if ((p = item as Pack) != null) {
                            curPackSlotsRemaining = p.Data.slots;
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
