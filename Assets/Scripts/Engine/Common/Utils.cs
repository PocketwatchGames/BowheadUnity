// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using System.Reflection;
using System.Collections;
#endif

public static partial class Utils {

	public static string activeSceneName {
		get {
			return SceneManager.GetActiveScene().name;
		}
	}

	public static string loadedSubLevelName {
		get {
			var name = SceneManager.GetSceneAt(0).name;
			for (int i = 1; i < SceneManager.sceneCount; ++i) {
				var z = SceneManager.GetSceneAt(i).name;
				if (z.Length > name.Length) { // this works because Bowhead naming conventions mean sublevels are longer
					name = z;
				}
			}
			return name;
		}
	}

	public static int loadedSceneCount {
		get {
			int numLoaded = 0;
			for (int i = 0; i < SceneManager.sceneCount; ++i) {
				var scene = SceneManager.GetSceneAt(i);
				if (scene.isLoaded) {
					++numLoaded;
				}
			}
			return numLoaded;
		}
	}

	public static Scene firstLoadedScene {
		get {
			for (int i = 0; i < SceneManager.sceneCount; ++i) {
				var scene = SceneManager.GetSceneAt(i);
				if (scene.isLoaded) {
					return scene;
				}
			}
			return new Scene();
		}
	}

	public static bool ContainsAny(this string s, string chars) {
		for (int i = 0; i < chars.Length; ++i) {
			if (s.IndexOf(chars[i]) != -1) {
				return true;
			}
		}

		return false;
	}

