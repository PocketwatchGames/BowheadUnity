// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine.Assertions;

namespace Bowhead {

	public abstract class ChangeLayer<T> : ClientScriptRegistry<T> where T: ChangeLayer<T>{

		int _layer = -1;

		protected void SetLayer(int layer) {
			if (_layer == -1) {
				_layer = gameObject.layer;
			}
			gameObject.layer = layer;
		}

		protected void RestoreLayer() {
			gameObject.layer = _layer;
			_layer = -1;
		}

	}
}