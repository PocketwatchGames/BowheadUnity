using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bowhead.Actors;
public class StatusEffectHUD : MonoBehaviour {

	public Text name;
	public Slider slider;

	private StatusEffect _statusEffect;

	public void Init(StatusEffect e) {
		_statusEffect = e;
		name.text = _statusEffect.data.name;
		slider.value = 1;
	}
	
	// Update is called once per frame
	void Update () {
		if (_statusEffect.time <= 0) {
			Destroy(gameObject);
		}
		else {
			slider.value = _statusEffect.time / _statusEffect.totalTime;
		}
	}
}
