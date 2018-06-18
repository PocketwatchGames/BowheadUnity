using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Port {
    public class PlayerStatePanel : MonoBehaviour {

        [SerializeField]
        Player _player;

        Slider _health;
        Slider _water;
        Slider _stamina;
        MoneyPanel _money;


        // Use this for initialization
        void Start() {
            _health = transform.GetAnyChildComponent<Slider>("Health");
            _water = transform.GetAnyChildComponent<Slider>("Water");
            _stamina = transform.GetAnyChildComponent<Slider>("Stamina");
            _money = transform.GetAnyChildComponent<MoneyPanel>("Money");
        }

        // Update is called once per frame
        void Update() {
            if (_player == null) {
                return;
            }

            _health.value = _player.health / _player.maxHealth;
            _water.value = _player.thirst / _player.maxThirst;
            _stamina.value = _player.stamina / _player.maxStamina;
        }

        public void Init(Player player) {
            _player = player;
            _player.OnMoneyChange += onMoneyChange;
        }

        private void onMoneyChange() {
            _money.SetMoney(_player.money.ToString());
        }
    }
}
