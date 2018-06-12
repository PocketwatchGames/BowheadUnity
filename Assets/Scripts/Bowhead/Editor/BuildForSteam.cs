// Copyright (c) 2018 Pocketwatch Games LLC.
#define PERFORCE

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

namespace Bowhead {
	public class SteamPublishWindow : EditorWindow {
		const int STEAM_APP_ID = 346930;
		const string LABEL = "Alpha (Severed Head)";

		static bool prefsLoaded;
		static string scriptFlags;
		static string steamLogin;
		static string steamPassword;
		static string branch;

#if PERFORCE
		static string p4user;
		static string p4pass;
		static string p4client;
		static string p4server;
		static string p4path;
#endif

		static readonly string[] STATIC_LEVELS = {
			"Assets/Scenes/Entry.unity",
			"Assets/Scenes/MainMenu/MainMenu.unity",
			"Assets/Scenes/Loading.unity",
			"Assets/Scenes/MissionPrologue/MissionPrologue.unity"
		};

		static readonly string[] BRANCHES = {
			"internaltest",
			"developer"
		};

		enum EDeploymentType {
			ClientGame,
			BackendServer
		}

		static readonly Platform[] PLATFORMS = {
			new Platform("MacOSX", new[] { 346930 }, new [] { 346932 }, new [] { BuildTarget.StandaloneOSX }, EDeploymentType.ClientGame),
            new Platform("Windows", new[] { 346930 }, new [] { 346931 }, new [] { BuildTarget.StandaloneWindows }, EDeploymentType.ClientGame),
			new Platform("Backend Game Server", new[] { 447460 }, new [] { 447462 }, new [] { BuildTarget.StandaloneLinux }, EDeploymentType.BackendServer)
		};

		class Platform {
			public readonly string name;
			public readonly BuildTarget[] targets;
			public readonly List<int> appIds;
			public readonly List<int> depotIds;
			public readonly EDeploymentType deployment;
			public bool shouldBuild;

			public Platform(string name, int[] appIds, int[] depotIds, BuildTarget[] targets, EDeploymentType deployment) {
				this.name = name;
				this.appIds = new List<int>(appIds);
				this.depotIds = new List<int>(depotIds);
				this.targets = targets;
				this.deployment = deployment;
			}

			public static string GetAppPath(BuildTarget target) {
				switch (target) {
					case BuildTarget.StandaloneOSX:
						return GetTargetPath(target) + "/Bowhead.app";
					case BuildTarget.StandaloneWindows: goto case BuildTarget.StandaloneWindows64;
					case BuildTarget.StandaloneWindows64:
						return GetTargetPath(target) + "/Bowhead.exe";
					case BuildTarget.StandaloneLinux:
						return GetTargetPath(target) + "/Bowhead.x86";
				}

				throw new System.Exception("Invalid platform");
			}

			public static string GetTargetPath(BuildTarget target) {
				switch (target) {
					case BuildTarget.StandaloneOSX:
						return Utils.projectRootDirectory + "/Build/Steam/OSX";
					case BuildTarget.StandaloneWindows:
						return Utils.projectRootDirectory + "/Build/Steam/Windows/x86";
					case BuildTarget.StandaloneWindows64:
						return Utils.projectRootDirectory + "/Build/Steam/Windows/x64";
					case BuildTarget.StandaloneLinux:
						return Utils.projectRootDirectory + "/Build/Steam/Linux/x86";
				}

				throw new System.Exception("Invalid platform");
			}

			public static string GetAssemblyPath(BuildTarget target) {
				switch (target) {
					case BuildTarget.StandaloneOSX:
						return Utils.projectRootDirectory + "/Build/Steam/OSX/Bowhead.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll";
					case BuildTarget.StandaloneWindows:
						return Utils.projectRootDirectory + "/Build/Steam/Windows/x86/Deadhold_Data/Managed/Assembly-CSharp.dll";
					case BuildTarget.StandaloneWindows64:
						return Utils.projectRootDirectory + "/Build/Steam/Windows/x64/Deadhold_Data/Managed/Assembly-CSharp.dll";
					case BuildTarget.StandaloneLinux:
						return Utils.projectRootDirectory + "/Build/Steam/Linux/x86/Deadhold_Data/Managed/Assembly-CSharp.dll";
				}

				throw new System.Exception("Invalid platform");
			}
		}

