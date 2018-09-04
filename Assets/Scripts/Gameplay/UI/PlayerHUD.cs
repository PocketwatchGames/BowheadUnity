using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player = Bowhead.Actors.Player;

public class PlayerHUD : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Text _weaponMultiplier;

    private Player _target;

    void LateUpdate()
    {
        if (_target == null || _target.go == null)
        {
            Destroy(gameObject);
            return;
        }

		//var mountOrTarget = _target.mount ?? _target;
		var mountOrTarget = _target;

		float wm = 0;
		for (int i=0;i<Player.MaxInventorySize;i++) {
			var weapon = _target.GetInventorySlot(i) as Bowhead.Weapon;
			if (weapon != null && weapon.data.attacks.Length > 0) {
				float curChargeMultiplier = weapon.GetMultiplier(_target, weapon.chargeTime);
				if (weapon.chargeTime > 0 && (weapon.chargeTime >= weapon.data.attacks[weapon.attackHand].chargeTime || curChargeMultiplier > 1)) {
					wm = Mathf.Max(wm, curChargeMultiplier);
				}
			}
		}
		if (wm <= 1) {
			_weaponMultiplier.gameObject.SetActive(false);
		}
		else {
			_weaponMultiplier.fontSize = 20 + (int)(2 * wm);
			_weaponMultiplier.gameObject.SetActive(true);
			_weaponMultiplier.text = "x" + Mathf.FloorToInt(wm);
		}

        if (wm > 1)
        {
            transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
        }

	}

	bool showDebug;

	void OnGUI() {

		if (_target != null) {
			if (Event.current.type == EventType.KeyDown) {
				if (Event.current.keyCode == KeyCode.F3) {
					showDebug = !showDebug;
				}
			}

			if (showDebug) {
				_target.world.worldStreaming.DrawDebugHUD();
			}
		}
	}


	public void SetTarget(Player p)
    {
        _target = p;
    }
}
