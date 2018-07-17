using System;
using UnityEngine;

namespace Bowhead.Actors {

	public abstract class StatusEffectData : EntityData {

		new public static StatusEffectData Get(string name) {
			return DataManager.GetData<StatusEffectData>(name);
		}

	}

	public abstract class StatusEffectData<T> : StatusEffectData where T : StatusEffectData<T> {

		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}