// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public class PhysicalMaterialQuery : MonoBehaviour {

		PhysicalMaterialComponent _colliderMaterial;

		protected virtual void Awake() {
			_colliderMaterial = GetComponent<PhysicalMaterialComponent>();
		}

		public virtual PhysicalMaterialClass GetMaterialAtPoint(Vector3 point) {
			return (_colliderMaterial != null) ? _colliderMaterial.physicalMaterialClass : null;
		}
	}
}