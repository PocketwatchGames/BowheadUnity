using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pawn = Bowhead.Actors.Pawn;

public class WeaponChargeHUD : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Slider _charge;

	private Pawn _target;
	private int _slot;

	void LateUpdate() {
		var weapon = _target.GetInventorySlot(_slot) as Bowhead.Weapon;
		if (weapon != null && weapon.chargeTime > 0.25f) {
			_charge.value = weapon.chargeTime / weapon.data.chargeTime;
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
