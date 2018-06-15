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

        public void Init(Player player) {
            _player = player;
            var r = _inventorySlotPrefab.GetComponent<RectTransform>().rect;
            slotSize = new Vector3(r.width, r.height, 0);

            var mainContainer = Instantiate(_inventoryContainerPrefab, transform, false);
            for (int slot = 0; slot <= (int)Player.InventorySlot.RIGHT_HAND; slot++) {
                var s = Instantiate(_inventorySlotPrefab, mainContainer.transform, false);
                s.GetComponent<RectTransform>().anchoredPosition = new Vector2(slot * (slotSize.x + slotMargin) + slotMargin + slotSize.x / 2, 0);
                s.Init((Player.InventorySlot)slot);
                s.SetItem(player.inventory[slot]);
            }
        }

    }
}
