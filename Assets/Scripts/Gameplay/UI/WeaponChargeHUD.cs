using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pawn = Bowhead.Actors.Pawn;

public class WeaponChargeHUD : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Slider _charge;
	[SerializeField]
	UnityEngine.UI.Image _fill;

	public float chargeMinDisplayTime = 0.2f;

	private Pawn _target;
	private int _slot;

	void LateUpdate() {
		return;

		var weapon = _target.GetInventorySlot(_slot) as Bowhead.Weapon;
		if (weapon != null && weapon.chargeTime > 0.2f) {
			float value = 0;
			if (weapon.data.attacks[weapon.attackHand].chargeTime > chargeMinDisplayTime) {
				value = Mathf.Min(1, (weapon.chargeTime - chargeMinDisplayTime) / (weapon.data.attacks[weapon.attackHand].chargeTime - chargeMinDisplayTime));
			}
			_charge.value = value;
			if (value < 1) {
				_fill.color = Color.gray;
			}
			else {
				_fill.color = Color.white;
			}
			_charge.gameObject.SetActive(true);
			transform.position = Camera.main.WorldToScreenPoint(_target.headPosition()) + new Vector3(0,20 * _slot,0);
		}
		else {
			_charge.gameObject.SetActive(false);
		}
	}




	public void SetTarget(Pawn p, int slot) {
		_target = p;
		_slot = slot;
	}
}
