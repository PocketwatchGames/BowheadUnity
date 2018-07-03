// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class World_ChunkComponent : MonoBehaviour {
	[SerializeField]
	MeshFilter _meshFilter;
	
	MeshCollider _meshCollider;

	public Mesh mesh => _meshFilter.mesh;

	public void UpdateCollider() {
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
		}
		_meshCollider = gameObject.AddComponent<MeshCollider>();
		_meshCollider.sharedMesh = mesh;
	}

	public void Clear() {
		mesh.Clear();
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
			_meshCollider = null;
		}
	}
}
