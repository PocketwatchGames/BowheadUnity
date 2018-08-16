// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldAtlas))]
public class WorldAtlasEditor : Editor {
	const TextureImporterFormat RHO_TEXTURE_FORMAT = TextureImporterFormat.DXT5;
	const TextureImporterFormat COLOR_TEXTURE_FORMAT = TextureImporterFormat.DXT1;
	const TextureImporterFormat NORMALS_TEXTURE_FORMAT = TextureImporterFormat.DXT5;

	SerializedProperty _materials;
	SerializedProperty _renderMaterials;
	bool _materialsExpanded;

	void OnEnable() {
		_materials = serializedObject.FindProperty("materials");
		_renderMaterials = serializedObject.FindProperty("renderMaterials");
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();

		EditorGUILayout.BeginVertical();

		EditorGUILayout.PropertyField(_renderMaterials, true);

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
			Utils.CreateAssetFolder(folder, "Resources");
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
			Utils.CreateAssetFolder(folder, "TextureArrays");
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
				arr[ofs+0] = s.bottom;
				arr[ofs+1] = s.sides;
				arr[ofs+2] = s.top;
			}

			return arr;
		}

		public static bool Equals(TextureSet a, TextureSet b) {
			return (a.top == b.top) && (a.sides == b.sides) && (a.bottom == b.bottom);
		}
	};

	struct TextureBundle {
		public TextureSet albedo;
		public TextureSet normals;
		//public TextureSet roughness;
		//public TextureSet ao;
		//public TextureSet height;
		public TextureSet rho;

		public static bool Equals(TextureBundle a, TextureBundle b) {
			return TextureSet.Equals(a.albedo, b.albedo) &&
				TextureSet.Equals(a.normals, b.normals) &&
				//TextureSet.Equals(a.roughness, b.roughness) &&
				//TextureSet.Equals(a.ao, b.ao) &&
				//TextureSet.Equals(a.height, b.height) &&
				TextureSet.Equals(a.rho, b.rho);
		}
	
		public static int[] OptimizeShaderIndices(ref TextureBundle[] arr) {
			List<int> indices = new List<int>();
			List<TextureBundle> wkSet = new List<TextureBundle>();

			foreach (var b in arr) {
				var index = AddIndex(wkSet, b);
				indices.Add(index);
			}

			arr = wkSet.ToArray();
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

	void CheckThrowTextureSet(WorldAtlasMaterial atlasMaterial, WorldAtlasMaterialTextures.TextureSet textureSet, string channelName) {
		if (textureSet.top == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'top' texture!");
		}
		if (textureSet.sides == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'sides' texture!");
		}
		if (textureSet.bottom == null) {
			ThrowAssetException(atlasMaterial, "Texture set for " + channelName + " is missing 'bottom' texture!");
		}
	}

	void AddTextureSet(WorldAtlasMaterial atlasMaterial, List<Texture2D> arr, List<TextureSet> indices, WorldAtlasMaterialTextures.TextureSet textureSet, string channelName) {
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

	class RHOTextureJoin {
		public int roughnessIndex;
		public int heightIndex;
		public int aoIndex;
	};

	class RHOTextureTable {
		public List<RHOTextureJoin> textures = new List<RHOTextureJoin>();
		public List<Texture2D> roughness = new List<Texture2D>();
		public List<Texture2D> height = new List<Texture2D>();
		public List<Texture2D> ao = new List<Texture2D> ();
		public List<TextureSet> indices = new List<TextureSet>();

		public int AddTextureJoin(Texture2D r, Texture2D h, Texture2D o) {
			var ri = roughness.FindIndex(0, roughness.Count, x => x.GetInstanceID() == r.GetInstanceID());
			var hi = height.FindIndex(0, height.Count, x => x.GetInstanceID() == h.GetInstanceID());
			var oi = ao.FindIndex(0, ao.Count, x => x.GetInstanceID() == o.GetInstanceID());
			
			var idx = textures.FindIndex(0, textures.Count, x => (x.roughnessIndex == ri) && (x.heightIndex == hi) && (x.aoIndex == oi));
			if (idx < 0) {
				if (ri < 0) {
					ri = roughness.Count;
					roughness.Add(r);
				}
				if (hi < 0) {
					hi = height.Count;
					height.Add(h);
				}
				if (oi < 0) {
					oi = ao.Count;
					ao.Add(o);
				}

				idx = textures.Count;
				textures.Add(new RHOTextureJoin() {
					roughnessIndex = ri,
					heightIndex = hi,
					aoIndex = oi
				});

			}
			return idx;
		}
	};

	struct RHOTextureSet {
		public WorldAtlasMaterialTextures.TextureSet roughness;
		public WorldAtlasMaterialTextures.TextureSet height;
		public WorldAtlasMaterialTextures.TextureSet ao;
	};

	void AddTextureSet(WorldAtlasMaterial atlasMaterial, RHOTextureTable table, RHOTextureSet textureSet) {
		CheckThrowTextureSet(atlasMaterial, textureSet.roughness, "Roughness");
		CheckThrowTextureSet(atlasMaterial, textureSet.ao, "AO");
		CheckThrowTextureSet(atlasMaterial, textureSet.height, "Height");

		var tSet = new TextureSet() {
			top = table.AddTextureJoin(textureSet.roughness.top, textureSet.height.top, textureSet.ao.top),
			sides = table.AddTextureJoin(textureSet.roughness.sides, textureSet.height.sides, textureSet.ao.sides),
			bottom = table.AddTextureJoin(textureSet.roughness.bottom, textureSet.height.bottom, textureSet.ao.bottom)
		};

		table.indices.Add(tSet);
	}

	RHOTextureTable LoadRHOTextureTable(WorldAtlas atlas) {
		var table = new RHOTextureTable();

		for (int i = 0; i < atlas.materials.Length; ++i) {
			var m = atlas.materials[i];

			if (m == null) {
				ThrowAssetException(atlas, "Material for terrain type '" + ((EVoxelBlockType)(i + 1)).ToString() + "' is not set!");
			}

			if (m.textures != null) {
				var set = new RHOTextureSet() {
					roughness = m.textures.roughness,
					height = m.textures.height,
					ao = m.textures.ao
				};
				AddTextureSet(m, table, set);
			} else {
				ThrowAssetException(m, "Missing textures for atlas material.");
			}
		}

		int w = -1;
		int h = -1;
		int mm = -1;

		var settings = new ImportSettings_t() {
			alphaSource = TextureImporterAlphaSource.FromGrayScale,
			alphaIsTransparency = false,
			aniso = 16,
			filterMode = FilterMode.Trilinear,
			mipmap = false,
			readable = true,
			type = TextureImporterType.Default,
			format = TextureImporterFormat.Alpha8
		};

		foreach (var t in table.roughness) {
			CheckTextureSizeAndFormatAndThrow(ref w, ref h, ref mm, settings, t, "Roughness");
		}

		foreach (var t in table.height) {
			CheckTextureSizeAndFormatAndThrow(ref w, ref h, ref mm, settings, t, "Height");
		}

		foreach (var t in table.ao) {
			CheckTextureSizeAndFormatAndThrow(ref w, ref h, ref mm, settings, t, "AO");
		}

		return table;
	}


	void LoadTextureChannel(WorldAtlas atlas, TextureBundle[] bundles, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> getTexSetChannel, Func<TextureBundle, TextureSet, TextureBundle> setBundleChannel, string channelName) {
		List<Texture2D> textures;
		List<TextureSet> indices;

		LoadTextureList(atlas, out textures, out indices, getTexSetChannel, channelName);
		TextureBundle.CopyChannelTextureSet(bundles, indices, setBundleChannel);
	}

	void LoadRHOTextureChannel(WorldAtlas atlas, TextureBundle[] bundles, WorldAtlasClientData clientData) {
		var table = LoadRHOTextureTable(atlas);
		TextureBundle.CopyChannelTextureSet(bundles, table.indices, (b, s) => { b.rho = s; return b; });
	}

	void SetChannelTextureIndices(int[] block2TextureSet, TextureBundle[] bundles, Func<TextureBundle, TextureSet> getTexSet, Action<int[]> setTextureSet2ArrayIndex) {
		var indices = new List<TextureSet>();
		foreach (var i in block2TextureSet) {
			while (i >= indices.Count) {
				indices.Add(default(TextureSet));
			}
			indices[i] = getTexSet(bundles[i]);
		}

		setTextureSet2ArrayIndex(TextureSet.ToIntArray(indices));
	}

	void RebuildAtlasData() {
		foreach (var obj in serializedObject.targetObjects) {
			var atlas = (WorldAtlas)obj;
			var atlasData = CreateInstance<WorldAtlasData>();
			var atlasClientData = CreateInstance<WorldAtlasClientData>();

			LoadTerrainTextures(atlas, atlasClientData);

			var bundles = new TextureBundle[atlas.materials.Length];
			LoadTextureChannel(atlas, bundles, (x) => x.albedo, (b, s) => { b.albedo = s; return b; }, "Albedo");
			LoadTextureChannel(atlas, bundles, (x) => x.normals, (b, s) => { b.normals = s; return b; }, "Normals");
			LoadRHOTextureChannel(atlas, bundles, atlasClientData);

			atlasClientData.block2TextureSet = TextureBundle.OptimizeShaderIndices(ref bundles);

			SetChannelTextureIndices(atlasClientData.block2TextureSet, bundles, (x) => x.albedo, (x) => atlasClientData.albedo.textureSet2ArrayIndex = x);
			SetChannelTextureIndices(atlasClientData.block2TextureSet, bundles, (x) => x.normals, (x) => atlasClientData.normals.textureSet2ArrayIndex = x);
			SetChannelTextureIndices(atlasClientData.block2TextureSet, bundles, (x) => x.rho, (x) => atlasClientData.rho.textureSet2ArrayIndex = x);
						
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
		LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.normals.textureArray = arr, "Normals");
		//LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.roughness.textureArray = arr, "Roughness");
		//LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.ao.textureArray = arr, "AO");
		//LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.height.textureArray = arr, "Height");
		LoadTextureArrayChannel(atlas, clientData, (cd, arr) => cd.rho.textureArray = arr, "RHO");
	}

	void CreateColorChannelTextureArray(WorldAtlas atlas, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> f, string channelName) {
		List<Texture2D> textures;
		List<TextureSet> indices;

		LoadTextureList(atlas, out textures, out indices, f, channelName);

		if (textures.Count < 1) {
			throw new Exception("No textures defined in atlas!");
		}

		var settings = new ImportSettings_t() {
			alphaSource = TextureImporterAlphaSource.None,
			alphaIsTransparency = false,
			aniso = 16,
			filterMode = FilterMode.Trilinear,
			mipmap = true,
			readable = true,
			type = TextureImporterType.Default,
			format = COLOR_TEXTURE_FORMAT
		};

		CreateTextureArray(atlas, settings, textures, channelName);
	}

	void CreateNormalsChannelTextureArray(WorldAtlas atlas, Func<WorldAtlasMaterialTextures, WorldAtlasMaterialTextures.TextureSet> f, string channelName) {
		List<Texture2D> textures;
		List<TextureSet> indices;

		LoadTextureList(atlas, out textures, out indices, f, channelName);

		if (textures.Count < 1) {
			throw new Exception("No textures defined in atlas!");
		}

		var settings = new ImportSettings_t() {
			alphaSource = TextureImporterAlphaSource.FromInput,
			alphaIsTransparency = false,
			aniso = 16,
			filterMode = FilterMode.Trilinear,
			mipmap = true,
			readable = true,
			type = TextureImporterType.NormalMap,
			format = NORMALS_TEXTURE_FORMAT
		};

		CreateTextureArray(atlas, settings, textures, channelName);
	}

	Texture2D JoinTextures(RHOTextureTable table, RHOTextureJoin join, TextureImporterFormat format) {
		var t = new Texture2D(table.roughness[0].width, table.roughness[0].height, TextureFormat.ARGB32, true);
		var pixels = t.GetPixels32();

		var r = table.roughness[join.roughnessIndex];
		var h = table.height[join.heightIndex];
		var ao = table.height[join.aoIndex];

		var rPixels = r.GetPixels32();
		var hPixels = h.GetPixels32();
		var aoPixels = ao.GetPixels32();

		var numPixels = pixels.Length;
		for (int i = 0; i < numPixels; ++i) {
			var p = new Color32(rPixels[i].a, aoPixels[i].a, 0, hPixels[i].a);
			pixels[i] = p;
		}

		t.SetPixels32(pixels);
		t.Apply();

		if ((format == TextureImporterFormat.DXT1) || (format == TextureImporterFormat.DXT5)) {
			EditorUtility.CompressTexture(t, (TextureFormat)format, TextureCompressionQuality.Best);
			t.Apply();
		}

		return t;
	}

	void CreateRHOTextureArray(WorldAtlas atlas) {
		var table = LoadRHOTextureTable(atlas); // this sets the incoming texture formats to Alpha8

		if (table.textures.Count < 1) {
			throw new Exception("No textures defined in atlas!");
		}

		var textureList = new List<Texture2D>(table.textures.Count);

		foreach (var t in table.textures) {
			var joined = JoinTextures(table, t, RHO_TEXTURE_FORMAT);
			textureList.Add(joined);
		}

		var settings = new ImportSettings_t() {
			alphaSource = TextureImporterAlphaSource.FromInput,
			alphaIsTransparency = false,
			aniso = 16,
			filterMode = FilterMode.Trilinear,
			mipmap = true,
			readable = true,
			type = TextureImporterType.Default,
			format = RHO_TEXTURE_FORMAT
		};

		UncheckedCreateTextureArray(atlas, settings, textureList, "RHO");

		foreach (var t in textureList) {
			DestroyImmediate(t);
		}
	}

	void RebuildTextureArray() {
		foreach (var obj in serializedObject.targetObjects) {
			var atlas = (WorldAtlas)obj;

			CreateColorChannelTextureArray(atlas, x => x.albedo, "Albedo");
			CreateNormalsChannelTextureArray(atlas, x => x.normals, "Normals");
			CreateRHOTextureArray(atlas);
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

			if ((format == TextureImporterFormat.DXT1) || (format == TextureImporterFormat.DXT5)) {
				if (s.textureCompression != TextureImporterCompression.CompressedHQ) {
					s.textureCompression = TextureImporterCompression.CompressedHQ;
					changed = true;
				}
			} else if (s.textureCompression != TextureImporterCompression.Uncompressed) {
				s.textureCompression = TextureImporterCompression.Uncompressed;
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

		UncheckedCreateTextureArray(atlas, settings, textures, channelName);
	}

	void UncheckedCreateTextureArray(WorldAtlas atlas, ImportSettings_t settings, List<Texture2D> textures, string channelName) {

		var arr = new Texture2DArray(textures[0].width, textures[0].height, textures.Count, (TextureFormat)settings.format, true, false);
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
