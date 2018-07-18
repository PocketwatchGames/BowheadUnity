using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Bowhead;
public class StatusEffectHUD : MonoBehaviour {

	public Text nameText;
	public Slider slider;

	public StatusEffectData statusEffectType { get; private set; }
	private List<StatusEffect> _statusEffects = new List<StatusEffect>();

	public void Init(StatusEffect e) {
		statusEffectType = e.data;
		_statusEffects.Add(e);
        nameText.text = GetText();
		slider.value = GetMaxTime();
	}
	
	// Update is called once per frame
	void Update () {
		if (_statusEffects == null || _statusEffects.Count == 0) {
			Destroy(gameObject);
		}
		else {
			slider.value = GetMaxTime();
		}
		if (_statusEffects.RemoveAll(e => e.time <= 0) > 0) {
			nameText.text = GetText();
		}
	}

	float GetMaxTime() {
		float maxT = 0;
		foreach (var e in _statusEffects) {
			maxT = Mathf.Max(maxT, e.time / e.totalTime);
		}
		return maxT;
	}

	string GetText() {
		if (_statusEffects.Count > 1) {
			return statusEffectType.name + " x" + _statusEffects.Count;
		} else {
			return statusEffectType.name;
		}
	}
}
