// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TagsAssetBuilder : EditorWindow {

	Transform root;
	Object folder;
	TagsAsset tags;

	void OnGUI() {
		EditorGUILayout.BeginVertical();

		root = (Transform)EditorGUILayout.ObjectField("Hierarchy Root", root, typeof(Transform), true);
		folder = EditorGUILayout.ObjectField("Destination Folder", folder, typeof(Object), false);
		tags = (TagsAsset)EditorGUILayout.ObjectField("Tags to Update (Optional)", tags, typeof(TagsAsset), false);

		GUILayoutHelpers.HorzLine();

		GUI.enabled = (root != null) && ((tags != null) || (folder != null));

		if (GUILayout.Button((tags != null) ? "Update Tags Asset" : "Create Tags Asset")) {
			if (tags != null) {
				tags.Rebuild(root);
            } else {
				if (TagsAsset.Create(root, Utils.SafeGetNewAssetPathFromPath(AssetDatabase.GetAssetPath(folder) + "/NewTags")) == null) {
					EditorUtility.DisplayDialog("Error", "Couldn't create tags asset.", "OK");
				}
			}
        }

		GUI.enabled = true;

		EditorGUILayout.EndVertical();
	}
	
	[MenuItem("Bowhead/Tags Builder...")]
	static void Menu() {
		EditorWindow.GetWindow<TagsAssetBuilder>().titleContent.text = "Tags Builder";
	}
}