		static void LoadPrefs() {
			if (!prefsLoaded) {
				prefsLoaded = true;

				steamLogin = EditorPrefs.GetString("steamlogin", "<Enter Steam Login>");
				steamPassword = EditorPrefs.GetString("steampass", "");

				scriptFlags = EditorPrefs.GetString("steamscriptflags", BuildOptionsWindow.DefaultFlags);
				branch = EditorPrefs.GetString("steambranch", BRANCHES[0]);

#if PERFORCE
				p4path = EditorPrefs.GetString("p4path", "C:\\Program Files\\Perforce");
				p4server = EditorPrefs.GetString("p4server", string.Empty);
				p4user = EditorPrefs.GetString("p4user", string.Empty);
				p4pass = EditorPrefs.GetString("p4pass", string.Empty);
				p4client = EditorPrefs.GetString("p4client", string.Empty);
#endif

				foreach (var p in PLATFORMS) {
					p.shouldBuild = EditorPrefs.GetBool("steambuild_" + p.name, true);
				}
			}
		}

		void OnEnable() {
			LoadPrefs();
		}

		SteamPublishWindow() {
			titleContent.text = "Publish Steam";
		}

		[MenuItem("Bowhead/Publish To Steam...")]
		static void Menu() {
			EditorWindow.GetWindow<SteamPublishWindow>();
		}

