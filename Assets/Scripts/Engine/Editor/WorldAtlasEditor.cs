// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldAtlas))]
public class WorldAtlasEditor : Editor {

	SerializedProperty _materials;
	bool _materialsExpanded;

	void OnEnable() {
		_materials = serializedObject.FindProperty("materials");
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();

		EditorGUILayout.BeginVertical();

		_materialsExpanded = EditorGUILayout.Foldout(_materialsExpanded, "Materials", true);

		if (_materialsExpanded) {
			++EditorGUI.indentLevel;
			EditMaterials();
			--EditorGUI.indentLevel;
		}

		EditorGUILayout.BeginVertical("GroupBox");
		EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Rebuild AtlasData...")) {
			RebuildAtlasData();
		}
		if (GUILayout.Button("Rebuild TextureArray...")) {
			RebuildTextureArray();
		}
		EditorGUILayout.EndHorizontal();
		if (GUILayout.Button("Rebuild All...")) {
			RebuildAll();
		}
		EditorGUILayout.EndVertical();
		EditorGUILayout.EndVertical();

		serializedObject.ApplyModifiedProperties();
	}

	void EditMaterials() {
		var size = _materials.arraySize;
		for (int i = 0; i < size; ++i) {
			EditorGUILayout.PropertyField(_materials.GetArrayElementAtIndex(i), new GUIContent(((EVoxelBlockType)(i + 1)).ToString()), true);
		}
	}

	WorldAtlasData LoadAtlasData(WorldAtlas atlas) {
		var path = atlas.GetAssetFolderPath();
		path = path + "/" + atlas.name + "_AtlasData.asset";
		var atlasData = AssetDatabase.LoadAssetAtPath<WorldAtlasData>(path);
		if (atlasData == null) {
			atlasData = CreateInstance<WorldAtlasData>();
			AssetDatabase.CreateAsset(atlasData, path);
		}
		return atlasData;
	}

	WorldAtlasClientData LoadAtlasClientData(WorldAtlas atlas) {
		var folder = atlas.GetAssetFolderPath();
		var path = folder + "/Resources/" + atlas.name + "_AtlasClientData.asset";
		var atlasData = AssetDatabase.LoadAssetAtPath<WorldAtlasClientData>(path);
		if (atlasData == null) {
			atlasData = CreateInstance<WorldAtlasClientData>();
			AssetDatabase.CreateFolder(folder, "Resources");
			AssetDatabase.CreateAsset(atlasData, path);
		}
		return atlasData;
	}

	Texture2DArray SaveTextureArray(WorldAtlas atlas, Texture2DArray array, string channel) {
		var path = atlas.GetAssetFolderPath();
		path = path + "/" + atlas.name + "_" + channel + "TextureArray.asset";
		var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
		if (existing != null) {
			EditorUtility.CopySerialized(array, existing);
			EditorUtility.SetDirty(existing);
			array = existing;
		} else {
			AssetDatabase.CreateAsset(array, path);
		}
		return array;
	}

	Texture2DArray LoadTextureArray(WorldAtlas atlas, string channel) {
		var path = atlas.GetAssetFolderPath();
		path = path + "/" + atlas.name + "_" + channel + "TextureArray.asset";
		return AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
	}

	void LoadTextureList(WorldAtlas atlas, out List<WorldAtlasMaterialTextures> arr, out List<int> indices) {
		arr = new List<WorldAtlasMaterialTextures>();
		indices = new List<int>();

		for (int i = 0; i < atlas.materials.Length; ++i) {
			var m = atlas.materials[i];

			if (m == null) {
				ThrowAssetException(atlas, "Material for terrain type '" + ((EVoxelBlockType)(i + 1)).ToString() + "' is not set!");
			}

			if (m.textures != null) {
				var idx = arr.FindIndex(0, arr.Count, x => x == m.textures);
				if (idx >= 0) {
					indices.Add(idx);
				} else {
					indices.Add(arr.Count);
					arr.Add(m.textures);
				}
			} else {
				ThrowAssetException(m, "Missing textures for atlas material.");
			}
		}
	}

	void RebuildAtlasData() {
	}

	void RebuildTextureArray() {
		foreach (var obj in serializedObject.targetObjects) {
			var atlas = (WorldAtlas)obj;

			List<WorldAtlasMaterialTextures> textures;
			List<int> indices;

			LoadTextureList(atlas, out textures, out indices);

			if (textures.Count < 1) {
				throw new Exception("No textures defined in atlas!");
			}

			{
				var settings = new ImportSettings_t() {
					alphaSource = TextureImporterAlphaSource.FromInput,
					alphaIsTransparency = true,
					aniso = 16,
					filterMode = FilterMode.Trilinear,
					mipmap = true,
					readable = true,
					type = TextureImporterType.Default,
					format = TextureImporterFormat.DXT5
				};

				CreateTextureArray(atlas, settings, textures, (m) => m.albedo, "Albedo");
			}
		}
	}

	struct ImportSettings_t {
		public TextureImporterAlphaSource alphaSource;
		public bool alphaIsTransparency;
		public int aniso;
		public FilterMode filterMode;
		public bool mipmap;
		public bool readable;
		public TextureImporterType type;
		public TextureImporterFormat format;

		public bool Apply(TextureImporterSettings s) {
			bool changed = false;

			if (s.alphaIsTransparency != alphaIsTransparency) {
				s.alphaIsTransparency = alphaIsTransparency;
				changed = true;
			}

			if (s.alphaSource != alphaSource) {
				s.alphaSource = alphaSource;
				changed = true;
			}

			if (s.aniso != aniso) {
				s.aniso = aniso;
				changed = true;
			}

			if (s.filterMode != filterMode) {
				s.filterMode = filterMode;
				changed = true;
			}

			if (s.readable != readable) {
				s.readable = readable;
				changed = true;
			}

			if (s.textureType != type) {
				s.textureType = type;
				changed = true;
			}

			return changed;
		}

		public bool Apply(TextureImporterPlatformSettings s) {
			bool changed = false;

			if (s.format != format) {
				s.format = format;
				changed = true;
			}

			if (s.textureCompression != TextureImporterCompression.CompressedHQ) {
				s.textureCompression = TextureImporterCompression.CompressedHQ;
				changed = true;
			}

			if (!s.overridden) {
				s.overridden = true;
				changed = true;
			}

			return changed;
		}
	};

	void SetTextureImporterSettings(Texture2D t, ImportSettings_t settings) {
		bool changed = false;

		{
			var s = t.GetImporterSettings();
			if (settings.Apply(s)) {
				t.SetImporterSettings(s);
				changed = true;
			}
		}

		{
			var s = t.GetImporterPlatformSettings("Standalone");
			if (settings.Apply(s)) {
				t.SetImporterPlatformSettings(s);
				changed = true;
			}
		}

		if (changed) {
			t.ReimportAsset();
		}
	}

	void CheckTextureSizeAndFormatAndThrow(WorldAtlasMaterialTextures textures, ref int w, ref int h, ref int mm, ImportSettings_t settings, Texture2D channel, string channelName) {
		if (channel == null) {
			ThrowAssetException(textures, channelName + " channel is missing!");
		}
		if (w == -1) {
			w = channel.width;
		} else if (w != channel.width) {
			ThrowAssetException(textures, channelName + " channel width mismatch.");
		}
		if (h == -1) {
			h = channel.height;
		} else if (h != channel.height) {
			ThrowAssetException(textures, channelName + " channel height mismatch.");
		}

		SetTextureImporterSettings(channel, settings);

		if (mm == -1) {
			mm = channel.mipmapCount;
		} else if (mm != channel.mipmapCount) {
			ThrowAssetException(textures, channelName + " channel mipmaps mismatch.");
		}
	}

	void CreateTextureArray(WorldAtlas atlas, ImportSettings_t settings, List<WorldAtlasMaterialTextures> textures, System.Func<WorldAtlasMaterialTextures, Texture2D> getChannel, string channelName) {

		int w = -1;
		int h = -1;
		int mm = -1;

		for (int i = 0; i < textures.Count; ++i) {
			var t = getChannel(textures[i]);
			CheckTextureSizeAndFormatAndThrow(textures[i], ref w, ref h, ref mm, settings, t, channelName);
		}

		var arr = new Texture2DArray(w, h, textures.Count, (TextureFormat)settings.format, true, false);
		arr.Apply(false, true);
		arr.wrapMode = TextureWrapMode.Repeat;

		for (int i = 0; i < textures.Count; ++i) {
			var t = getChannel(textures[i]);
			for (int mipNum = 0; mipNum < t.mipmapCount; ++mipNum) {
				Graphics.CopyTexture(t, 0, mipNum, arr, i, mipNum);
			}
		}

		SaveTextureArray(atlas, arr, channelName);
	}

	void ThrowAssetException(UnityEngine.Object asset, string message) {
		throw new Exception(message + " : " + AssetDatabase.GetAssetPath(asset));
	}

	void RebuildAll() {
		RebuildAtlasData();
		RebuildTextureArray();
	}
}
