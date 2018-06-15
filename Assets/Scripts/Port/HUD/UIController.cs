using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class UIController : MonoBehaviour {

        [SerializeField]
        private World _world;

        private InventoryPanel _inventory;
        private PlayerStatePanel _playerState;


        // Use this for initialization
        void Start() {
            _inventory = Instantiate(DataManager.GetPrefab<InventoryPanel>("InventoryPanel"));
            _inventory.transform.SetParent(transform, false);
            _inventory.Init(_world.player);


            _playerState = Instantiate(DataManager.GetPrefab<PlayerStatePanel>("PlayerStatePanel"));
            _playerState.transform.SetParent(transform, false);
            _playerState.Init(_world.player);
        }

        // Update is called once per frame
        void Update() {
        }
    }
}