// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead;

public static partial class Utils {
	public static Vector3 PutPositionOnGround(Vector3 pos) {
		RaycastHit hitInfo;
		if (Physics.Raycast(pos + new Vector3(0, 1024, 0), Vector3.down, out hitInfo, Mathf.Infinity, Layers.DefaultMask|Layers.TerrainMask|Layers.BlockMask)) {
			return hitInfo.point;
		}
		return pos;
	}

	public static Vector3 PutPositionOnGroundOrWater(Vector3 pos) {
		RaycastHit hitInfo;
		if (Physics.Raycast(pos + new Vector3(0, 1024, 0), Vector3.down, out hitInfo, Mathf.Infinity, Layers.DefaultMask|Layers.TerrainMask|Layers.BlockMask|Layers.WaterMask, QueryTriggerInteraction.Collide)) {
			return hitInfo.point;
		}
		return pos;
	}

	public static Color GetUserPrefsPrimaryColor() {
		return UserPrefs.instance.GetColor("primaryColor", Color.blue);
    }

	public static Color GetUserPrefsSecondaryColor() {
		return UserPrefs.instance.GetColor("secondaryColor", Color.white);
	}

	public static void SetUserPrefsPrimaryColor(Color color) {
		UserPrefs.instance.SetColor("primaryColor", color);
	}

	public static void SetUserPrefsSecondaryColor(Color color) {
		UserPrefs.instance.SetColor("secondaryColor", color);
	}

	public static string GetLocalizedText(string key) {
		return "???" + key + "???";
	}

	public static string GetLocalizedText(string key, params object[] args) {
		return string.Format(GetLocalizedText(key), args);
	}

	public static void PrecacheWithSounds<T>(WeakAssetRef<T> asset) where T : Component {
		var t = asset.Load();
		if (t != null) {
			t.gameObject.PrecacheSounds();
		}
	}

	public static void PrecacheWithSounds<T>(WeakAssetRef<T> asset, System.Action<T> f) where T : Component {
		var t = asset.Load();
		if (t != null) {
			t.gameObject.PrecacheSounds();
			f(t);
		}
	}

	public static void PrecacheWithSounds(GameObject_WRef asset) {
		var go = asset.Load();
		if (go != null) {
			go.PrecacheSounds();
		}
	}

	public static void PrecacheWithSounds(GameObject_WRef asset, System.Action<GameObject> f) {
		var go = asset.Load();
		if (go != null) {
			go.PrecacheSounds();
			f(go);
		}
	}

	public static void PrecacheSounds(this GameObject go) {
		var sounds = go.GetComponentsInAllChildren<SoundEntity>();
		for (int i = 0; i < sounds.Length; ++i) {
			SoundCue.Precache(sounds[i].soundCue);
		}
	}
}