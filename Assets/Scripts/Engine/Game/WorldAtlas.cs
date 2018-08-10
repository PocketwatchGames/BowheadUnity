// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WorldAtlas", menuName = "WorldAtlas")]
public sealed class WorldAtlas : ScriptableObject, ISerializationCallbackReceiver {
	public WorldAtlasMaterial[] materials;

	[Serializable]
	public struct RenderMaterials_t {
		public Material solid;
		public Material water;
	};

	public RenderMaterials_t renderMaterials;

	public void OnAfterDeserialize() {
		materials = Utils.Resize(materials, (int)EVoxelBlockType.NumBlockTypes - 1); // doesn't include air
	}

	public void OnBeforeSerialize() {
#if UNITY_EDITOR
		materials = Utils.Resize(materials, (int)EVoxelBlockType.NumBlockTypes - 1); // doesn't include air
#endif
	}
};
