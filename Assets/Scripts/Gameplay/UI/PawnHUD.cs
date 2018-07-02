using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pawn = Bowhead.Actors.Pawn;

public class PawnHUD : MonoBehaviour {

	[SerializeField]
	UnityEngine.UI.Slider _health;

	private Pawn _target;

	
	void LateUpdate () {
		if (_target == null || _target.go == null) {
			Destroy(gameObject);
			return;
		}
		float h = _target.health / _target.maxHealth;
		if (h < 1) {
			_health.value = h;
			_health.gameObject.SetActive(true);
			transform.position = Camera.main.WorldToScreenPoint(_target.headPosition());
		}
		else {
			_health.gameObject.SetActive(false);
		}
	}




	public void SetTarget(Pawn p) {
		_target = p;
	}
}
