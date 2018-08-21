// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead.Client {
	public sealed class GraphicsSettings {

		public int activeDisplayIndex;
		public int screenWidth;
		public int screenHeight;
		public bool vsync;
		public int waterReflections;
		public int shadows;
		public int aa;
		public int umaQuality;
		public int animationQuality;
		public bool ssao;
		public bool reflectionProbes;
		public bool anisoTextures;
		public bool fullscreen;

		public GraphicsSettings() {
			Load();
		}

		public void Load() {
			activeDisplayIndex = savedActiveDisplayIndex;
			screenWidth = savedScreenWidth;
			screenHeight = savedScreenHeight;
			vsync = savedVsync;
			waterReflections = savedWaterReflections;
			shadows = savedShadows;
			aa = savedAA;
			ssao = savedSSAO;
			reflectionProbes = savedReflectionProbes;
			anisoTextures = savedAnisoTextures;
			fullscreen = savedFullscreen;
			umaQuality = savedUMAQuality;
			animationQuality = savedAnimationQuality;
		}

		public int displayCount {
			get {
				return Display.displays.Length;
			}
		}

		public int savedActiveDisplayIndex {
			get {
				return UserPrefs.instance.GetInt("Graphics.Monitor", 0);
			}
		}

		public int savedScreenWidth {
			get {
				return UserPrefs.instance.GetInt("Graphics.ScreenWidth", 1280);
			}
		}

		public int savedScreenHeight {
			get {
				return UserPrefs.instance.GetInt("Graphics.ScreenHeight", 720);
			}
		}

		public bool savedVsync {
			get {
				return UserPrefs.instance.GetInt("Graphics.VSync", 1) != 0;
			}
		}

		public int savedWaterReflections {
			get {
				return UserPrefs.instance.GetInt("Graphics.WaterReflections", 1);
			}
		}

		public int savedShadows {
			get {
				return UserPrefs.instance.GetInt("Graphics.Shadows", 3);
			}
		}

		public int savedUMAQuality {
			get {
				return UserPrefs.instance.GetInt("Graphics.UMAQuality", 2);
			}
		}

		public int savedAA {
			get {
				return UserPrefs.instance.GetInt("Graphics.AA", 1);
			}
		}

		public int savedAnimationQuality {
			get {
				return UserPrefs.instance.GetInt("Graphics.AnimationQuality", 2);
			}
		}

		public bool savedSSAO {
			get {
				return UserPrefs.instance.GetInt("Graphics.SSAO", 1) != 0;
			}
		}

		public bool savedReflectionProbes {
			get {
				return UserPrefs.instance.GetInt("Graphics.ReflectionProbes", 1) != 0;
			}
		}

		public bool savedAnisoTextures {
			get {
				return UserPrefs.instance.GetInt("Graphics.AnisoTextures", 1) != 0;
			}
		}

		public bool savedFullscreen {
			get {
				return UserPrefs.instance.GetInt("Graphics.Fullscreen", 1) != 0;
			}
		}

		public bool dirty {
			get {
				return (activeDisplayIndex != savedActiveDisplayIndex) ||
					(screenWidth != savedScreenWidth) ||
					(screenHeight != savedScreenHeight) ||
					(vsync != savedVsync) ||
					(waterReflections != savedWaterReflections) ||
					(shadows != savedShadows) ||
					(aa != savedAA) ||
					(ssao != savedSSAO) ||
					(reflectionProbes != savedReflectionProbes) ||
					(anisoTextures != savedAnisoTextures) ||
					(fullscreen != savedFullscreen) ||
					(umaQuality != savedUMAQuality) ||
					(animationQuality != savedAnimationQuality);
			}
		}

		public void ApplyNonResolution() {
			ApplyShadows(); // this sets quality level globally
			ApplyWaterReflections();
			ApplyCameraSettings();
#if DEV_STREAMING
			QualitySettings.vSyncCount = 0;
			QualitySettings.realtimeReflectionProbes = false;
			QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
			QualitySettings.blendWeights = BlendWeights.FourBones;
#else
			QualitySettings.vSyncCount = vsync ? 1 : 0;
			QualitySettings.realtimeReflectionProbes = reflectionProbes;
			QualitySettings.anisotropicFiltering = anisoTextures ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Disable;
			QualitySettings.blendWeights = (animationQuality == 0) ? BlendWeights.OneBone : (animationQuality == 1) ? BlendWeights.TwoBones : BlendWeights.FourBones;
#endif
		}

		public void ApplyCameraSettings() {
			ApplyAA();
			ApplySSAO();
		}

		void ApplyWaterReflections() {
			var go = GameObject.Find("Water/VisibleWaterPlane");
			if (go != null) {
				//var script = go.GetComponent<MirrorReflection>();
				//if (waterReflections > 0) {
				//	script.m_TextureSize = Mathf.FloorToInt(Mathf.Pow(2, 8+waterReflections));
				//}
				//script.enabled = waterReflections != 0;
			}
		}

		void ApplySSAO() {
			var cameras = GameObject.FindObjectsOfType<Camera>();

			foreach (var c in cameras) {
				ApplySSAO(c.gameObject);
			}
		}

		void ApplySSAO(GameObject go) {
			//var script = go.GetComponent<SESSAO>();
			//if (script != null) {
			//	script.enabled = ssao;
			//}
		}

		void ApplyAA() {
			var cameras = GameObject.FindObjectsOfType<Camera>();

			foreach (var c in cameras) {
				ApplyAA(c.gameObject);
			}
		}

		void ApplyAA(GameObject go) {
			//var script = go.GetComponent<UnityStandardAssets.ImageEffects.Antialiasing>();
			//if (script != null) {
			//	script.enabled = aa > 0;
			//	if (aa > 6) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.DLAA;
			//	} else if (aa > 5) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.FXAA3Console;
			//	} else if (aa > 4) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.FXAA2;
			//	} else if (aa > 3) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.FXAA1PresetB;
			//	} else if (aa > 2) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.FXAA1PresetA;
			//	} else if (aa > 1) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.NFAA;
			//	} else if (aa > 0) {
			//		script.mode = UnityStandardAssets.ImageEffects.AAMode.SSAA;
			//	}
			//}
		}

		void ApplyShadows() {
#if !DEV_STREAMING
			if (shadows > 3) {
				QualitySettings.SetQualityLevel(4);
			} else if (shadows > 2) {
				QualitySettings.SetQualityLevel(3);
			} else if (shadows > 1) {
				QualitySettings.SetQualityLevel(2);
			} else if (shadows > 0) {
				QualitySettings.SetQualityLevel(1);
			} else {
#endif
				QualitySettings.SetQualityLevel(0);
#if !DEV_STREAMING
		}
#endif
		}

		public bool ApplyResolution() {
			bool resChange = false;
			bool switchFullscreenOnly = false;

			var unityDisplay = PlayerPrefs.GetInt("UnitySelectMonitor");

			if (activeDisplayIndex != unityDisplay) {
				PlayerPrefs.SetInt("UnitySelectMonitor", activeDisplayIndex);
				Debug.Log("Vidmodechange: " + screenWidth + "x" + screenHeight + ", fullscreen = " + fullscreen);
				Screen.SetResolution(screenWidth, screenHeight, fullscreen);
				resChange = true;
			} else if ((screenWidth != savedScreenWidth) || (screenHeight != savedScreenHeight) || (fullscreen != savedFullscreen)) {
				resChange = true;
				switchFullscreenOnly = (screenWidth == savedScreenWidth) && (screenHeight == savedScreenHeight);
				Debug.Log("Vidmodechange: " + screenWidth + "x" + screenHeight + ", fullscreen = " + fullscreen);
				Screen.SetResolution(screenWidth, screenHeight, fullscreen);
			}

			PlayerPrefs.SetInt("Screenmanager Resolution Width", screenWidth);
			PlayerPrefs.SetInt("Screenmanager Resolution Height", screenHeight);
			PlayerPrefs.SetInt("Screenmanager Is Fullscreen mode", fullscreen ? 1 : 0);

			return resChange && !switchFullscreenOnly;
		}

		public void Apply() {
			ApplyResolution();
			ApplyNonResolution();
		}

		public void Save() {
			UserPrefs.instance.SetInt("Graphics.Monitor", activeDisplayIndex);
			UserPrefs.instance.SetInt("Graphics.ScreenWidth", screenWidth);
			UserPrefs.instance.SetInt("Graphics.ScreenHeight", screenHeight);
			UserPrefs.instance.SetInt("Graphics.VSync", vsync ? 1 : 0);
			UserPrefs.instance.SetInt("Graphics.WaterReflections", waterReflections);
			UserPrefs.instance.SetInt("Graphics.Shadows", shadows);
			UserPrefs.instance.SetInt("Graphics.AA", aa);
			UserPrefs.instance.SetInt("Graphics.SSAO", ssao ? 1 : 0);
			UserPrefs.instance.SetInt("Graphics.ReflectionProbes", reflectionProbes ? 1 : 0);
			UserPrefs.instance.SetInt("Graphics.AnisoTextures", anisoTextures ? 1 : 0);
			UserPrefs.instance.SetInt("Graphics.Fullscreen", fullscreen ? 1 : 0);
			UserPrefs.instance.SetInt("Graphics.UMAQuality", umaQuality);
			UserPrefs.instance.SetInt("Graphics.AnimationQuality", animationQuality);
		}

		public void RevertResolution() {
			activeDisplayIndex = savedActiveDisplayIndex;
			screenWidth = savedScreenWidth;
			screenHeight = savedScreenHeight;
			ApplyResolution();
		}

		public void Detect() {
			var vendorId = SystemInfo.graphicsDeviceVendorID;
						
			if ((vendorId == 0x10DE) || (vendorId == 0x1002) || (vendorId == 0x1022)) {
				// ATI/NVIDIA
				screenWidth = Display.main.renderingWidth;
				screenHeight = Display.main.renderingHeight;
				aa = 1;
				ssao = true;
				anisoTextures = true;
				umaQuality = 2;
				shadows = 3;
				waterReflections = 2;
				animationQuality = 2;
				vsync = true;
			} else {
				// Everyone else
				screenWidth = 1280;
				screenHeight = 720;
				aa = 0;
				ssao = false;
				anisoTextures = false;
				umaQuality = 1;
				shadows = 0;
				waterReflections = 0;
				animationQuality = 1;
				vsync = false;
			}
		}
	}
}