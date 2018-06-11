// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead {

	[Serializable]
	public struct BloodSpray {
		public ParticleSystem prefab;
		public float probability;
		[HideInInspector]
		public bool initialized;
	}

	[Serializable]
	public sealed class BloodSprays_WRef : WeakAssetRef<BloodSprays> { }

	public class BloodSprays : ScriptableObject, ISerializationCallbackReceiver {

		[SerializeField]
		BloodSpray[] blood;

		float totalp;

		public GameObject GetRandomBlood(float random) {
			float p = 0f;

			if (blood != null) {
				for (int i = 0; i < blood.Length; ++i) {
					var b = blood[i];
					if ((b.prefab != null) && (b.probability > 0f)) {
						p += b.probability / totalp;
						if (random <= p) {
							return b.prefab.gameObject;
						}
					}
				}
			}

			return null;
		}

		public void OnBeforeSerialize() {
#if UNITY_EDITOR
			if (blood != null) {
				for (int i = 0; i < blood.Length; ++i) {
					var b = blood[i];
					if (!b.initialized) {
						b.initialized = true;
						b.probability = 1;
						blood[i] = b;
					}
				}
			}
#endif
		}

		public void OnAfterDeserialize() {
			totalp = 0;
			if (blood != null) {
				for (int i = 0; i < blood.Length; ++i) {
					var b = blood[i];
					if (b.prefab != null) {
						totalp += b.probability;
					}
				}
			}
		}
	}
}