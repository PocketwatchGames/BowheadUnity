// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldAtlasClientData_WRef : WeakAssetRef<WorldAtlasClientData> { };
public sealed class WorldAtlasClientData : ScriptableObject {
	public Texture2DArray terrainTextureArray;
}