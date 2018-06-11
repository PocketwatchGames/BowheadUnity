// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.Actors.Spells {
	public class SpellEvents : MonoBehaviour {

		public enum EActivity {
			None,
			EnableOnEnd,
			DisableOnEnd,
			DestroyOnEnd
		}

		public EActivity activity;
		public float destroyOnEndDelay;

		int EFFECT_BEGIN;
		int EFFECT_BEGIN_IMMEDIATE;
		int EFFECT_END;
		int EFFECT_CAST;

		Animator _animator;

		void Awake() {
			EFFECT_BEGIN = Animator.StringToHash("EffectBegin");
			EFFECT_BEGIN_IMMEDIATE = Animator.StringToHash("EffectBeginImmediate");
			EFFECT_END = Animator.StringToHash("EffectEnd");
			EFFECT_CAST = Animator.StringToHash("EffectCast");
			_animator = GetComponent<Animator>();

			gameObject.SetActive(false);
		}

		public virtual void EffectStart() {
			if (activity == EActivity.EnableOnEnd) {
				gameObject.SetActive(false);
			} else {
				gameObject.SetActive(true);
			}

			if (_animator != null) {
				_animator.SetTrigger(EFFECT_BEGIN);
			}
		}

		public virtual void EffectStartImmediate() {
			if (activity == EActivity.EnableOnEnd) {
				gameObject.SetActive(false);
			} else {
				gameObject.SetActive(true);
			}

			if (_animator != null) {
				_animator.SetTrigger(EFFECT_BEGIN_IMMEDIATE);
			}
		}

		public void EffectStop() {
			if (activity == EActivity.DestroyOnEnd) {
				Utils.DestroyGameObject(gameObject, destroyOnEndDelay);
			} else {
				if (activity == EActivity.DisableOnEnd) {
					gameObject.SetActive(false);
				} else {
					gameObject.SetActive(true);
				}
				if (_animator != null) {
					_animator.SetTrigger(EFFECT_END);
				}
			}
		}

		public void EffectCast() {
			if (_animator != null) {
				_animator.SetTrigger(EFFECT_CAST);
			}
		}
	}
}