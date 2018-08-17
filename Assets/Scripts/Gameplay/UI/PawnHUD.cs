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
			if (Vector2.Dot(Camera.main.transform.forward, _target.headPosition() - Camera.main.transform.position) < 0) {
				transform.GetChild(0).gameObject.SetActive(false);
			}
			else {
				transform.GetChild(0).gameObject.SetActive(true);
			}
		}
		if (h < 1 && critter.alive) {
			_health.value = h;
			_health.gameObject.SetActive(true);
		}
		else {
			_health.gameObject.SetActive(false);
		}

		if (w > 0 && critter.panic == 0 && critter.alive) {
			_aggro.gameObject.SetActive(true);
			if (w > critter.data.waryLimit) {
				_aggro.transform.GetChild(1).GetChildComponent<UnityEngine.UI.Image>("Fill").color = Color.red;
				_aggro.value = Mathf.Clamp01((w-critter.data.waryLimit)/ (critter.data.investigateLimit - critter.data.waryLimit));
			}
			else {
				_aggro.transform.GetChild(1).GetChildComponent<UnityEngine.UI.Image>("Fill").color = Color.white;
				_aggro.value = Mathf.Clamp01(w/ critter.data.waryLimit);
			}
		}
		else {
			_aggro.gameObject.SetActive(false);
		}
	}




	public void SetTarget(Pawn p) {
		_target = p;
	}
}
