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

	WorldAtlasData SaveAtlasData(WorldAtlas atlas, WorldAtlasData atlasData) {
		var path = atlas.GetAssetFolderPath();
		path = path + "/" + atlas.name + "_AtlasData.asset";
		var existing = AssetDatabase.LoadAssetAtPath<WorldAtlasData>(path);
		if (existing != null) {
			EditorUtility.CopySerialized(atlasData, existing);
			EditorUtility.SetDirty(existing);
			atlasData = existing;
		} else {
			AssetDatabase.CreateAsset(atlasData, path);
			atlasData = AssetDatabase.LoadAssetAtPath<WorldAtlasData>(path);
		}
		return atlasData;
	}

	WorldAtlasClientData SaveAtlasClientData(WorldAtlas atlas, WorldAtlasClientData clientData) {
		var folder = atlas.GetAssetFolderPath();
		var path = folder + "/Resources/" + atlas.name + "_AtlasClientData.asset";
		var existing = AssetDatabase.LoadAssetAtPath<WorldAtlasClientData>(path);
		if (existing != null) {
			EditorUtility.CopySerialized(clientData, existing);
			EditorUtility.SetDirty(existing);
			clientData = existing;
		} else {
			AssetDatabase.CreateFolder(folder, "Resources");
			AssetDatabase.CreateAsset(clientData, path);
			clientData = AssetDatabase.LoadAssetAtPath<WorldAtlasClientData>(path);
		}
		return clientData;
	}

	void SaveTextureArray(WorldAtlas atlas, Texture2DArray array, string channel) {
		var folder = atlas.GetAssetFolderPath();
		var path = folder + "/TextureArrays/" + atlas.name + "_" + channel + "TextureArray.asset";
		var existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
		if (existing != null) {
			EditorUtility.CopySerialized(array, existing);
			EditorUtility.SetDirty(existing);
		} else {
			AssetDatabase.CreateFolder(folder, "TextureArrays");
			AssetDatabase.CreateAsset(array, path);
		}
	}

	Texture2DArray LoadTextureArray(WorldAtlas atlas, string channel) {
		var path = atlas.GetAssetFolderPath();
		path = path + "/TextureArrays/" + atlas.name + "_" + channel + "TextureArray.asset";
		return AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
	}

	int AddTextureIndex(List<Texture2D> arr, Texture2D t) {
		var idx = arr.FindIndex(0, arr.Count, x => x.GetInstanceID() == t.GetInstanceID());
		if (idx < 0) {
			idx = arr.Count;
			arr.Add(t);
		}
		return idx;
	}

	struct TextureSet {
		public int top;
		public int sides;
		public int bottom;

		public static int[] ToIntArray(List<TextureSet> ts) {
			int[] arr = new int[ts.Count*3];

			for (int i = 0; i < ts.Count; ++i) {
				var ofs = i*3;
				var s = ts[i];
				arr[ofs+0] = s.top;
				arr[ofs+1] = s.sides;
				arr[ofs+2] = s.bottom;
			}

			return arr;
		}

		public static bool Equals(TextureSet a, TextureSet b) {
			return (a.top == b.top) && (a.sides == b.sides) && (a.bottom == b.bottom);
		}
	};

	struct TextureBundle {
		public TextureSet albedo;

		public static bool Equals(TextureBundle a, TextureBundle b) {
			return TextureSet.Equals(a.albedo, b.albedo);
		}
	
		public static int[] OptimizeShaderIndices(TextureBundle[] arr) {
			List<int> indices = new List<int>();
			List<TextureBundle> wkSet = new List<TextureBundle>();

			foreach (var b in arr) {
				var index = AddIndex(wkSet, b);
				indices.Add(index);
			}

			return indices.ToArray();
		}

		public static void CopyChannelTextureSet(TextureBundle[] bundles, List<TextureSet> textureSets, Func<TextureBundle, TextureSet, TextureBundle> set) {
			for (int i = 0; i < bundles.Length; ++i) {
				bundles[i] = set(bundles[i], textureSets[i]);
			}
		}

		static int AddIndex(List<TextureBundle> arr, TextureBundle bundle) {
			var index = arr.FindIndex(0, arr.Count, x => Equals(x, bundle));
			if (index < 0) {
				return AddNew(arr, bundle);
			}
			return index;
		}

		static int AddNew(List<TextureBundle> arr, TextureBundle bundle) {
			var index = arr.Count;
			arr.Add(bundle);
			return index;
		}
	};

	void AddTextureSet(WorldAtlasMaterial atlasMaterial, List<Texture2D> arr, List<TextureSet> indices, WorldAtlasMaterialTextures.TextureSet textureSet, string channelName) {
		if (textureSet.top == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'top' texture!");
		}
		if (textureSet.sides == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'sides' texture!");
		}
		if (textureSet.bottom == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'bottom' texture!");
		}

		var tSet = new TextureSet() {
			top = AddTextureIndex(arr, textureSet.top),
			sides = AddTextureIndex(arr, textureSet.sides),
			bottom = AddTextureIndex(arr, textureSet.bottom)
		};

		indices.Add(tSet);
	}

	void LoadTextureList(WorldAtlas atlas, out List<Texture2D> arr, out List<TextureSet> indices, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> f, string channelName) {
		arr = new List<Texture2D>();
		indices = new List<TextureSet>();

		for (int i = 0; i < atlas.materials.Length; ++i) {
			var m = atlas.materials[i];

			if (m == null) {
				ThrowAssetException(atlas, "Material for terrain type '" + ((EVoxelBlockType)(i + 1)).ToString() + "' is not set!");
			}

			if (m.textures != null) {
				AddTextureSet(m, arr, indices, f(m.textures), channelName);
			} else {
				ThrowAssetException(m, "Missing textures for atlas material.");
			}
		}
	}

	void LoadTextureChannel(WorldAtlas atlas, TextureBundle[] bundles, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> getTexSetChannel, Action<int[]> setTextureSet2ArrayIndex, Func<TextureBundle, TextureSet, TextureBundle> setBundleChannel, string channelName) {
		List<Texture2D> textures;
		List<TextureSet> indices;

		LoadTextureList(atlas, out textures, out indices, getTexSetChannel, channelName);
		setTextureSet2ArrayIndex(TextureSet.ToIntArray(indices));
		TextureBundle.CopyChannelTextureSet(bundles, indices, setBundleChannel);
	}

	void RebuildAtlasData() {
		foreach (var obj in serializedObject.targetObjects) {
			var atlas = (WorldAtlas)obj;
			var atlasData = CreateInstance<WorldAtlasData>();
			var atlasClientData = CreateInstance<WorldAtlasClientData>();

			LoadTerrainTextures(atlas, atlasClientData);

			var bundles = new TextureBundle[atlas.materials.Length];
			LoadTextureChannel(atlas, bundles, (x) => x.albedo, (x) => atlasClientData.albedo.textureSet2ArrayIndex = x, (b, s) => { b.albedo = s; return b; }, "Albedo");

			atlasClientData.block2TextureSet = TextureBundle.OptimizeShaderIndices(bundles);
			atlasClientData.renderMaterials = atlas.renderMaterials;

			atlasClientData = SaveAtlasClientData(atlas, atlasClientData);

			atlasData.atlasClientData = new WorldAtlasClientData_WRef();
			atlasData.atlasClientData.AssignFromAsset(atlasClientData);
			SaveAtlasData(atlas, atlasData);
		}
	}

	void LoadTextureArrayChannel(WorldAtlas atlas, WorldAtlasClientData clientData, Action<WorldAtlasClientData, Texture2DArray> set, string channelName) {
		var arr = LoadTextureArray(atlas, channelName);
		if (arr == null) {
			RebuildTextureArray();
			arr = LoadTextureArray(atlas, channelName);
			if (arr == null) {
				ThrowAssetException(atlas, "Unable to load " + channelName + " texture array!");
			}
		}
		set(clientData, arr);
	}

	void LoadTerrainTextures(WorldAtlas atlas, WorldAtlasClientData clientData) {
		LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.albedo.textureArray = arr, "Albedo");
	}

	void CreateColorChannelTextureArray(WorldAtlas atlas, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> f, string channelName) {
		List<Texture2D> textures;
		List<TextureSet> indices;

		LoadTextureList(atlas, out textures, out indices, f, channelName);

		if (textures.Count < 1) {
			throw new Exception("No textures defined in atlas!");
		}

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

		CreateTextureArray(atlas, settings, textures, channelName);
	}

	void RebuildTextureArray() {
		foreach (var obj in serializedObject.targetObjects) {
			var atlas = (WorldAtlas)obj;

			CreateColorChannelTextureArray(atlas, x => x.albedo, "Albedo");
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

	void CheckTextureSizeAndFormatAndThrow(ref int w, ref int h, ref int mm, ImportSettings_t settings, Texture2D channel, string channelName) {
		if (w == -1) {
			w = channel.width;
		} else if (w != channel.width) {
			ThrowAssetException(channel, channelName + " channel width mismatch.");
		}
		if (h == -1) {
			h = channel.height;
		} else if (h != channel.height) {
			ThrowAssetException(channel, channelName + " channel height mismatch.");
		}

		SetTextureImporterSettings(channel, settings);

		if (mm == -1) {
			mm = channel.mipmapCount;
		} else if (mm != channel.mipmapCount) {
			ThrowAssetException(channel, channelName + " channel mipmaps mismatch.");
		}
	}

	void CreateTextureArray(WorldAtlas atlas, ImportSettings_t settings, List<Texture2D> textures, string channelName) {

		int w = -1;
		int h = -1;
		int mm = -1;

		for (int i = 0; i < textures.Count; ++i) {
			CheckTextureSizeAndFormatAndThrow(ref w, ref h, ref mm, settings, textures[i], channelName);
		}

		var arr = new Texture2DArray(w, h, textures.Count, (TextureFormat)settings.format, true, false);
		arr.Apply(false, true);
		arr.wrapMode = TextureWrapMode.Repeat;

		for (int i = 0; i < textures.Count; ++i) {
			var t = textures[i];
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
