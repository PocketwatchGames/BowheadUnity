// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

public static class EditorOnlyTextureUtils {

	[MenuItem("Tools/Selected Textures/ReadWrite/Enable")]
	static void EnableTextureReadWrite() {
		bool didAny = false;
		Utils.ForeachSelectedAsset<Texture2D>((t) => {
			var settings = t.GetImportSettings();
			if (!settings.readable) {
				settings.readable = true;
				t.SetImportSettings(settings);
				didAny = true;
			}
		});
		if (didAny) {
			AssetDatabase.Refresh();
		}
	}

	[MenuItem("Tools/Selected Textures/ReadWrite/Disable")]
	static void DisableTextureReadWrite() {
		bool didAny = false;
		Utils.ForeachSelectedAsset<Texture2D>((t) => {
			var settings = t.GetImportSettings();
			if (settings.readable) {
				settings.readable = false;
				t.SetImportSettings(settings);
				didAny = true;
			}
		});
		if (didAny) {
			AssetDatabase.Refresh();
		}
	}
}
