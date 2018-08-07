// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldAtlasClientData_WRef : WeakAssetRef<WorldAtlasClientData> { };
public sealed class WorldAtlasClientData : ScriptableObject, ISerializationCallbackReceiver {
	[Serializable]
	public struct TerrainTextures {
		public Texture2DArray albedo;
	};

	public TerrainTextures terrainTextures;
	public int[] materialTextureArrayIndices;

	public void OnBeforeSerialize() {
#if UNITY_EDITOR
		Resize();
#endif
	}

	public void OnAfterDeserialize() {
		Resize();
	}

	void Resize() {
		materialTextureArrayIndices = Utils.Resize(materialTextureArrayIndices, (int)EVoxelBlockType.NumBlockTypes - 1);
	}
}