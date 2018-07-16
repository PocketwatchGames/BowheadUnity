using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player = Bowhead.Actors.Player;

public class PlayerHUD : MonoBehaviour {

    [SerializeField]
    UnityEngine.UI.Slider _health;
    [SerializeField]
    UnityEngine.UI.Slider _stamina;
    [SerializeField]
    UnityEngine.UI.Image _staminaFill;
    [SerializeField]
    UnityEngine.UI.Slider _water;
	[SerializeField]
	UnityEngine.UI.Text _weaponMultiplier;

    private Player _target;
	private float _changeTimerWater, _changeTimerHealth;

    void LateUpdate()
    {
        if (_target == null || _target.go == null)
        {
            Destroy(gameObject);
            return;
        }

		var mountOrTarget = _target.mount ?? _target;

        float h = _target.health / _target.maxHealth;
        float s = mountOrTarget.stamina / mountOrTarget.maxStamina;
        float w = _target.water / _target.maxWater;
		float wm = 0;
		for (int i=0;i<Player.MaxInventorySize;i++) {
			var weapon = _target.GetInventorySlot(i) as Bowhead.Weapon;
			if (weapon != null) {
				wm = Mathf.Max(wm, weapon.GetMultiplier());
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

		if (_health.value != h) {
			_changeTimerHealth = 3;
		}
		if (_water.value != w) {
			_changeTimerWater = 3;
		}

        if (h < 1 || w < 1 || s < 1 || _changeTimerHealth > 0 || _changeTimerWater > 0 || wm > 1)
        {
            transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
            _health.value = h;
            _water.value = w;
            _stamina.value = s;
            _staminaFill.color = mountOrTarget.recovering ? Color.yellow : Color.green;
        }
        _stamina.gameObject.SetActive(s < 1);
        _health.gameObject.SetActive(_changeTimerHealth > 0 || (h < 1 && _target.stance == Player.Stance.Combat));
        _water.gameObject.SetActive(_changeTimerWater > 0 || (w < 1 && _target.stance == Player.Stance.Combat));

		_changeTimerHealth = Mathf.Max(0, _changeTimerHealth - Time.deltaTime);
		_changeTimerWater = Mathf.Max(0, _changeTimerWater - Time.deltaTime);

	}




	public void SetTarget(Player p)
    {
        _target = p;
    }
}
