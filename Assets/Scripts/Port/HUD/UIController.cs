using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class UIController : MonoBehaviour {

        [SerializeField]
        private GameWorld _world;

        private InventoryPanel _inventory;
        private PlayerStatePanel _playerState;

        [SerializeField]
        private InventoryPanel _inventoryPrefab;
        [SerializeField]
        private PlayerStatePanel _playerStatePrefab;


        // Use this for initialization
        void Start() {
            _inventory = Instantiate(_inventoryPrefab, transform, false);
            _inventory.Init(_world.player);


            _playerState = Instantiate(_playerStatePrefab, transform, false);
            _playerState.Init(_world.player);
        }


        // Update is called once per frame
        void Update() {
        }
    }
}