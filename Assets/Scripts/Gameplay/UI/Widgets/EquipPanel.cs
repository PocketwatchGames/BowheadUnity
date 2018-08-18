using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead.Client.UI {
    public class EquipPanel : MonoBehaviour {

        [SerializeField]
        Player _player;

		[SerializeField]
		Transform _mainContainer;
		[SerializeField]
		Transform _buttonHintContainer;
		[SerializeField]
		EquipSlot _equipSlotLeft;
		[SerializeField]
		EquipSlot _equipSlotRight;
		[SerializeField]
		StatusEffectHUD _statusEffectPrefab;


        private EquipSlot[] _slots = new EquipSlot[Player.MaxInventorySize];

		[SerializeField] GameObject _statusEffectContainer;


		public void Init(Player player) {
            _player = player;
			_equipSlotLeft.Init(_player, 0);
			_equipSlotRight.Init(_player, 1);

			_player.OnInventoryChange += OnInventoryChange;
			GameManager.instance.clientWorld.StatusEffectAddedEvent += OnStatusEffectAdded;
			_player.OnInputMethodChange += OnInputMethodChange;
			_player.OnMountChange += OnInventoryChange;

			OnInventoryChange();
		}


		private void OnInventoryChange() {
            Rebuild();
		}

		private void OnDestroy() {
			_player.OnInventoryChange -= OnInventoryChange;
			_player.OnMountChange -= OnInventoryChange;
			_player.OnInputMethodChange -= OnInputMethodChange;
		}

		private void OnStatusEffectAdded(Pawn target, StatusEffect e) {
            if (target != _player) {
                return;
            }

			foreach (var s in GetComponentsInChildren<StatusEffectHUD>()) {
				if (s.statusEffectType == e.data) {
					s.Init(e);
					return;
				}
			}

			var hud = Instantiate<StatusEffectHUD>(_statusEffectPrefab, _statusEffectContainer.transform);
			hud.Init(e);
		}


		private void OnInputMethodChange() 
		{
			Rebuild();
		}

		private void Rebuild() {
			_equipSlotLeft.SetItem(_player.GetInventorySlot((int)Player.InventorySlot.LEFT_HAND), _player.GetInventorySlot((int)Player.InventorySlot.LEFT_HAND_ALT));
			_equipSlotRight.SetItem(_player.GetInventorySlot((int)Player.InventorySlot.RIGHT_HAND), _player.GetInventorySlot((int)Player.InventorySlot.RIGHT_HAND_ALT));

			_buttonHintContainer.DestroyAllChildren();

			{
				var b = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, _buttonHintContainer.transform, false);
				b.SetButton(_player.GetButtonHint("B"));
				b.SetHint("Pack");
				b.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
			}
			{
				var b = GameObject.Instantiate(GameManager.instance.clientData.hudButtonHintPrefab, _buttonHintContainer.transform, false);
				b.SetButton(_player.GetButtonHint("X"));
				if (_player.mount != null) {
					b.SetHint("Dismount");
				} else {
					b.SetHint("Swap");
				}
				b.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
			}
		}



	}
}
