// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RagdollAssetBuilder : EditorWindow {

	Transform root;
	Object folder;
	RagdollAsset ragdoll;

	void OnGUI() {
		EditorGUILayout.BeginVertical();

		root = (Transform)EditorGUILayout.ObjectField("Ragdoll Root", root, typeof(Transform), true);
		folder = EditorGUILayout.ObjectField("Destination Folder", folder, typeof(Object), false);
		ragdoll = (RagdollAsset)EditorGUILayout.ObjectField("Ragdoll to Update (Optional)", ragdoll, typeof(RagdollAsset), false);

		GUILayoutHelpers.HorzLine();

		GUI.enabled = (root != null) && ((ragdoll != null) || (folder != null));

		if (GUILayout.Button((ragdoll != null) ? "Update Ragdoll Asset" : "Create Ragdoll Asset")) {
			if (ragdoll != null) {
				ragdoll.Rebuild(root);
            } else {
				if (RagdollAsset.Create(root, Utils.SafeGetNewAssetPathFromPath(AssetDatabase.GetAssetPath(folder) + "/NewRagdoll")) == null) {
					EditorUtility.DisplayDialog("Error", "No ragdoll components were found from the specified root.", "OK");
				}
			}
        }

		GUI.enabled = true;

		EditorGUILayout.EndVertical();
	}
	
	[MenuItem("Bowhead/Ragdoll Builder...")]
	static void Menu() {
		EditorWindow.GetWindow<RagdollAssetBuilder>().titleContent.text = "Ragdoll Builder";
	}
}
