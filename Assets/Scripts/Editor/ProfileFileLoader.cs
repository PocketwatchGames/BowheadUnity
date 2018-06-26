// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEditor;

namespace Bowhead.Editor {
	public static class ProfileFileLoader {

		[MenuItem("Bowhead/Load Profiler Data...")]
		static void LoadProfilerData() {
			var path = EditorUtility.OpenFilePanel("Open Profiler Log", "", "");
			if (!string.IsNullOrEmpty(path)) {
				if (path.EndsWith(".data")) {
					path = path.Substring(0, path.Length - ".data".Length);
				}
				UnityEngine.Profiling.Profiler.AddFramesFromFile(path);
			}
		}

	}
}