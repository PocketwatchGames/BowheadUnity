using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class MoneyPanel : MonoBehaviour {
        UnityEngine.UI.Text _text;
        void Start() {
            _text = transform.GetChildComponent<UnityEngine.UI.Text>("Money");
        }
        public void SetMoney(string t) {
            _text.text = t;
        }
    }
}