// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

public static class EditorOnlyTextureUtils {

	[MenuItem("Assets/Selected Textures/ReadWrite/Enable")]
	static void EnableTextureReadWrite() {
		Utils.ForeachSelectedAsset<Texture2D>((t) => {
			var settings = t.GetImporterSettings();
			if (!settings.readable) {
				settings.readable = true;
				t.SetImporterSettings(settings);
				t.ReimportAsset();
			}
		});
	}

	[MenuItem("Assets/Selected Textures/ReadWrite/Disable")]
	static void DisableTextureReadWrite() {
		Utils.ForeachSelectedAsset<Texture2D>((t) => {
			var settings = t.GetImporterSettings();
			if (settings.readable) {
				settings.readable = false;
				t.SetImporterSettings(settings);
				t.ReimportAsset();
			}
		});
	}
}
