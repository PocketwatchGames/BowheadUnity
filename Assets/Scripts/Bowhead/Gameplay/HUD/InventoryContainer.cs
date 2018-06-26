using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
    public class InventoryContainer : MonoBehaviour {

        private void OnDestroy() {
            gameObject.DestroyAllChildren();
        }

    }
}