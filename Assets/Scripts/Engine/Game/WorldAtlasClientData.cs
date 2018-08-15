// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WorldAtlasClientData_WRef : WeakAssetRef<WorldAtlasClientData> { };
public sealed class WorldAtlasClientData : ScriptableObject, ISerializationCallbackReceiver {
	public const int TEX2ARRAYINDEX_SIZE = ((int)EVoxelBlockType.NumBlockTypes - 1) * 3;
	
	[Serializable]
	public struct TerrainTextureChannel {
		public Texture2DArray textureArray;
		public int[] textureSet2ArrayIndex;
	};

	public TerrainTextureChannel albedo;
	public TerrainTextureChannel normals;
	//public TerrainTextureChannel roughness;
	//public TerrainTextureChannel ao;
	//public TerrainTextureChannel height;
	public TerrainTextureChannel rho;
	public int[] block2TextureSet;

	public WorldAtlas.RenderMaterials_t renderMaterials;

	public void OnBeforeSerialize() {
#if UNITY_EDITOR
		Resize();
#endif
	}

	public void OnAfterDeserialize() {
		Resize();
	}

	void Resize() {
		block2TextureSet = Utils.Resize(block2TextureSet, (int)EVoxelBlockType.NumBlockTypes - 1);
		albedo.textureSet2ArrayIndex = Utils.Resize(albedo.textureSet2ArrayIndex, TEX2ARRAYINDEX_SIZE);
	}
}