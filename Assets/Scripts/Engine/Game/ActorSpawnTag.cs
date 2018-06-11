// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

public class ActorSpawnTag<T> : ActorSpawnTag where T : Actor {

	public ActorSpawnTag() : base(typeof(T)) { }

}

public class ActorSpawnTag : ActorReference {
	[SerializeField]
	int _spawnID;

	[HideInInspector]
	public string typeName;
	[HideInInspector]
	public bool replicates;
	[HideInInspector]
	public bool staticSpawn;

	[NonSerialized]
	[HideInInspector]
	public bool typeIsReadOnly;

	[NonSerialized]
	[HideInInspector]
	public bool replicatesIsReadOnly;

	[NonSerialized]
	[HideInInspector]
	public bool staticSpawnIsReadOnly;

	[NonSerialized]
	[HideInInspector]
	public ActorSpawnTag original;

	Type _type;

	public bool dynamicSpawn {
		get {
			return !staticSpawn;
		}
	}

	public bool isInstance {
		get;
		set;
	}

	public bool clone {
		get;
		set;
	}

#if UNITY_EDITOR
		
	[InitializeOnLoadMethod]
	static void EditorStaticRun() {
//		AssignUniqueIDs();
		EditorApplication.hierarchyWindowChanged += AssignUniqueIDs;
	}

	static void AssignUniqueIDs() {
		if (!EditorApplication.isPlayingOrWillChangePlaymode) {
			var sceneTags = new Dictionary<Scene, List<ActorSpawnTag>>();

			{
				var tags = GameObject.FindObjectsOfType<ActorSpawnTag>();
				for (int i = 0; i < tags.Length; ++i) {
					var tag = tags[i];

					List<ActorSpawnTag> tagList;
					if (!sceneTags.TryGetValue(tag.gameObject.scene, out tagList)) {
						tagList = new List<ActorSpawnTag>();
						sceneTags[tag.gameObject.scene] = tagList;
					}

					tagList.Add(tag);
				}
			}

			if (sceneTags.Count > 1) {
				Debug.LogError("More than one scene with actors is open! Spawn indices will NOT be generated.");
			} else {
				foreach (var pair in sceneTags) {

					bool dirty = false;
					HashSet<int> usedIds = new HashSet<int>();
					
					// pick an availableID that doesn't cause a massive
					// reindex of the entire map.

					for (int i = 0; i < pair.Value.Count; ++i) {
						var tag = pair.Value[i];

						if (tag.spawnID > 0) {
							usedIds.Add(tag._spawnID);
						}
					}

					int firstAvailableId = 1;

					while (usedIds.Contains(firstAvailableId)) {
						++firstAvailableId;
					}

					usedIds.Clear();

					for (int i = 0; i < pair.Value.Count; ++i) {

						var tag = pair.Value[i];
						
						if ((tag.spawnID == 0) || usedIds.Contains(tag.spawnID)) {
							var oldID = tag.spawnID;
							tag._spawnID = firstAvailableId;

							if (oldID == 0) {
								Debug.Log("New actor " + tag.transform.GetPath() + " assigned spawnID = " + tag.spawnID);
							} else {
								Debug.LogWarning("Existing actor " + tag.transform.GetPath() + " spawnID had a conflict and was changed from " + oldID + " to " + tag.spawnID);
							}

							EditorUtility.SetDirty(tag);
							dirty = true;
						}

						usedIds.Add(tag._spawnID);

						while (usedIds.Contains(firstAvailableId)) {
							++firstAvailableId;
						}
					}

					if (dirty) {
						UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pair.Key);
					}
				}
			}
		}
	}
#endif

	public ActorSpawnTag() { }

	public ActorSpawnTag(bool isStatic, bool clone) {
		staticSpawn = isStatic;
		staticSpawnIsReadOnly = true;
		this.clone = clone;
	}

	public ActorSpawnTag(Type restrictedType) {
		typeName = restrictedType.FullName;
		typeIsReadOnly = true;
	}

    public ActorSpawnTag(Type restrictedType, bool isStatic, bool clone) {
		typeName = restrictedType.FullName;
		typeIsReadOnly = true;
		staticSpawn = isStatic;
		staticSpawnIsReadOnly = true;
		this.clone = clone;
	}
	
	protected override void Awake() {
		base.Awake();
		if (typeName != null) {
			_type = Type.GetType(typeName);
		}
	}

	public int spawnID {
		get {
			return _spawnID;
		}
	}

	public Type type {
		get {
			return _type;
		}
	}
}
