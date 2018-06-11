// Copyright (c) 2018 Pocketwatch Games, LLC.

using UnityEngine;
using System;

namespace Bowhead {

	// Models an experience curve as:
	// (((x-1)/divisor)^exponent)+1
	public sealed class XPCurve : ScriptableObject {
		[SerializeField]
		float _exponent;
		[SerializeField]
		float _divisor;
		[SerializeField]
		float _preScale;
		[SerializeField]
		float _postScale;

		public float Eval(float t) {
			var x = Mathf.Pow((t-1f)/_divisor, _exponent);
			return ((x*_preScale)+1f)*_postScale;
		}
	}
}
