// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead {
	public class ExplosionDecalComponent : MonoBehaviour {

		public Vector2 size;
		public bool drawOnWater;

		void Start() {
			var clworld = GameManager.instance.clientWorld;
			if (clworld != null) {
				//clworld.RenderExplosionSplat(Utils.PutPositionOnGroundOrWater(transform.position), size, GameManager.instance.randomNumber * 360, drawOnWater);
			}
		}
	}
}