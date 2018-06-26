using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Client.UI {
    public class MoneyPanel : MonoBehaviour {
        UnityEngine.UI.Text _text;
        UnityEngine.UI.Text _weight;
        void Start() {
            _text = transform.GetChildComponent<UnityEngine.UI.Text>("Money");
            _weight = transform.GetChildComponent<UnityEngine.UI.Text>("WeightClass");
        }
        public void SetMoney(string t) {
            _text.text = t;
        }
        public void SetWeightClass(string t) {
            _weight.text = t;
        }
    }
}