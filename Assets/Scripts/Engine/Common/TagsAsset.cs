// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public sealed class TagsAsset_WRef : WeakAssetRef<TagsAsset> {}

public class TagsAsset : ScriptableObject {

	[SerializeField]
	List<Element> elements;

#if UNITY_EDITOR
	public static TagsAsset Create(Transform root, string path) {
		List<Element> elements = new List<Element>();

		AddTags(root, null, elements);

		TagsAsset asset = null;

		if (elements.Count > 0) {
			asset = Utils.CreateAsset<TagsAsset>(path);
			asset.elements = elements;
		}

		return asset;
	}

	static void AddTags(Transform root, string path, List<Element> elements) {
		for (int i = 0; i < root.childCount; ++i) {
			var child = root.GetChild(i);
			var childPath = child.name;

			if (!string.IsNullOrEmpty(path)) {
				childPath = path + "/" + childPath;
			}

			if (!child.gameObject.CompareTag("Untagged") && !child.gameObject.CompareTag("EditorOnly")) {
				var e = new Element();
				e.path = childPath;
				e.tag = child.gameObject.tag;
				elements.Add(e);
			}

			AddTags(child, childPath, elements);
		}
	}

	public void Rebuild(Transform root) {
		elements = new List<Element>();

		AddTags(root, null, elements);

		EditorUtility.SetDirty(this);
	}
#endif

	public void ApplyTags(Transform root) {
		
		if ((elements != null) && (elements.Count > 0)) {
			for (int i = 0; i < elements.Count; ++i) {
				var e = elements[i];
				var t = root.Find(e.path);
				if (t != null) {
					t.gameObject.tag = e.tag;
				}
			}
		}
	}

	[Serializable]
	struct Element {
		public string path;
		public string tag;
	}
}
