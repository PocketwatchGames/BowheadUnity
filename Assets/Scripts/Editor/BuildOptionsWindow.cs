// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using UnityEngine.Assertions;
using System.Collections.Generic;

[InitializeOnLoad]
public class BuildOptionsWindow : EditorWindow {

	static string currentScriptFlags;

	static void AddScriptFlags() {
		AddFlag("Shipping Build", "Remove code not meant for public consumption", "SHIP", false);
		AddFlag("Enable Profiling", "Enable Profiling", "PROFILING", false);
		AddFlag("Steam Integration", "Use Steam API", "STEAM_API", false);
		AddFlag("Voxel Debug Mesh", "Generates debug voxel mesh", "DEBUG_VOXEL_MESH", false);
		AddFlag("Streaming Mode", "Ugly but fast!", "DEV_STREAMING", false);
	}

	static BuildOptionsWindow() {
		AddScriptFlags();
	}

	BuildOptionsWindow() {
		titleContent.text = "Build Options";
	}

	static void AddFlag(string name, string description, string value, bool initial) {
		flags.Add(new Flag(name, description, value, initial, false));
	}

	static void AddHiddenFlag(string name, string description, string value, bool initial) {
		flags.Add(new Flag(name, description, value, initial, true));
	}

	void OnEnabled() {
		LoadPrefs();
	}

	public static void LoadPrefs() {
		if (!prefsLoaded) {
			prefsLoaded = true;
			
			string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
			bool newSymbol = false;

			foreach (var flag in flags) {
				bool b = EditorPrefs.GetBool("scriptflags_" + flag.value, false);
				if (!b) {
					if (flag.initial) {
						symbols = AddDefine(symbols, flag.value);
						newSymbol = true;
					}
					EditorPrefs.SetBool("scriptflags_" + flag.value, true);
				}
			}

			if (newSymbol) {
				PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, symbols);
			}
		}
	}

	public static string DefaultFlags {
		get {
			string s = "";
			foreach (var flag in flags) {
				if (flag.initial) {
					if (string.IsNullOrEmpty(s)) {
						s = flag.value;
					} else {
						s += ";" + flag.value;
					}
				}
			}
			return s;
		}
	}

	[MenuItem("Bowhead/Editor Settings...")]
	static void MenuBuildSettings() {
		currentScriptFlags = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
		EditorWindow.GetWindow<BuildOptionsWindow>();
	}

	public static string OnGUI_ScriptFlags(string symbols, bool forBuild) {
		EditorGUILayout.Space();
		EditorGUILayout.BeginHorizontal();
		GUILayoutHelpers.CenterLabel("Script Options");
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();

		string newSymbols = string.Empty;

		foreach (var flag in flags) {
			if (!(forBuild && flag.hiddenInBuild)) {
				var b = EditorGUILayout.Toggle(new GUIContent(flag.name, flag.description), IsDefined(symbols, flag.value));
				if (b) {
					if (newSymbols.Length > 0) {
						newSymbols += ';' + flag.value;
					} else {
						newSymbols = flag.value;
					}
				}
			}
		}

		return newSymbols;
	}

	public static bool IsDefined(string symbols, string str) {
		var defines = symbols.Split(';');
		var check = str.Split(';');

		foreach (var x in check) {
			bool found = false;
			foreach (var s in defines) {
				if (s == x) {
					found = true;
				}
			}
			if (!found) {
				return false;
			}
		}

		return true;
	}
	
	public static string AddDefine(string symbols, string flag) {
		if (symbols.Length > 0) {
			symbols += ';' + flag;
		} else {
			symbols = flag;
		}
		return symbols;
	}

	public static string RemoveDefine(string symbols, string flag) {
		if (symbols.Contains(";" + flag)) {
			symbols = symbols.Replace(";" + flag, string.Empty);
		} else if (symbols.Contains(flag)) {
			symbols = symbols.Replace(flag, string.Empty);
		}
		return symbols;
	}

	void OnGUI() {
		EditorGUILayout.BeginVertical();
		currentScriptFlags = OnGUI_ScriptFlags(currentScriptFlags ?? string.Empty, false);
		if (currentScriptFlags != PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)) {
			GUI.enabled = true;
		} else {
			GUI.enabled = false;
		}

		if (GUILayout.Button("Apply Changes")) {
			PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, currentScriptFlags);
        }

		GUI.enabled = true;

		EditorGUILayout.EndVertical();
	}

	class Flag {
		public Flag(string name, string description, string value, bool initial, bool hiddenInBuild) {
			this.name = name;
			this.value = value;
			this.initial = initial;
			this.description = description;
			this.hiddenInBuild = hiddenInBuild;
		}

		public string name;
		public string value;
		public string description;
		public bool initial;
		public readonly bool hiddenInBuild;
	}

	static List<Flag> flags = new List<Flag>();
	static bool prefsLoaded = false;
}
