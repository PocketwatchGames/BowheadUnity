// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAtlastMaterialTextures", menuName = "WorldAtlasMaterialTextures")]
public sealed class WorldAtlasMaterialTextures : ScriptableObject {
	public Material material;
	public Texture2D albedo;
}
