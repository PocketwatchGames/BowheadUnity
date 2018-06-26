// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public sealed class ClickThrough : ChangeLayer<ClickThrough> {

		public static void Push() {
			var objs = objects;
			for (int i = 0; i < objs.Count; ++i) {
				objs[i].SetLayer(Layers.IgnoreRaycast);
			}
		}

		public static void Pop() {
			var objs = objects;
			for (int i = 0; i < objs.Count; ++i) {
				objs[i].RestoreLayer();
			}
		}
	}
}