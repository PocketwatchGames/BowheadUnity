using UnityEngine;
using System;

namespace Bowhead {
	[Serializable]
	public struct BloodSplatter {
		[MinMaxSlider(0, 8)]
		public Vector2 radius;
		[MinMaxSlider(0, 8)]
		public Vector2 size;
		[MinMaxSlider(0, 64)]
		public IntMath.Vector2i count;
	}
}
