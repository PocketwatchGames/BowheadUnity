// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WorldChunkComponent : MonoBehaviour {
	[SerializeField]
	MeshFilter _meshFilter;
	[SerializeField]
	Material _water;
	
	MeshCollider _meshCollider;

	public Mesh mesh => _meshFilter.mesh;

	void Awake() {
		_meshCollider = GetComponent<MeshCollider>();
	}

	public void UpdateCollider() {
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
		}
		_meshCollider = gameObject.AddComponent<MeshCollider>();
		_meshCollider.sharedMesh = mesh;

		if (gameObject.layer == Layers.Water) {
			GetComponent<MeshRenderer>().sharedMaterial = _water;
		}
	}

	public void Clear() {
		mesh.Clear();
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
			_meshCollider = null;
		}
	}
}
