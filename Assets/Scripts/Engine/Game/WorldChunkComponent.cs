// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class WorldChunkComponent : MonoBehaviour {
	[SerializeField]
	MeshFilter _meshFilter;
		
	MeshCollider _meshCollider;
	MeshRenderer _meshRenderer;

	public Mesh mesh => _meshFilter.mesh;

	void Awake() {
		_meshCollider = GetComponent<MeshCollider>();
		_meshRenderer = GetComponent<MeshRenderer>();
	}

	public void UpdateCollider() {
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
		}
		_meshCollider = gameObject.AddComponent<MeshCollider>();
		_meshCollider.sharedMesh = mesh;
	}

	public void SetSubmeshMaterials(WorldAtlas.RenderMaterials_t materials, int submeshCount) {
		var mats = new Material[submeshCount];
		var material = (gameObject.layer == Layers.Water) ? materials.water : materials.solid;
		
		for (int i = 0; i < submeshCount; ++i) {
			mats[i] = material;
		}

		_meshRenderer.sharedMaterials = mats;
	}

	public void SetPropertyBlock(MaterialPropertyBlock properties, int submesh) {
		_meshRenderer.SetPropertyBlock(properties, submesh);
	}

	public void Clear() {
		mesh.Clear();
		if (_meshCollider != null) {
			Utils.DestroyComponent(_meshCollider);
			_meshCollider = null;
		}
	}
}
