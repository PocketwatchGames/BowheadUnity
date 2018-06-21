using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class UIController : MonoBehaviour {

        private InventoryPanel _inventory;
        private PlayerStatePanel _playerState;

        [SerializeField]
        private InventoryPanel _inventoryPrefab;
        [SerializeField]
        private PlayerStatePanel _playerStatePrefab;
		
        public void SetPlayer(Player player) {
            _inventory = Instantiate(_inventoryPrefab, transform, false);
            _inventory.Init(player);

            _playerState = Instantiate(_playerStatePrefab, transform, false);
            _playerState.Init(player);
        }
    }
}