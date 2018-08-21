using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

public class PlayerDecal : MonoBehaviour {

    private Pawn _target;
	[SerializeField]
	private UnityEngine.UI.Image _healthBar;
	[SerializeField]
	private UnityEngine.UI.Image _shieldBar;

	private float _destAngle = 0;
	private float _angle = 0;

	public float angleAdjustTime = 0.1f;

	// Update is called once per frame
	void LateUpdate () {

		if (_target != null) {
            transform.position = _target.footPosition();

			var shield = _target.GetDefensiveWeapon();

			if (shield == null) {
				_shieldBar.gameObject.SetActive(false);
			} else {
				if (shield.chargeTime > 0) {
					_destAngle = shield.data.blockAngleRange;
				} else {
					_destAngle = 0;
				}
				_angle += (_destAngle - _angle) * Mathf.Min(1.0f, Time.deltaTime / angleAdjustTime);
				_shieldBar.fillAmount = _angle / 180.0f;
				_shieldBar.transform.rotation = Quaternion.Euler(90, _target.yaw * Mathf.Rad2Deg - _angle, 0);
			}
			_healthBar.fillAmount = Mathf.Clamp01(_target.health / _target.maxHealth);
		}

	}

    public void SetTarget(Pawn p)
    {
        _target = p;
    }
}
