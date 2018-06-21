﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

	public abstract class InteractableData : EntityData {
		public GameObject_WRef prefab;

		new public static InteractableData Get(string name) {
			return DataManager.GetData<InteractableData>(name);
		}
	}

	public abstract class InteractableData<T> : InteractableData where T : InteractableData<T> {
		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}
