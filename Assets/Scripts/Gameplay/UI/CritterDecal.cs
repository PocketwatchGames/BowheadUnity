using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

public class CritterDecal : MonoBehaviour {

    private Pawn _target;
	[SerializeField]
	private GameObject _healthBar;
	[SerializeField]
	private UnityEngine.UI.Image _healthBarSlider;
	[SerializeField]
	private UnityEngine.UI.Image _aggroBar;


	private float _showHealthTimer;
	private float _destAngle = 0;
	private float _angle = 0;

	public float angleAdjustTime = 0.1f;

	// Update is called once per frame
	void LateUpdate () {

		if (_target == null || !_target.alive) {
			GameObject.Destroy(gameObject);
			Bowhead.GameManager.instance.clientWorld.DamageEvent -= OnDamage;
		} else { 
			transform.position = _target.footPosition();
			_healthBar.SetActive(_target.health < _target.maxHealth);
			if (_showHealthTimer > 0) {
				_showHealthTimer = Mathf.Max(0, _showHealthTimer - Time.deltaTime);
			}
			if (_showHealthTimer > 7) {
				_healthBar.GetComponent<CanvasGroup>().alpha = 1.0f;
			} else if (_showHealthTimer <= 0) {
				_healthBar.GetComponent<CanvasGroup>().alpha = 0.1f;
			} else {
				_healthBar.GetComponent<CanvasGroup>().alpha = 0.1f + 0.9f * _showHealthTimer / 7;
			}
			_healthBarSlider.fillAmount = Mathf.Clamp01(_target.health / _target.maxHealth);
		} 

	}

    public void SetTarget(Pawn p)
    {
        _target = p;
		Bowhead.GameManager.instance.clientWorld.DamageEvent += OnDamage;
    }
	private void OnDamage(Pawn p, float d, bool directHit) {
		_showHealthTimer = 10;
	}
}
