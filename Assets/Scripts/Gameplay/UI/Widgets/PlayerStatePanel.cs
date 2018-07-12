using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using Player = Bowhead.Actors.Player;
    public class PlayerStatePanel : MonoBehaviour {

		[SerializeField]
		ButtonHint _buttonHintJump;

		[SerializeField]
		ButtonHint _buttonHintStance;

		[SerializeField]
        Player _player;

		[SerializeField]
		Slider _health;
		[SerializeField]
		Slider _water;
		[SerializeField]
		Slider _stamina;



        // Use this for initialization
        void Start() {

			_buttonHintJump.SetButton("A");
			_buttonHintJump.SetHint("Jump/Sprint");
			_buttonHintStance.SetButton("B");
			_buttonHintStance.SetHint("Sheathe Weapons");
        }

        // Update is called once per frame
        void Update() {
            if (_player == null) {
                return;
            }

            _health.value = _player.health / _player.maxHealth;
            _water.value = _player.water / _player.maxWater;
            _stamina.value = _player.stamina / _player.maxStamina;

			if (_player.stance == Player.Stance.Combat) {
				_buttonHintStance.SetHint("Sheathe Weapons");
			}
			else {
				_buttonHintStance.SetHint("Unsheathe Weapons");
			}
		}

        public void Init(Player player) {
            _player = player;
        }

    }
}
