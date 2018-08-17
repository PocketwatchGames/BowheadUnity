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

		}

        public void Init(Player player) {
            _player = player;
        }

    }
}
