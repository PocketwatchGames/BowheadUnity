using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pawn = Bowhead.Actors.Pawn;
using Critter = Bowhead.Actors.Critter;

public class PawnHUD : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Slider _health;
	[SerializeField]
	UnityEngine.UI.Slider _aggro;
	[SerializeField]
	UnityEngine.UI.Image _sight;
	[SerializeField]
	UnityEngine.UI.Image _scent;
	[SerializeField]
	UnityEngine.UI.Image _hearing;

	private Pawn _target;

	
	void LateUpdate () {
		if (_target == null || _target.go == null) {
			Destroy(gameObject);
			return;
		}
		float h = _target.health / _target.maxHealth;

		Critter critter = _target as Critter;

		float w = critter.wary;
		if (h < 1 || w > 0) {
			transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
		}
		if (h < 1) {
			_health.value = h;
			_health.gameObject.SetActive(true);
		}
		else {
			_health.gameObject.SetActive(false);
		}
		if (w > 0 && critter.panic == 0) {
			_aggro.value = Mathf.Clamp01(w);
			_aggro.gameObject.SetActive(true);
		}
		else {
			_aggro.gameObject.SetActive(false);
		}
		_scent.gameObject.SetActive(critter.canSmell > 0 && critter.panic == 0);
		_sight.gameObject.SetActive(critter.canSee > 0 && critter.panic == 0);
		_hearing.gameObject.SetActive(critter.canHear > 0 && critter.panic == 0);
	}




	public void SetTarget(Pawn p) {
		_target = p;
	}
}