		void OnGUI() {
			EditorGUILayout.BeginVertical();

			GUILayoutHelpers.HorzLine();

			{
				var s = BuildOptionsWindow.OnGUI_ScriptFlags(scriptFlags, true);
				if (s != scriptFlags) {
					EditorPrefs.SetString("steamscriptflags", s);
					scriptFlags = s;
				}
			}

			GUILayoutHelpers.HorzLine();

			OnGUI_BuildPlatforms();

			GUILayoutHelpers.HorzLine();

#if PERFORCE
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			GUILayoutHelpers.CenterLabel("Perforce");
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			{
				var s = EditorGUILayout.TextField("P4 Directory", p4path);
				if (s != p4path) {
					EditorPrefs.SetString("p4path", s);
					p4path = s;
				}

				s = EditorGUILayout.TextField("P4 Server", p4server);
				if (s != p4server) {
					EditorPrefs.SetString("p4server", s);
					p4server = s;
				}

				s = EditorGUILayout.TextField("P4 User", p4user);
				if (s != p4user) {
					EditorPrefs.SetString("p4user", s);
					p4user = s;
				}

				s = EditorGUILayout.PasswordField("P4 Password", p4pass);
				if (s != p4pass) {
					EditorPrefs.SetString("p4pass", s);
					p4pass = s;
				}

				s = EditorGUILayout.TextField("P4 Workspace", p4client);
				if (s != p4client) {
					EditorPrefs.SetString("p4client", s);
					p4client = s;
				}
			}

			GUILayoutHelpers.HorzLine();
#endif

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			GUILayoutHelpers.CenterLabel("Steam");
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			{
				var i = 0;
				while ((branch != BRANCHES[i]) && (i < BRANCHES.Length)) {
					++i;
				}

				if (i >= BRANCHES.Length) {
					i = 0;
				}

				var s = EditorGUILayout.Popup("Set Live", i, BRANCHES);
				if (s != i) {
					branch = BRANCHES[s];
					EditorPrefs.SetString("steambranch", branch);
				}
			}

			{
				var s = EditorGUILayout.TextField("Steam Login", steamLogin);
				if (s != steamLogin) {
					EditorPrefs.SetString("steamlogin", s);
					steamLogin = s;
				}
			}

			{
				var s = EditorGUILayout.PasswordField("Steam Password", steamPassword);
				if (s != steamPassword) {
					EditorPrefs.SetString("steampass", s);
					steamPassword = s;
				}
			}

			GUILayoutHelpers.HorzLine();

			bool build = false;

			if (GUILayout.Button("Build (testing only!)...")) {
				build = true;
			}

			bool publish = false;

			if (GUILayout.Button("Build and Publish...")) {
				publish = true;
				build = true;
			}

			//if (GUILayout.Button("Retry Publish (careful!)...")) {
			//	publish = true;
			//}

			EditorGUILayout.EndVertical();

			string oldScriptFlags = null;

			if (build || publish) {
#if PERFORCE
				if (string.IsNullOrEmpty(p4path) || string.IsNullOrEmpty(p4server) || string.IsNullOrEmpty(p4user) || string.IsNullOrEmpty(p4pass) || string.IsNullOrEmpty(p4client)) {
					EditorUtility.DisplayDialog("Error", "Please fill in the perforce fields to build for Steam.", "OK");
					return;
				}
#endif
				oldScriptFlags = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
			}

			if (build && publish) {
				var progress = new EditorProgressBar("Building...", 2);
				var r = CheckForPrereqs() && Build();
				if (r) {
					progress.Step("Publishing...");
					r = Publish();
					progress.Step(null);
				} else {
					progress.Close();
				}

				if (r) {
					EditorUtility.DisplayDialog("Success", "Build and publish completed successfully.", "OK");
				} else {
					EditorUtility.DisplayDialog("Error", "Build failed.", "OK");
				}
			} else if (build) {
				var progress = new EditorProgressBar("Building...", 1);
				var r = CheckForPrereqs() && Build();
				progress.Step(null);
				if (r) {
					EditorUtility.DisplayDialog("Success", "Build completed successfully.", "OK");
				} else {
					EditorUtility.DisplayDialog("Error", "Build failed.", "OK");
				}
			} else if (publish) {
				var progress = new EditorProgressBar("Publishing...", 1);
				var r = CheckForPrereqs();
				if (r) {
					progress.Step("Publishing...");
					r = Publish();
					progress.Step(null);
				} else {
					progress.Close();
				}

				if (r) {
					EditorUtility.DisplayDialog("Success", "Publish completed successfully.", "OK");
				} else {
					EditorUtility.DisplayDialog("Error", "Publish failed.", "OK");
				}
			}

			if (build || publish) {
				if (oldScriptFlags != PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)) {
					PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, oldScriptFlags);
				}
			}
		}

		void OnGUI_BuildPlatforms() {
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			GUILayoutHelpers.CenterLabel("Platforms");
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Space();

			foreach (var p in PLATFORMS) {
				var b = EditorGUILayout.Toggle(p.name, p.shouldBuild);
				if (b != p.shouldBuild) {
					p.shouldBuild = b;
					EditorPrefs.SetBool("steambuild_" + p.name, b);
				}
			}
		}

		bool CheckForPrereqs() {
#if PERFORCE
			if (!CheckForP4()) {
				EditorUtility.DisplayDialog("Error", "Missing p4.exe", "OK");
				return false;
			}
#else
			if (!CheckForSVN()) {
				EditorUtility.DisplayDialog("Error", "Missing svn.exe", "OK");
				return false;
			}
#endif
			return true;
		}

		bool Build() {
			bool builtOne = false;

			var buildId = GetBuildID();
			if (buildId == null) {
				return false;
			}

			if (!WriteBuildInfo(buildId)) {
				return false;
			}

			var numThatWillBuild = 0;
			foreach (var platform in PLATFORMS) {
				if (platform.shouldBuild) {
					++numThatWillBuild;
				}
			}

			if (numThatWillBuild < 1) {
				EditorUtility.DisplayDialog("Error", "There are platforms selected.", "OK");
				return false;
			}

			var progress = new EditorProgressBar(null, numThatWillBuild);

			Platform toSkip = null;

			foreach (var platform in PLATFORMS) {
				if (platform.shouldBuild) {
					foreach (var target in platform.targets) {
						if (EditorUserBuildSettings.activeBuildTarget == target) {
							toSkip = platform;
							break;
						}
					}
				}
				if (toSkip != null) {
					progress.description = platform.name;
					builtOne = true;
					if (!BuildPlatform(platform)) {
#if PERFORCE
						P4RevertFile(buildInfoPath);
#else
						SVNRevertFile(buildInfoPath);
#endif
						progress.Close();
						return false;
					}
					progress.Step(null);
					break;
				}
			}

			foreach (var platform in PLATFORMS) {
				if (platform.shouldBuild && (platform != toSkip)) {
					progress.description = platform.name;
					builtOne = true;
					if (!BuildPlatform(platform)) {
#if PERFORCE
						P4RevertFile(buildInfoPath);
#else
						SVNRevertFile(buildInfoPath);
#endif
						progress.Close();
						return false;
					}
					progress.Step(null);
				}
			}

#if PERFORCE
			P4RevertFile(buildInfoPath);
#else
			SVNRevertFile(buildInfoPath);
#endif
			return builtOne;
		}
		
		static string buildInfoPath {
			get {
				return Utils.projectRootDirectory + "/Assets/Scripts/Bowhead/BuildInfo.cs";
			}
		}

		static bool WriteBuildInfo(string id) {
#if PERFORCE
			if (!P4EditFile(buildInfoPath)) {
				EditorUtility.DisplayDialog("Error", "Unable to open BuildInfo for edit.", "OK");
				return false;
			}
#endif
			try {
				using (var file = new StreamWriter(buildInfoPath, false, System.Text.Encoding.ASCII)) {
					file.WriteLine("public static class BuildInfo {");
					file.WriteLine("\tpublic const string ID = \"" + id + "\";");
					file.WriteLine("}");
				}
			} catch (Exception e) {
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Error", "Unable to write BuildInfo.", "OK");
				return false;
			}

			return true;
		}

		static string buildNumberPath {
			get {
				return Utils.projectRootDirectory + "/build_number.txt";
			}
		}

		static int GetBuildNumber() {
			try {
				using (var file = new StreamReader(buildNumberPath, System.Text.Encoding.ASCII)) {
					string line = file.ReadToEnd();
					return int.Parse(line);
				}
			} catch (Exception e) {
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Error", "Unable to read build number.", "OK");
				return -1;
			}
		}

		static bool IncrementBuildNumber() {
			int buildNum = GetBuildNumber();
			if (buildNum < 0) {
				EditorUtility.DisplayDialog("Error", "Unable to increment build number.", "OK");
				return false;
			}
#if PERFORCE
			if (!P4EditFile(buildNumberPath)) {
				EditorUtility.DisplayDialog("Error", "Unable to open buildnumber for edit.", "OK");
				return false;
			}
#endif

			try {
				using (var file = new StreamWriter(buildNumberPath, false, System.Text.Encoding.ASCII)) {
					file.WriteLine(buildNum+1);
				}
			} catch (Exception e) {
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Error", "Unable to increment build number.", "OK");
				return false;
			}

			return true;
		}

		static bool CommitBuildNumber(string message) {
#if PERFORCE
			if (!P4CommitFile(buildNumberPath, message)) {
				P4RevertFile(buildNumberPath);
				EditorUtility.DisplayDialog("Error", "Unable to commit build number.", "OK");
			}
#else
			if (!SVNCommitFile(buildNumberPath, message)) {
				SVNRevertFile(buildNumberPath);
				EditorUtility.DisplayDialog("Error", "Unable to commit build number.", "OK");
			}
#endif
			return true;
		}

		static void RevertBuildNumber() {
#if PERFORCE
			P4RevertFile(buildNumberPath);
#else
			SVNRevertFile(buildNumberPath);
#endif
		}

