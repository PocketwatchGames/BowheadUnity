using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class InventoryPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }

        public void Init(Player player) {
            _player = player;
        }
    }
}
