// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAtlastMaterial", menuName = "WorldAtlasMaterial")]
public sealed class WorldAtlasMaterial : ScriptableObject {
	public WorldAtlasMaterialPhysics physics;
	public WorldAtlasMaterialTextures textures;
}