#if PERFORCE
		static string GetBuildID() {
			int changelist;
			if (!GetP4ChangelistNumber(out changelist)) {
				EditorUtility.DisplayDialog("Error", "Unable to get p4 changelist.", "OK");
				return null;
			}

			int buildNum = GetBuildNumber();
			if (buildNum < 0) {
				return null;
			}

			return LABEL + "-b" + (buildNum+1) + "-r" + changelist;
		}
#else
		static string GetBuildID() {
			int revision1, revision2;
			if (!GetSVNRevisionNumber(out revision1, out revision2)) {
				EditorUtility.DisplayDialog("Error", "Unable to get SVN revision number.", "OK");
				return null;
			}

			int buildNum = GetBuildNumber();
			if (buildNum < 0) {
				return null;
			}

			var rev = (revision2 != revision1) ? (revision1 + ":" + revision2) : revision1.ToString();

			return LABEL + "-b" + (buildNum+1) + "-r" + rev;
		}
#endif

		bool Publish() {
			var steamTempPath = Utils.projectRootDirectory + "/Build/Steam/steamcmd/temp";
			var steamCmdPath = Utils.projectRootDirectory + "/Build/Steam/steamcmd";
			try {
				Directory.CreateDirectory(steamTempPath);
				Directory.CreateDirectory(steamCmdPath);
			} catch (Exception e) {
				Debug.LogException(e);
				EditorUtility.DisplayDialog("Error", "Unable to create temp folder: '" + steamTempPath, "OK");
				return false;
			}

			steamCmdPath += "/steamcmd.exe";

			bool builtOne = false;
			foreach (var platform in PLATFORMS) {
				if (platform.shouldBuild) {
					builtOne = true;
					if (!WritePlatformVDFs(platform)) {
						return false;
					}
				}
			}

			if (builtOne) {
				try {
					if (!File.Exists(steamCmdPath)) {
						File.Copy(Utils.projectRootDirectory + "/steamcmd/steamcmd.exe", steamCmdPath);
					}
				} catch (Exception e) {
					Debug.LogException(e);
					EditorUtility.DisplayDialog("Error", "Unable to copy steamcmd.exe", "OK");
					return false;
				}

				var buildId = GetBuildID();
				if (buildId == null) {
					return false;
				}

				{
					var prefix = string.Empty;
					foreach (var platform in PLATFORMS) {
						if (platform.shouldBuild) {
							prefix += "[" + platform.name + "]";
						}
					}
					if (!string.IsNullOrEmpty(prefix)) {
						buildId = prefix + " " + buildId;
					}
				}

				List<int> appIds = new List<int>();
				foreach (var p in PLATFORMS) {
					if (p.shouldBuild) {
						foreach (int appId in p.appIds) {
							if (!appIds.Contains(appId)) {
								appIds.Add(appId);
							}
						}
					}
				}

				foreach (var appId in appIds) {
					var appConfigFilePath = Utils.projectRootDirectory + "/Build/Steam/steamcmd/app.vdf";

					try {
						using (var file = new StreamWriter(appConfigFilePath, false, System.Text.Encoding.ASCII)) {
							file.WriteLine("\"appbuild\"");
							file.WriteLine("{");
							file.WriteLine("\t\"appid\" \"" + appId + "\"");
							file.WriteLine("\t\"desc\" \"" + buildId + "\"");
							file.WriteLine("\t\"buildoutput\" \"" + steamTempPath + "\"");
							file.WriteLine("\t\"setlive\" \"" + branch + "\"");
							file.WriteLine("\t\"preview\" \"0\"");
							file.WriteLine("\t\"local\" \"\"");
							file.WriteLine("\t\"depots\"");
							file.WriteLine("\t{");
							foreach (var platform in PLATFORMS) {
								if (platform.shouldBuild && platform.appIds.Contains(appId)) {
									foreach (var depotId in platform.depotIds) {
										var depotConfigFile = "depot_" + depotId + ".vdf";
										file.WriteLine("\t\t\"" + depotId + "\" \"" + depotConfigFile + "\"");
									}
								}
							}
							file.WriteLine("\t}");
							file.WriteLine("}");
						}
					} catch (Exception e) {
						Debug.LogException(e);
						EditorUtility.DisplayDialog("Error", "Error writing steam configuration file for application: " + appConfigFilePath, "OK");
						return false;
					}

					var progress = new EditorProgressBar("Uploading to Steam", 1);
					if (!RunSteamCmd(steamCmdPath, appConfigFilePath)) {
						progress.Close();
						return false;
					}
					progress.Step(null);
				}

				if (IncrementBuildNumber()) {
					if (!CommitBuildNumber(buildId)) {
						EditorUtility.DisplayDialog("Error", "Error updating build number", "OK");
						RevertBuildNumber();
					}
				} else {
					RevertBuildNumber();
				}
			} else {
				EditorUtility.DisplayDialog("Error", "No platforms are selected.", "OK");
				return false;
			}

			return true;
		}

		bool RunSteamCmd(string steamCmdPath, string appConfigFilePath) {
			while (true) {
				int retCode;

				// run steam cmd to upload build
				if (!RunProcess(steamCmdPath, "+login " + steamLogin + " " + steamPassword + " +run_app_build_http \"" + appConfigFilePath + "\" +quit", out retCode)) {
					EditorUtility.DisplayDialog("Error", "Error running steamcmd.exe", "OK");
					return false;
				}

				// steamcmd code 7 seems to mean that the steam binaries were updated.
				if ((retCode < 0) || ((retCode > 1) && (retCode != 7))) {
					if (!EditorUtility.DisplayDialog("Error", "steamcmd.exe returned an error.", "Retry", "Cancel")) {
						return false;
					}
				}

				break;
			}

			return true;
		}

		void RecursiveDeleteDirectory(string path) {
			foreach (var subDir in Directory.GetDirectories(path)) {
				RecursiveDeleteDirectory(subDir);
			}

			foreach (var file in Directory.GetFiles(path)) {
				var z = Path.Combine(path, file);
				File.SetAttributes(z, FileAttributes.Normal);
				File.Delete(z);
			}
		}

		void RecursiveClearReadonlyState(string path) {
			foreach (var subDir in Directory.GetDirectories(path)) {
				RecursiveClearReadonlyState(subDir);
			}

			foreach (var file in Directory.GetFiles(path)) {
				var z = Path.Combine(path, file);
				File.SetAttributes(z, FileAttributes.Normal);
			}
		}

		bool BuildPlatform(Platform p) {
			var progress = new EditorProgressBar(null, p.targets.Length*2);

			foreach (var target in p.targets) {
				progress.description = "Building " + p.name + " - [" + target + "]";

				var buildPath = Platform.GetTargetPath(target);

				try {
					RecursiveDeleteDirectory(buildPath);
				} catch (Exception e) {
					if (!(e is DirectoryNotFoundException)) {
						Debug.LogException(e);
						EditorUtility.DisplayDialog("Error", "Unable to clean build path: '" + buildPath + "' for " + p.name, "OK");
						progress.Close();
						return false;
					}
				}

				try {
					Directory.CreateDirectory(buildPath);
				} catch (Exception e) {
					Debug.LogException(e);
					EditorUtility.DisplayDialog("Error", "Unable to create build path: '" + buildPath + "' for " + p.name, "OK");
					progress.Close();
					return false;
				}

				List<string> levels = new List<string>();
				for (int i = 0; i < STATIC_LEVELS.Length; ++i) {
					levels.Add(STATIC_LEVELS[i]);
				}

				for (int i = 0; i < GameManager.LEVELS.Count; ++i) {
					var level = GameManager.LEVELS[i];

					levels.Add("Assets/Scenes/" + level.name + "/" + level.name + ".unity");

					for (int k = 0; k < level.sublevels.Length; ++k) {
						levels.Add("Assets/Scenes/" + level.name + "/" + level.sublevels[k] + ".unity");
					}
				}

				BuildOptions options = BuildOptions.ForceEnableAssertions;

				var buildFlags = scriptFlags;

				if (buildFlags.Contains("PROFILING")) {
					options |= BuildOptions.Development;
				}
				if (p.deployment == EDeploymentType.BackendServer) {
					options |= BuildOptions.EnableHeadlessMode;

					buildFlags = BuildOptionsWindow.AddDefine(buildFlags, "DEDICATED_SERVER");
					buildFlags = BuildOptionsWindow.AddDefine(buildFlags, "BACKEND_SERVER");
					buildFlags = BuildOptionsWindow.RemoveDefine(buildFlags, "LOGIN_SERVER");
					buildFlags = BuildOptionsWindow.RemoveDefine(buildFlags, "STEAM_API");
				}
												
				if (buildFlags != PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)) {
					PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, buildFlags);
				}

				var report = BuildPipeline.BuildPlayer(levels.ToArray(), Platform.GetAppPath(target), target, options);
				
				// TODO:

				//if (!string.IsNullOrEmpty(err)) {
				//	EditorUtility.DisplayDialog("Error", err, "OK");
				//	progress.Close();
				//	return false;
				//}

				if (p.deployment == EDeploymentType.BackendServer) {
					if (!CopyTelemetry(buildPath)) {
						EditorUtility.DisplayDialog("Error", "Failed to copy telemetry.", "OK");
						progress.Close();
						return false;
					}
				}

				RecursiveClearReadonlyState(buildPath);

				if (target == BuildTarget.StandaloneWindows) {
					// need to copy steam over...
					try {
						File.Move(Path.Combine(Platform.GetTargetPath(target), "Deadhold_Data/Plugins/steam_api.dll"), Path.Combine(Platform.GetTargetPath(target), "steam_api.dll"));
					} catch (Exception) { }
				}

				progress.Step(null);
				if (BuildOptionsWindow.IsDefined(buildFlags, "SHIP")) {
					progress.description = "Obfuscating " + p.name + " [" + target + "]";
					if (!ObfuscateAssembly(Platform.GetAssemblyPath(target))) {
						progress.Close();
						return false;
					}
				}

				progress.Step(null);
			}

			return true;
		}

		bool ObfuscateAssembly(string assemblyPath) {
			int retCode;
			string output;

			if (!RunSilentProcess("eazfuscator.net.exe", "\"" + assemblyPath + "\"", out retCode, out output) || (retCode != 0)) {
				if (!string.IsNullOrEmpty(output)) {
					Debug.LogError(output);
				}
				EditorUtility.DisplayDialog("Error", "Assembly obfuscation failed", "OK");
				return false;
			}

			return true;
		}

		bool WritePlatformVDFs(Platform p) {

			for (int i = 0; i < p.depotIds.Count; ++i) {
				var depotId = p.depotIds[i];
				var target = p.targets[i];

				var depotConfigFilePath = Utils.projectRootDirectory + "/Build/Steam/steamcmd/depot_" + depotId + ".vdf";
				
				try {
					using (var file = new StreamWriter(depotConfigFilePath, false, System.Text.Encoding.ASCII)) {
						file.WriteLine("\"DepotBuildConfig\"");
						file.WriteLine("{");
						file.WriteLine("\t\"DepotID\" \"" + depotId + "\"");
						file.WriteLine("\t\"ContentRoot\" \"" + Platform.GetTargetPath(target) + "\"");
						file.WriteLine("\t\"FileMapping\"");
						file.WriteLine("\t{");
						file.WriteLine("\t\t\"LocalPath\" \"*\"");
						file.WriteLine("\t\t\"DepotPath\" \".\"");
						file.WriteLine("\t\t\"recursive\" \"1\"");
						file.WriteLine("\t}");
						file.WriteLine("}");
					}
				} catch (Exception e) {
					Debug.LogException(e);
					EditorUtility.DisplayDialog("Error", "Error writing steam configuration file for platform: " + p.name + ", file: " + depotConfigFilePath, "OK");
					return false;
				}
			}

			return true;
		}

		static bool CheckForSVN() {
			try {
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				startInfo.CreateNoWindow = true;
				startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				startInfo.Arguments = "--version";
				startInfo.FileName = "svn.exe";
				return System.Diagnostics.Process.Start(startInfo) != null;
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}
		}

		static bool CheckForP4() {
			try {
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				startInfo.CreateNoWindow = true;
				startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				startInfo.Arguments = "-h";
				startInfo.FileName = Path.Combine(p4path, "p4.exe");
				return System.Diagnostics.Process.Start(startInfo) != null;
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}
		}

		static bool CheckForObfuscator() {
			try {
				System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
				startInfo.CreateNoWindow = true;
				startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
				startInfo.FileName = "eazfuscator.net.exe";
				return System.Diagnostics.Process.Start(startInfo) != null;
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}
		}

		static bool SVNRevertFile(string path) {
			int retCode;
			string output;

			if (!RunSilentProcess("svn.exe", "revert " + "\"" + path + "\"", out retCode, out output)) {
				return false;
			}

			if (retCode != 0) {
				Debug.LogError(output);
				EditorUtility.DisplayDialog("Error", "svn revert failed", "OK");
				return false;
			}

			return true;
		}

		static bool SVNCommitFile(string path, string message) {
			int retCode;
			string output;

			if (!RunSilentProcess("svn.exe", "commit -m \"" + message + "\" \"" + path + "\"", out retCode, out output)) {
				return false;
			}

			if (retCode != 0) {
				Debug.LogError(output);
				EditorUtility.DisplayDialog("Error", "svn commit failed", "OK");
				return false;
			}

			return true;
		}

		static bool GetSVNRevisionNumber(out int revision1, out int revision2) {
			int retCode;
			string output;

			revision1 = 0;
			revision2 = 0;

			if (!RunSilentProcess("svnversion", "-n", out retCode, out output) || string.IsNullOrEmpty(output)) {
				EditorUtility.DisplayDialog("Error", "Unable to get SVN revision number!", "OK");
				return false;
			}

			int ofs = 0;
			while ((ofs < output.Length) && char.IsNumber(output[ofs])) {
				++ofs;
			}

			if (ofs < 1) {
				EditorUtility.DisplayDialog("Error", "Unable to get SVN revision number!", "OK");
				return false;
			}

			revision1 = int.Parse(output.Substring(0, ofs));

			int ofs2 = ofs+1;
			while ((ofs2 < output.Length) && char.IsNumber(output[ofs2])) {
				++ofs2;
			}

			if (ofs2 > ofs+1) {
				revision2 = int.Parse(output.Substring(ofs+1, ofs2-ofs-1));
			} else {
				revision2 = revision1;
			}

			return true;
		}

		static bool P4EditFile(string path) {
			int retCode;
			string output;

			if (!RunP4Command("edit \"" + path + "\"", out retCode, out output)) {
				return false;
			}

			if (retCode != 0) {
				Debug.LogError(output);
				EditorUtility.DisplayDialog("Error", "p4 edit failed", "OK");
				return false;
			}

			return true;
		}

		static bool P4RevertFile(string path) {
			int retCode;
			string output;

			if (!RunP4Command("revert \"" + path + "\"", out retCode, out output)) {
				return false;
			}

			if (retCode != 0) {
				Debug.LogError(output);
				EditorUtility.DisplayDialog("Error", "p4 revert failed", "OK");
				return false;
			}

			return true;
		}

		static bool P4CommitFile(string path, string message) {
			int retCode;
			string output;

			if (!RunP4Command("submit -d \"" + message + "\" \"" + path + "\"", out retCode, out output)) {
				return false;
			}

			if (retCode != 0) {
				Debug.LogError(output);
				EditorUtility.DisplayDialog("Error", "p4 submit failed", "OK");
				return false;
			}

			return true;
		}

		static bool GetP4ChangelistNumber(out int changelist) {
			int retCode;
			string output;

			changelist = 0;

			if (!RunP4Command("changes -m1 @" + p4client, out retCode, out output) || string.IsNullOrEmpty(output)) {
				return false;
			}

			int ofs = 7;
			string rev = string.Empty;

			while ((ofs < output.Length) && char.IsNumber(output[ofs])) {
				rev += output[ofs];
				++ofs;
			}

			if (string.IsNullOrEmpty(rev)) {
				EditorUtility.DisplayDialog("Error", "Unable to get P4 changelist number!", "OK");
				return false;
			}

			changelist = int.Parse(rev);
			return true;
		}

		static bool RunP4Command(string arguments, out int retCode, out string output) {
			if (!RunSilentProcess(Path.Combine(p4path, "p4.exe"), string.Format("-p {0} -u {1} -P \"{2}\" -c {3} {4}", p4server, p4user, p4pass, p4client, arguments), out retCode, out output)) {
				EditorUtility.DisplayDialog("Error", "Unable run P4 command: " + arguments + "!", "OK");
				return false;
			}
			return true;
		}

		static bool RunProcess(string processName, string arguments, out int retCode) {
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.Arguments = arguments;
			startInfo.FileName = processName;

			try {
				var proc = System.Diagnostics.Process.Start(startInfo);
				if (proc != null) {
					proc.WaitForExit();
					retCode = proc.ExitCode;
					return true;
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}

			retCode = -1;
			return false;
		}

		static bool RunSilentProcess(string processName, string arguments, out int retCode, out string output) {
			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
			startInfo.CreateNoWindow = true;
			startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
			startInfo.Arguments = arguments;
			startInfo.FileName = processName;
			startInfo.UseShellExecute = false;
			startInfo.RedirectStandardError = true;
			startInfo.RedirectStandardOutput = true;

			try {
				var proc = System.Diagnostics.Process.Start(startInfo);
				if (proc != null) {
					output = ReadOutput(proc);
					proc.WaitForExit();
					retCode = proc.ExitCode;
					return true;
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}

			retCode = -1;
			output = string.Empty;
			return false;
		}

		static string ReadOutput(System.Diagnostics.Process process) {
			string s = "";
			string r;

			while ((r=process.StandardOutput.ReadLine()) != null) {
				s += r+"\n";
			}

			while ((r=process.StandardError.ReadLine()) != null) {
				s += r+"\n";
			}

			return s;
		}

		static bool CopyTelemetry(string targetPath) {
			var telemetryDstPath = Path.Combine(targetPath, "Telemetry");
			var telemetrySrcPath = Path.Combine(Utils.projectRootDirectory, "Backend/Telemetry/bin/Release");

			Directory.CreateDirectory(telemetryDstPath);

			if (!Directory.Exists(telemetrySrcPath)) {
				Debug.LogError("Telemetry does not exist at: " + telemetrySrcPath);
				return false;
			}

			var files = Directory.GetFiles(telemetrySrcPath);
			foreach (var f in files) {
				var dest = Path.Combine(telemetryDstPath, Path.GetFileName(f));
				try {
					File.Copy(f, dest);
				} catch (Exception e) {
					Debug.LogException(e);
					return false;
				}
			}

			return true;
		}
	}
}