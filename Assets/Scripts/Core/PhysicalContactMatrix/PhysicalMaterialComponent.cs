// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(PhysicalMaterialQuery))]
	public class PhysicalMaterialComponent : MonoBehaviour {
		public PhysicalMaterialClass physicalMaterialClass;
	}
}