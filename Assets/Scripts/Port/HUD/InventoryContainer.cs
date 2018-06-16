using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class InventoryContainer : MonoBehaviour {

        private void OnDestroy() {
            gameObject.DestroyAllChildren();
        }

    }
}