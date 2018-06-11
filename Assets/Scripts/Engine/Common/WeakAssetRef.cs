// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

[Serializable]
public sealed class Sprite_WRef : WeakAssetRef<Sprite> { }

[Serializable]
public sealed class Texture_WRef : WeakAssetRef<Texture> { }

[Serializable]
public sealed class Texture2D_WRef : WeakAssetRef<Texture2D> { }

[Serializable]
public sealed class Texture3D_WRef : WeakAssetRef<Texture3D> { }

[Serializable]
public sealed class GameObject_WRef : WeakAssetRef<GameObject> { }

[Serializable]
public sealed class ParticleSystem_WRef : WeakAssetRef<ParticleSystem> { }

[Serializable]
public sealed class Material_WRef : WeakAssetRef<Material> { }

public abstract class WeakAssetRef<T> : WeakAssetRef where T : UnityEngine.Object {

	T _loaded;

	public T Load() {
		if (_loaded == null) {
			if (!string.IsNullOrEmpty(_assetPath)) {
				_loaded = Resources.Load<T>(_assetPath);
			}
		}
		return _loaded;
	}

	public void Release() {
		_loaded = null;
	}

	public static bool operator == (WeakAssetRef<T> a, WeakAssetRef<T> b) {
		if (ReferenceEquals(a, b)) {
			return true;
		}
		if (ReferenceEquals(b, null)) {
			return string.IsNullOrEmpty(a._assetPath);
		}
		if (ReferenceEquals(a, null)) {
			return string.IsNullOrEmpty(b._assetPath);
		}
		return false;
	}

	public static bool operator != (WeakAssetRef<T> a, WeakAssetRef<T> b) {
		return !(a == b);
	}

	public override bool Equals(object obj) {
		if (obj == null) {
			return this == null;
		}
		var r = obj as WeakAssetRef<T>;
		if (r != null) {
			return this == r;
		}
		return false;
	}

	public override int GetHashCode() {
		return base.GetHashCode();
	}

	protected override void Precache() {
		Load();
	}

#if UNITY_EDITOR
	public override Type inspector_assetType {
		get {
			return typeof(T);
		}
	}

	public T inspector_Resource {
		get {
			return _loaded;
		}
	}

	public override void InspectorFlush() {
		base.InspectorFlush();
		Release();
	}
#endif
}

public abstract class WeakAssetRef {
	[HideInInspector]
	[SerializeField]
	protected string _assetPath;
	[HideInInspector]
	[SerializeField]
	protected string _editorPath;

	bool _precache;

	public static void Precache(WeakAssetRef assetRef) {
		if (assetRef != null) {
			assetRef.Precache();
		}
	}

	public static void Precache<T>(WeakAssetRef<T> assetRef, Action<T> f) where T : UnityEngine.Object {
		if ((assetRef != null) && !assetRef._precache) {
			assetRef._precache = true;
			var t = assetRef.Load();
			if (t != null) {
				f(t);
			}
			assetRef._precache = false;
		}
	}

	public static void Precache<T>(IList<WeakAssetRef<T>> assetRefs) where T : UnityEngine.Object {
		if (assetRefs != null) {
			for (int i = 0; i < assetRefs.Count; ++i) {
				Precache(assetRefs[i]);
			}
		}
	}

	public static void Precache<T>(IList<WeakAssetRef<T>> assetRefs, Action<T> f) where T : UnityEngine.Object {
		if (assetRefs != null) {
			for (int i = 0; i < assetRefs.Count; ++i) {
				Precache(assetRefs[i], f);
			}
		}
	}

	public static T TryLoad<T>(WeakAssetRef<T> assetRef) where T: UnityEngine.Object {
		if (assetRef != null) {
			return assetRef.Load();
		}
		return null;
	}

	protected abstract void Precache();

#if UNITY_EDITOR
	public abstract Type inspector_assetType { get; }

	public virtual void InspectorFlush() { }

	public string inspectorAssetPath {
		get {
			return _assetPath;
		}
	}
	
	public string inspectorEditorPath {
		get {
			return _editorPath;
		}
	}

	public void InspectorUpdate(string editorPath, string assetPath) {
		_editorPath = editorPath;
		_assetPath = assetPath;
		InspectorFlush();
	}

    public bool InspectorConditionalAssetMoved(string prevEditorPath, string newEditorPath) {
		if (prevEditorPath == _editorPath) {
			var newAssetPath = Utils.GetResourceRelativePath(newEditorPath);
			if (string.IsNullOrEmpty(newAssetPath)) {
				InspectorUpdate(string.Empty, string.Empty);
			} else {
				InspectorUpdate(newEditorPath, newAssetPath);
			}
            return true;
		}
		return false;
	}

	public bool InspectorConditionalAssetDeleted(string prevEditorPath) {
		if (prevEditorPath == _editorPath) {
			InspectorUpdate(string.Empty, string.Empty);
            return true;
		}
		return false;
	}
#endif
}

