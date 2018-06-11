// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead.Actors.Spells {
	[Serializable]
	public struct SpellEffectSounds {
		public SoundCue cast;
		public SoundCue expired;
		public SoundCue cleansed;

		public void Precache() {
			SoundCue.Precache(cast);
			SoundCue.Precache(expired);
			SoundCue.Precache(cleansed);
		}
	}

	public abstract class SpellEffectClass : StaticVersionedAssetWithSerializationCallback {
		const int VERSION = 1;

		[HideInInspector]
		[SerializeField]
		protected string spellEffectActorClassString;
		public float timeToLiveAfterSpellExpiry;

		public SpellEffectSounds sounds;

		public Type instanceType {
			get;
			private set;
		}
		
		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (string.IsNullOrEmpty(spellEffectActorClassString)) {
				instanceType = null;
			} else {
				instanceType = Type.GetType(spellEffectActorClassString);
			}
		}

		public virtual void Precache() {
			sounds.Precache();
		}

		public static void Precache(IList<SpellEffectClass> classes) {
			if (classes != null) {
				for (int i = 0; i < classes.Count; ++i) {
					var c = classes[i];
					if (c != null) {
						c.Precache();
					}
				}
			}
		}

		public T Spawn<T>(World world) where T: SpellEffectActor {
			if (instanceType != null) {
				return (T)world.Spawn(instanceType, null, SpawnParameters.defaultParameters);
			}
			throw new System.Exception("Missing SpellEffectActor class " + spellEffectActorClassString);
		}

#if UNITY_EDITOR
		protected sealed override void InitVersion() {
			OnInitVersion();
			version = VERSION;
		}

		protected virtual void OnInitVersion() {}
#endif
	}
}
