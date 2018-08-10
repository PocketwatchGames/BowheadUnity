// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAtlastMaterialTextures", menuName = "WorldAtlasMaterialTextures")]
public sealed class WorldAtlasMaterialTextures : ScriptableObject {
	
	[Serializable]
	public struct TextureSet {
		public Texture2D top;
		public Texture2D sides;
		public Texture2D bottom;
	};

	public TextureSet albedo;
	public TextureSet normals;
}
