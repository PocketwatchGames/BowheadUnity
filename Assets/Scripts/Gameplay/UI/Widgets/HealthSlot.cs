using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Player = Bowhead.Actors.Player;
namespace Bowhead.Client.UI {
	public class HealthSlot : MonoBehaviour {

		[SerializeField]
		private Text _text;
		[SerializeField]
		private Image _healthimer;
		[SerializeField]
		private Image _stunTimer;
		[SerializeField]
		private Image _background;
		private Player _player;


		public void Init(Player p) {
			_player = p;
			_text.gameObject.SetActive(false);
		}


		private void Update() {
			_healthimer.fillAmount = Mathf.Clamp01(_player.health / _player.maxHealth);
			_stunTimer.fillAmount = Mathf.Clamp01(_player.stunAmount / _player.data.stunLimit);
			_stunTimer.color = _player.stunned ? Color.yellow : Color.grey;
		}


		private void OnDestroy() {
			gameObject.DestroyAllChildren();
		}
	}
}