	// Disable the following behavior:
	// If sending a datagram using the sendto function results in an “ICMP port unreachable” response the subsequent call to recvfrom does not work with a WSAECONNRESET (10054) error response.
	public static void DisableICMPUnreachablePortError(this Socket socket) {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		uint IOC_IN = 0x80000000;
		uint IOC_VENDOR = 0x18000000;
		uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
		socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { System.Convert.ToByte(false) }, null);
#endif
	}

	public static string GetCommandLineArg(string cmd) {
		return GetCommandLineArg(cmd, 0);
	}

	public static string GetCommandLineArg(string cmd, int idx) {
		cmd = cmd.ToLower();
		var args = System.Environment.GetCommandLineArgs();
		for (int i = 0; i < args.Length; ++i) {
			if (args[i].ToLower() == cmd) {
				if ((i+idx+1) < args.Length) {
					var x = args[i+idx+1];
					if (x.StartsWith("+") || x.StartsWith("-")) {
						// x is another command line argument.
						return null;
					}
					return x;
				}
				return null;
			}
		}

		return null;
	}

	public static bool HasCommandLineArg(string cmd) {
		cmd = cmd.ToLower();
		var args = System.Environment.GetCommandLineArgs();
		for (int i = 0; i < args.Length; ++i) {
			if (args[i].ToLower() == cmd) {
				return true;
			}
		}
		return false;
	}

	public static int LerpRange(int min, int maxExclusive, float t) {
		var r = min + Mathf.FloorToInt((maxExclusive-min) * t);
		return (r < maxExclusive) ? r : maxExclusive-1;
	}


	public static void Swap<T>(ref T a, ref T b) {
		T x = a;
		a = b;
		b = x;
	}

	public static void RemoveAtSwap<T>(this List<T> list, int index) {
		if (list.Count > 1) {
			list[index] = list[list.Count-1];
			list.RemoveAt(list.Count-1);
		} else {
			list.RemoveAt(index);
		}
	}

	public static bool RemoveSwapSlow<T>(this List<T> list, T item) {
		var index = list.FindIndex((r) => r.Equals(item));
		if (index != -1) {
			RemoveAtSwap(list, index);
			return true;
		}
		return false;
	}

	public static Vector3 GetWorldSpaceCenter(this Collider c) {
		var capsule = c as CapsuleCollider;
		if (capsule != null) {
			return capsule.transform.position + capsule.center;
		}
		var sphere = c as SphereCollider;
		if (sphere != null) {
			return sphere.transform.position + sphere.center;
		}
		var box = c as BoxCollider;
		if (box != null) {
			return box.transform.position + box.center;
		}
		return c.transform.position;
	}

	public static float GetWorldSpaceRadius(this Collider c) {
		var capsule = c as CapsuleCollider;
		if (capsule != null) {
			switch (capsule.direction) {
				case 0:
					return capsule.height/2 * capsule.transform.localScale.x;
                case 1:
					return capsule.height/2 * capsule.transform.localScale.y;
				default:
					return capsule.height/2 * capsule.transform.localScale.z;
			}
		}
		var sphere = c as SphereCollider;
		return sphere.transform.localScale.x * sphere.radius;
	}

	public static Quaternion LookBasis(Vector3 up) {
		var basis = Vector3.right;

		if (Vector3.Dot(up, basis) > 0.99999f) {
			basis = Vector3.forward;
		}

		var forward = Vector3.Cross(up, basis);
		return Quaternion.LookRotation(forward, up);
	}

	public static void DestroyGameObject(GameObject go) {
		GameObject.Destroy(go);
	}

	public static void DestroyGameObject(GameObject go, float delay) {
		GameObject.Destroy(go, delay);
	}

	public static void DestroyComponent(Component c) {
		GameObject.Destroy(c);
	}

	public static void DestroyComponent(Component c, float delay) {
		GameObject.Destroy(c, delay);
	}
	
	public static void SetLayerRecursive(this GameObject go, int layer, int excludeLayerMask) {
		if (((1<<go.layer) & excludeLayerMask) == 0) {
			go.layer = layer;
		}

		for (int i = 0; i < go.transform.childCount; ++i) {
			var child = go.transform.GetChild(i);
			child.gameObject.SetLayerRecursive(layer, excludeLayerMask);
		}
	}

	public static void SetChildrenLayerRecursive(this GameObject go, int layer, int excludeLayerMask) {
		for (int i = 0; i < go.transform.childCount; ++i) {
			var child = go.transform.GetChild(i);
			child.gameObject.SetLayerRecursive(layer, excludeLayerMask);
		}
	}

	public static Transform GetFirstChild(this Transform t) {
		if (t.childCount < 1) {
			return null;
		}
		return t.GetChild(0);
	}

	public static T GetFirstChildComponent<T>(this Transform t) {
		var child = t.GetFirstChild();
		return (child != null) ? child.GetComponent<T>() : default(T);
	}

	public static Transform GetLastChild(this Transform t) {
		if (t.childCount < 1) {
			return null;
		}
		return t.GetChild(t.childCount-1);
	}

	public static T GetLastChildComponent<T>(this Transform t) {
		var child = t.GetLastChild();
		return (child != null) ? child.GetComponent<T>() : default(T);
	}

	public static string GetChildPath(this Transform t, Transform child) {
		var x = child;
		var path = x.name;
		for (x = x.parent; (x != null) && (x != t); x = x.parent) {
			path = x.name + "/" + path;
		}
		if (x == null) {
			throw new System.Exception(t.name + " does not have a child " + child.name);
		}
		return path;
	}

	public static T FindComponentUpwards<T>(this Transform t) where T : Component {
		var c = t.GetComponent<T>();
		if (c != null) {
			return c;
		}
		t = t.parent;
		if (t != null) {
			return t.GetComponent<T>();
		}
		return default(T);
	}

	public static Actor FindServerActorUpwards(this Transform t) {
		var r = t.FindComponentUpwards<ActorReference>();
		if (r != null) {
			return r.serverActor;
		}
		return null;
	}

	public static Actor FindClientActorUpwards(this Transform t) {
		var r = t.FindComponentUpwards<ActorReference>();
		if (r != null) {
			return r.clientActor;
		}
		return null;
	}

	public static T FindComponentUpwards<T>(this GameObject go) where T : Component {
		return go.transform.FindComponentUpwards<T>();
	}

	public static Actor FindServerActorUpwards(this GameObject go) {
		return go.transform.FindServerActorUpwards();
	}

	public static Actor FindClientActorUpwards(this GameObject go) {
		return go.transform.FindClientActorUpwards();
	}

	public static void DestroyAllChildren(this Transform t) {
		for (int i = t.childCount-1; i >= 0; --i) {
			var child = t.GetChild(i);
			if (child != null) {
				DestroyGameObject(child.gameObject);
			}
		}
	}

	public static void DestroyAllChildren(this GameObject g) {
		DestroyAllChildren(g.transform);
	}

	public static GameObject FindChild(this GameObject go, string name) {
		var t = go.transform.Find(name);
		return (t != null) ? t.gameObject : null;
	}

	public static GameObject FindAnyChild(this GameObject go, string name) {
		var t = go.transform.FindAnyChild(name);
		return (t != null) ? t.gameObject : null;
	}

	public static Transform FindAnyChild(this Transform t, string name) {
		var x = t.Find(name);
		if (x != null) {
			return x;
		}
		for (int i = 0; i < t.childCount; ++i) {
			x = t.GetChild(i).FindAnyChild(name);
			if (x != null) {
				return x;
			}
		}
		return null;
	}

	public static string GetPath(this Transform t) {
		string s = t.name;
		while (t.parent != null) {
			t = t.parent;
			s = t.name + "/" + s;
		}
		return s;
	}

	public static void DestroyChild(this Transform t, string name) {
		var x = t.Find(name);
		if (x != null) {
			DestroyGameObject(x.gameObject);
		}
	}

	public static void DestroyChild(this GameObject go, string name) {
		var x = go.FindChild(name);
		if (x != null) {
			DestroyGameObject(x);
		}
	}

	public static T GetChildComponent<T>(this GameObject go, string name) {
		var x = go.transform.Find(name);

		if (x != null) {
			return x.GetComponent<T>();
		}
		return default(T);
	}

	public static T GetChildComponent<T>(this Transform t, string name) {
		var x = t.Find(name);
		
		if (x != null) {
			return x.GetComponent<T>();
		}
		return default(T);
	}

	public static T GetAnyChildComponent<T>(this GameObject go, string name) {
		var x = go.transform.FindAnyChild(name);
		if (x != null) {
			return x.GetComponent<T>();
		}
		return default(T);
	}

	public static T GetAnyChildComponent<T>(this Transform t, string name) {
		var x = t.FindAnyChild(name);
		if (x != null) {
			return x.GetComponent<T>();
		}
		return default(T);
	}

	public static T[] GetComponentsInAllChildren<T>(this Component c) {
		return c.gameObject.transform.GetComponentsInAllChildren<T>();
	}

	public static T[] GetComponentsInAllChildren<T>(this GameObject go) {
		return go.transform.GetComponentsInAllChildren<T>();
	}

	public static T[] GetComponentsInAllChildren<T>(this Transform t) {
		return GetComponentsInAllChildren(t, new List<T>()).ToArray();
	}

	static List<T> GetComponentsInAllChildren<T>(Transform t, List<T> list) {
		list.AddRange(t.GetComponents<T>());
		for (int i = 0; i < t.childCount; ++i) {
			var x = t.GetChild(i);
			GetComponentsInAllChildren(x, list);
		}
		return list;
	}

	public static T GetComponentInAnyChild<T>(this GameObject go) {
		return go.transform.GetComponentInAnyChild<T>();
	}

	public static T GetComponentInAnyChild<T>(this Transform t) {
		var c = t.GetComponent<T>();
		if (c != null) {
			return c;
		}

		for (int i = 0; i < t.childCount; ++i) {
			var x = t.GetChild(i);
			c = GetComponentInAnyChild<T>(x);
			if (c != null) {
				return c;
			}
		}

		return default(T);
	}

	public static void DestroyComponent<T>(this Transform t) where T : Component {
		t.DestroyComponent<T>(0f);
	}

	public static void DestroyComponent<T>(this Transform t, float delay) where T : Component {
		t.gameObject.DestroyComponent<T>(delay);
	}

	public static void DestroyComponent<T>(this GameObject go) where T : Component {
		go.DestroyComponent<T>(0f);
	}

	public static void DestroyComponent<T>(this GameObject go, float delay) where T: Component {
		var t = go.GetComponent<T>();
		if (t != null) {
			GameObject.Destroy(t, delay);
		}
	}

	public static Transform[] FindTagsInHierarchy(this Transform t, string tag) {
		List<Transform> ttags = new List<Transform>();

		if (t.CompareTag(tag)) {
			ttags.Add(t);
		}
				
		for (int i = 0; i < t.childCount; ++i) {
			var x = t.GetChild(i);
			ttags.AddRange(x.FindTagsInHierarchy(tag));
		}
		
		return ttags.ToArray();
	}

	public static GameObject[] FindTagsInHierarchy(this GameObject t, string tag) {
		List<GameObject> ttags = new List<GameObject>();

		if (t.CompareTag(tag)) {
			ttags.Add(t);
		}

		for (int i = 0; i < t.transform.childCount; ++i) {
			var x = t.transform.GetChild(i);
			ttags.AddRange(x.gameObject.FindTagsInHierarchy(tag));
		}

		return ttags.ToArray();
	}

	public static Transform FindTagInHierarchy(this Transform t, string tag) {
		if (t.CompareTag(tag)) {
			return t;
		}
		for (int i = 0; i < t.childCount; ++i) {
			var x = t.GetChild(i);
			if (x.CompareTag(tag)) {
				return x;
			}
		}
		for (int i = 0; i < t.childCount; ++i) {
			var x = t.GetChild(i).FindTagInHierarchy(tag);
			if (x != null) {
				return x;
			}
		}
		return null;
	}

	public static GameObject FindTagInHierarchy(this GameObject go, string tag) {
		var t = go.transform.FindTagInHierarchy(tag);
		if (t != null) {
			return t.gameObject;
		}
		return null;
	}

	public static void DestroyComponentsInChildren<T>(this Transform t) where T : Object {
		t.DestroyComponentsInChildren<T>(0f);
	}

	public static void DestroyComponentsInChildren<T>(this Transform t, float delay) where T : Object {
		t.gameObject.DestroyComponentsInChildren<T>(delay);
	}

	public static void DestroyComponentsInChildren<T>(this GameObject go) where T : Object {
		go.DestroyComponentsInChildren<T>(0f);
	}

    public static void DestroyComponentsInChildren<T>(this GameObject go, float delay) where T : Object {
		var x = go.GetComponentsInChildren<T>();
		for (int i = 0; i < x.Length; ++i) {
			GameObject.Destroy(x[i]);
		}
	}

	public static void ActivateHierarchy(this GameObject obj, bool active, params GameObject[] except) {
		obj.SetActive(active);
		for (int i = 0; i < obj.transform.childCount; ++i) {
			var child = obj.transform.GetChild(i).gameObject;

			bool exclude = false;
			if (except != null) {
				for (int z = 0; z < except.Length; ++z) {
					if (except[z] == child) {
						exclude = true;
						break;
					}
                }
			}

			if (!exclude) {
				child.ActivateHierarchy(active, except);
			}
		}
	}

	public static byte NormalizedFloatToByte(float n) {
		return (byte)(n * 255f);
	}

	public static float ByteToNormalizedFloat(byte b) {
		return b / 255f;
	}

	public static bool Equals (this Color32 a, Color32 b) {
		return (a.a == b.a)
			&& (a.r == b.r)
			&& (a.g == b.g)
			&& (a.b == b.b);
	}

	public static uint ToUIntRGB(this Color c) {
		return ((Color32)c).ToUIntRGB();
	}

	public static uint ToUIntRGB(this Color32 c) {
		return ((uint)c.b) | (((uint)c.g) << 8) | (((uint)c.r) << 16);
	}

	public static Color32 GetColor32FromUIntRGB(uint rgb) {
		return new Color32((byte)((rgb >> 16) & 0xff), (byte)((rgb >> 8) & 0xff), (byte)(rgb & 0xff), 0xff);
	}

	public static T[] ToList<T>(this ReadOnlyCollection<T> collection) {
		T[] t = new T[collection.Count];
		collection.CopyTo(t, 0);
		return t;
	}

	public static float ParseFloat(string str) {
		return float.Parse(str, System.Globalization.NumberStyles.Any);
	}

	public static double ParseDouble(string str) {
		return double.Parse(str, System.Globalization.NumberStyles.Any);
	}

	public static void SetRectInCanvas(this RectTransform g, Rect r) {
		var clientArea = g.GetParentRectInCavas();

		var aMin = g.anchorMin;
		var aMax = g.anchorMax;

		Rect anchors = Rect.MinMaxRect(
			Mathf.Lerp(clientArea.xMin, clientArea.xMax, aMin.x),
			Mathf.Lerp(clientArea.yMin, clientArea.yMax, aMin.y),
			Mathf.Lerp(clientArea.xMin, clientArea.xMax, aMax.x),
			Mathf.Lerp(clientArea.yMin, clientArea.yMax, aMax.y)
		);

		g.offsetMin = new Vector2(r.xMin - anchors.xMin, r.yMin - anchors.yMin);
		g.offsetMax = new Vector2(r.xMax - anchors.xMax, r.yMax - anchors.yMax);
	}

	public static void SetPosInCanvas(this RectTransform g, Vector2 p) {
		var size = g.sizeDelta;
		Rect r = new Rect(p.x - size.x/2, p.y - size.y/2, size.x, size.y);
		g.SetRectInCanvas(r);
	}

	public static void SetSizeInCanvas(this RectTransform g, Vector2 size) {
		Rect r = g.GetRectInCanvas();
		r.width = size.x;
		r.height = size.y;
		g.SetRectInCanvas(r);
	}

	public static Vector2 ScreenToCanvas(this Canvas g, Vector2 p) {
		var scaler = g.GetComponent<CanvasScaler>();
		if (scaler != null) {
			Vector2 r;
			r.x = p.x / Screen.width * scaler.referenceResolution.x;
			r.y = p.y / g.scaleFactor;
			return r;
		} else {
			return p / g.scaleFactor;
		}
	}

	public static Vector2 GetPosInCanvas(this RectTransform g) {
		var rect = g.GetRectInCanvas();
		return rect.position;
	}

	public static Rect GetRectInCanvas(this RectTransform g) {
		var clientArea = g.GetParentRectInCavas();
		var aMin = g.anchorMin;
		var aMax = g.anchorMax;

		Rect anchors = Rect.MinMaxRect(
			Mathf.Lerp(clientArea.xMin, clientArea.xMax, aMin.x),
			Mathf.Lerp(clientArea.yMin, clientArea.yMax, aMin.y),
			Mathf.Lerp(clientArea.xMin, clientArea.xMax, aMax.x),
			Mathf.Lerp(clientArea.yMin, clientArea.yMax, aMax.y)
        );

		var ofsMin = g.offsetMin;
		var ofsMax = g.offsetMax;

		return Rect.MinMaxRect(
			anchors.xMin + ofsMin.x,
			anchors.yMin + ofsMin.y,
			anchors.xMax + ofsMax.x,
			anchors.yMax + ofsMax.y
		);
	}
	
	public static Rect GetCanvasPixelRect(this Canvas c) {
		var px = c.pixelRect;
		var scaler = c.GetComponent<CanvasScaler>();
		if (scaler != null) {
			return new Rect(0, 0, scaler.referenceResolution.x, px.height / c.scaleFactor);
		} else {
			return new Rect(0, 0, px.width / c.scaleFactor, px.height / c.scaleFactor);
		}
	}

	public static Rect GetParentRectInCavas(this Graphic g) {
		if (g.transform.parent == g.canvas.transform) {
			return g.canvas.GetCanvasPixelRect();
		}

		var t = g.transform.parent;
		var r = t.GetComponent<RectTransform>();
		while (r == null) {
			t = t.parent;
			r = t.GetComponent<RectTransform>();
		}

		return r.GetRectInCanvas();

	}

	public static Rect GetParentRectInCavas(this RectTransform g) {
		var cc = g.transform.parent.GetComponent<Canvas>();
		if (cc != null) {
			var px = cc.pixelRect;
			var scaler = g.transform.parent.GetComponent<CanvasScaler>();
			if (scaler != null) {
				return new Rect(0, 0, scaler.referenceResolution.x, px.height / cc.scaleFactor);
			} else {
				return new Rect(0, 0, px.width / cc.scaleFactor, px.height / cc.scaleFactor);
			}
		}

		var t = g.transform.parent;
		var r = t.GetComponent<RectTransform>();
		while (r == null) {
			t = t.parent;
			r = t.GetComponent<RectTransform>();
		}

		return r.GetRectInCanvas();
	}

	public static void SetPosInCanvasRelative(this RectTransform g, RectTransform relative, Vector2 p) {
		var relPos = relative.GetPosInCanvas();
		g.SetPosInCanvas(p + relPos);
	}

	public static void SetRectInCanvasRelative(this RectTransform g, RectTransform relative, Rect r) {
		var relPos = relative.GetPosInCanvas();
		r.x += relPos.x;
		r.y += relPos.y;
		g.SetRectInCanvas(r);
	}

	public static Vector2 GetCenterOfWidgetInCanvas(this RectTransform g) {
		var r = g.GetRectInCanvas();
		return new Vector2(r.x + (r.width / 2f), r.y + (r.height / 2));
	}

	public static void CenterWidgetInParent(this RectTransform g) {
		var r = g.GetRectInCanvas();
		var p = g.GetParentRectInCavas();
		r.x = p.x + ((p.width - r.width) / 2f);
		r.y = p.y + ((p.height - r.height) / 2f);
		g.SetRectInCanvas(r);
	}

	public static void CopyRectTransform(this RectTransform rt, RectTransform other) {
		rt.anchorMin = other.anchorMin;
		rt.anchorMax = other.anchorMax;
		rt.offsetMin = other.offsetMin;
		rt.offsetMax = other.offsetMin;
		rt.anchoredPosition = other.anchoredPosition;
		rt.pivot = other.pivot;
		rt.sizeDelta = other.sizeDelta;
		rt.localScale = other.localScale;
		rt.position = other.position;
	}

	public static TextureFormat ToTextureFormat(this RenderTextureFormat f) {
		switch (f) {
			case RenderTextureFormat.ARGB32:
				return TextureFormat.ARGB32;
			case RenderTextureFormat.ARGB4444:
				return TextureFormat.ARGB4444;
			case RenderTextureFormat.RGB565:
				return TextureFormat.RGB565;

		}

		throw new System.InvalidCastException("No conversion from RenderTextureFormat." + f + " to TextureFormat");
	}

	public static RenderTextureFormat ToRenderTextureFormat(this TextureFormat f) {
		switch (f) {
			case TextureFormat.ARGB32:
				return RenderTextureFormat.ARGB32;
			case TextureFormat.ARGB4444:
				return RenderTextureFormat.ARGB4444;
			case TextureFormat.RGB565:
				return RenderTextureFormat.RGB565;
		}

		throw new System.InvalidCastException("No conversion from TextureFormat." + f + " to RenderTextureFormat");
	}

	public static bool Touches(this Rect r, Rect x) {
		if (r.xMax < x.xMin) {
			return false;
		}
		if (r.xMin > x.xMax) {
			return false;
		}
		if (r.yMax < x.yMin) {
			return false;
		}
		if (r.yMin > x.yMax) {
			return false;
		}
		return true;
	}

	public static bool GetBoundsScreenRect(Camera camera, Bounds bounds, out Rect rect) {
		int numProjected = 0;

		float xMin = float.MaxValue;
		float xMax = float.MinValue;
		float yMin = float.MaxValue;
		float yMax = float.MinValue;

		var boundsMin = bounds.min;
		var boundsMax = bounds.max;

		for (int xi = 0; xi < 2; ++xi) {
			float x = (xi == 0) ? boundsMin.x : boundsMax.x;
			for (int yi = 0; yi < 2; ++yi) {
				float y = (yi == 0) ? boundsMin.y : boundsMax.y;
				for (int zi = 0; zi < 2; ++zi) {
					float z = (zi == 0) ? boundsMin.z : boundsMax.z;

					Vector3 proj = camera.WorldToScreenPoint(new Vector3(x, y, z));
					if (proj.z > 0f) {
						++numProjected;
						xMin = Mathf.Min(xMin, proj.x);
						xMax = Mathf.Max(xMax, proj.x);
						yMin = Mathf.Min(yMin, proj.y);
						yMax = Mathf.Max(yMax, proj.y);
					}
				}
			}
		}

		if (numProjected >= 3) {
			rect = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
			return true;
		}

		rect = new Rect();
		return false;
	}

	public static Vector2 DividedBy(this Vector2 v, Vector2 other) {
		return new Vector2(v.x / other.x, v.y / other.y);
	}

	public static Vector2 MultipliedBy(this Vector2 v, Vector2 other) {
		return new Vector2(v.x * other.x, v.y * other.y);
	}

	public static Vector2 Recip(this Vector2 v) {
		return new Vector2(1f / v.x, 1f / v.y);
	}

	public static float SignedMinAngleDelta(float a, float b) {
		a = NormalizeAngle(a);
		b = NormalizeAngle(b);

		float d = a - b;

		if (d < -180) {
			d = -360 - d;
		} else if (d > 180) {
			d = 360 - d;
		}

		return d;
	}

	public static float AngleLerpShortestPath(float a, float b, float t) {
		a = NormalizeAngle(a);
		b = NormalizeAngle(b);

		float d = b - a;

		if (d < -180) {
			b = a + (360 + d);
		} else if (d > 180) {
			b = a - (360 - d);
		}

		return NormalizeAngle(Mathf.Lerp(a, b, t));
	}

	public static float NormalizeAngle(float a) {
		while (a >= 360) {
			a -= 360;
		}
		while (a < 0) {
			a += 360;
		}
		return a;
	}

	[System.Diagnostics.Conditional("UNITY_EDITOR")]
	public static void DebugDrawArc(Vector3 pos, Vector3 dirNml, Vector3 up, float degrees, Color color, float radius, int numArcs, float duration, bool depthTest) {
		var step = degrees/numArcs;
		var angle = step;

		var start = pos+dirNml*radius;
		var prev = start;

		for (int i = 1; i < numArcs; ++i) {
			var cur = pos+(Quaternion.AngleAxis(angle, up)*dirNml)*radius;
			Debug.DrawLine(prev, cur, color, duration);
			angle += step;
			prev = cur;
		}

		Debug.DrawLine(prev, start, color, duration, depthTest);
	}

	public static double ReadSeconds() {
		return ((double)System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
	}

	static readonly long StopWatchFrequencyPerTick = System.Diagnostics.Stopwatch.Frequency;
	static readonly long StopWatchFrequencyPerMilli = StopWatchFrequencyPerTick / 1000;
	static readonly long StopWatchFrequencePerMicro = StopWatchFrequencyPerTick / 1000000;

	public static uint ReadMilliseconds() {
		var ticks = System.Diagnostics.Stopwatch.GetTimestamp();
		return (uint)((ticks / StopWatchFrequencyPerMilli) & uint.MaxValue);
	}

	public static uint ReadMicroseconds() {
		var ticks = System.Diagnostics.Stopwatch.GetTimestamp();
		return (uint)((ticks / StopWatchFrequencePerMicro) & uint.MaxValue);
	}

	public static void FadeOutAndStop(this AudioSource source, float fadeOutTime) {
		if (source.isPlaying) {
			if (fadeOutTime > 0f) {
				source.gameObject.DestroyComponent<AudioSourceFadeOutAndStop>();
				var t = source.gameObject.AddComponent<AudioSourceFadeOutAndStop>();
				t.fadeTime = fadeOutTime;
			} else {
				source.Stop();
			}
		}
	}

#if UNITY_EDITOR
	public static string[] tagNames {
		get {
			return InternalEditorUtility.tags;
		}
	}

	public static string[] layerNames {
		get {
			return InternalEditorUtility.layers;
		}
	}

	public static string currentlySelectedProjectPath {
		get {
			// http://forum.unity3d.com/threads/how-to-get-currently-selected-folder-for-putting-new-asset-into.81359/
			string path = "Assets";
			foreach (var obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets)) {
				path = AssetDatabase.GetAssetPath(obj);
				if (System.IO.File.Exists(path)) {
					path = System.IO.Path.GetDirectoryName(path);
				}
				break;
			}
			return path;
		}
	}

	static string _projectRootDir = null;

	// Returns the fully qualified unity project root folder (contains the Assets directory).
	public static string projectRootDirectory {
		get {
			if (_projectRootDir == null) {
				_projectRootDir = Application.dataPath;
				if (_projectRootDir.EndsWith("/Assets")) {
					_projectRootDir = _projectRootDir.Substring(0, _projectRootDir.Length - "/Assets".Length);
				}
			}
			return _projectRootDir;
		}
	}

	public static string SafeGetNewAssetPathFromName(string baseName) {
		var curAssetPath = currentlySelectedProjectPath;
		var testPath = curAssetPath + "/" + baseName + ".asset";
		if (!System.IO.File.Exists(testPath)) {
			return testPath;
		}

		for (int i = 1; ; ++i) {
			testPath = curAssetPath + "/" + baseName + i + ".asset";
			if (!System.IO.File.Exists(testPath)) {
				break;
			}
		}

		return testPath;
	}

	public static string SafeGetNewAssetPathFromPath(string pathName) {
		var testPath = pathName + ".asset";
		if (!System.IO.File.Exists(testPath)) {
			return testPath;
		}

		for (int i = 1; ; ++i) {
			testPath = pathName + i + ".asset";
			if (!System.IO.File.Exists(testPath)) {
				break;
			}
		}

		return testPath;
	}

	public static T CreateAsset<T>() where T : ScriptableObject, new() {
		return CreateAsset<T>(SafeGetNewAssetPathFromName("New" + typeof(T).Name));
    }

	public static T CreateAsset<T>(string path) where T : ScriptableObject, new() {
		var asset = ScriptableObject.CreateInstance<T>();
		AssetDatabase.CreateAsset(asset, path);
		AssetDatabase.SaveAssets();
		EditorUtility.FocusProjectWindow();
		Selection.activeObject = asset;
		return asset;
	}

	public static object GetValue(this SerializedProperty property) {
		// descend into the target object via the property path

		var path = property.propertyPath.Replace(".Array.data[", "[");
		object obj = property.serializedObject.targetObject;
		var elems = path.Split('.');

		for (int i = 0; i < elems.Length; ++i) {
			var e = elems[i];

			var index = e.IndexOf("[");

			if (index != -1) {
				var n = e.Substring(0, index);
				var p = e.Substring(index).Replace("[", "").Replace("]", "");
				var x = System.Convert.ToInt32(p);
				obj = GetValue(obj, n, x);
			} else {
				obj = GetValue(obj, e);
			}

			if (obj == null) {
				break;
			}
		}

		return obj;
	}

	static object GetValue(object obj, string name) {
		for (var type = obj.GetType(); type != typeof(object); type = type.BaseType) {
			var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			if (f != null) {
				return f.GetValue(obj);
			}
		}
		return null;
	}

	static object GetValue(object obj, string name, int index) {
		var list = GetValue(obj, name) as IList;
		if ((list != null) && (index < list.Count)) {
			return list[index];
		}
		return null;
	}

	struct SetValueStackItem {
		public object obj;
		public string name;
		public int index;
	}

	public static void SetValue(this SerializedProperty property, object value) {
		// descend into the target object via the property path

		var path = property.propertyPath.Replace(".Array.data[", "[");
		object obj = property.serializedObject.targetObject;
		var elems = path.Split('.');

		var stack = new List<SetValueStackItem>();

		for (int i = 0; i < elems.Length; ++i) {
			var e = elems[i];

			var set = i == (elems.Length - 1);

			var index = e.IndexOf("[");

			if (index != -1) {
				var n = e.Substring(0, index);
				var p = e.Substring(index).Replace("[", "").Replace("]", "");
				var x = System.Convert.ToInt32(p);
				if (set) {
					SetValue(obj, n, x, value);
				} else {
					stack.Add(new SetValueStackItem() {
						obj = obj,
						name = n,
						index = x
					});
					obj = GetValue(obj, n, x);
				}
			} else {
				if (set) {
					SetValue(obj, e, value);
				} else {
					stack.Add(new SetValueStackItem() {
						obj = obj,
						name = e,
						index = -1
					});
					obj = GetValue(obj, e);
				}
			}
		}

		for (int i = stack.Count-1; i >= 0; --i) {
			var s = stack[i];
			if (obj.GetType().IsValueType) {
				if (s.index != -1) {
					SetValue(s.obj, s.name, s.index, obj);
				} else {
					SetValue(s.obj, s.name, obj);
				}
			}
			obj = s.obj;
		}
	}

	static void SetValue(object obj, string name, object value) {
		var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
		if (f != null) {
			f.SetValue(obj, value);
		}
	}

	static void SetValue(object obj, string name, int index, object value) {
		var list = GetValue(obj, name) as IList;
		if (list != null) {
			list[index] = value;
		}
	}

	const string RESOURCES = "/Resources/";

	public static string GetResourceRelativePath(string path) {
		var idx = path.IndexOf(RESOURCES);
		if (idx == -1) {
			return string.Empty;
		}
		var pathStart = idx + RESOURCES.Length;
		var newPath = path.Substring(pathStart);

		var extIdx = newPath.LastIndexOf('.');
		if (extIdx != -1) {
			newPath = newPath.Remove(extIdx);
		}

		return newPath;
	}

#endif
}
