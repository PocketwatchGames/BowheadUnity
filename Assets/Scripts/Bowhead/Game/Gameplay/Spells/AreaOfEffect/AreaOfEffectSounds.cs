// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

namespace Bowhead.Actors.Spells {

	public class AreaOfEffectSounds : ScriptableObject {
		public SoundCue placed;
		public SoundCue expired;
		public SoundCue destroyed;
		public SoundCue cast;
		public SoundCue spawned;

		public void Precache() {
			SoundCue.Precache(placed);
			SoundCue.Precache(expired);
			SoundCue.Precache(destroyed);
			SoundCue.Precache(cast);
			SoundCue.Precache(spawned);
		}
	}
}