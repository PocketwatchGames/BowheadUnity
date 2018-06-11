// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(ActorSpawnTag), true)]
public class ActorSpawnTagInspector : ActorSpawnTagInspector<Actor> { }

public abstract class ActorSpawnTagInspector<T> : ActorSpawnTagInspectorBase<ActorSpawnTagInspectorTypes<T>> { }

public sealed class ActorSpawnTagInspectorTypes<T> : ClassDropdownTypes {
	static readonly Type[] _types = new[] { typeof(T) };

	public Type[] types {
		get {
			return _types;
		}
	}
}

public abstract class ActorSpawnTagInspectorBase<T> : Editor where T : ClassDropdownTypes, new() {

	public override void OnInspectorGUI() {
		DrawInspectorGUI();
	}

	protected virtual void DrawInspectorGUI() {
		ActorSpawnTagInspectorHelper<T>.DrawSpawnTagInspectorGUI((ActorSpawnTag)target);
	}
}

public static class ActorSpawnTagInspectorHelper<T> where T : ClassDropdownTypes, new() {
	public static void DrawSpawnTagInspectorGUI(ActorSpawnTag tag) {
		GUI.enabled = false;
		EditorGUILayout.IntField("SpawnId", tag.spawnID);
		var changed = false;
		GUI.enabled = !tag.typeIsReadOnly;
		var typeName = ClassDropDownHelper<T>.ShowClassDropDown("Actor Class", tag.typeName);
		if (tag.typeName != typeName) {
			tag.typeName = typeName;
			changed = true;
		}
		GUI.enabled = !tag.replicatesIsReadOnly;
		var replicates = EditorGUILayout.Toggle("Replicates", tag.replicates);
		if (tag.replicates != replicates) {
			tag.replicates = replicates;
			changed = true;
		}
		GUI.enabled = !tag.staticSpawnIsReadOnly;
		var staticSpawn = EditorGUILayout.Toggle("Static", tag.staticSpawn);
		if (tag.staticSpawn != staticSpawn) {
			tag.staticSpawn = staticSpawn;
			changed = true;
		}
		GUI.enabled = true;

		if (changed) {
			EditorUtility.SetDirty(tag);
		}
	}
}