// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead {
	using Object = UnityEngine.Object;

	[CreateAssetMenu(menuName = "Bowhead/Static Data")]
	public class StaticData : ScriptableObject, ISerializationCallbackReceiver {
		public interface Indexed {
			int staticIndex {
				get;
#if UNITY_EDITOR
				set;
#endif
			}

			void ClientPrecache();
		}
		public GameObject defaultActorPrefab;
		public World_ChunkComponent serverTerrainChunkComponent;
		public RandomNumberTable randomNumberTable;
		public PhysicalContactMatrix physicalContactMatrix;

		//[HideInInspector]
		public Object[] indexedObjects;

		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() { }

#if UNITY_EDITOR
		void IndexAssets(string[] paths) {
			List<Object> indexedObjects = (this.indexedObjects != null) ? new List<Object>(this.indexedObjects) : new List<Object>();
			bool changed = false;

			foreach (var path in paths) {
				changed = IndexAssetAtPath(path, indexedObjects) || changed;
			}

			if (changed) {
				this.indexedObjects = indexedObjects.ToArray();
				EditorUtility.SetDirty(this);
			}
		}

		static bool IndexAssetAtPath(string path, List<Object> indexedObjects) {
			bool changed = false;

			var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
			if (obj != null) {
				var indexed = obj as Indexed;
				if (indexed != null) {

					bool added = false;

					if ((indexed.staticIndex >= indexedObjects.Count) || (indexedObjects[indexed.staticIndex] == null)) {
						while (indexed.staticIndex >= indexedObjects.Count) {
							indexedObjects.Add(null);
						}
						indexedObjects[indexed.staticIndex] = obj;
						changed = true;
					} else if (indexedObjects[indexed.staticIndex] != obj) { // collision, move this object
						for (int i = 0; i < indexedObjects.Count; ++i) {
							if (indexedObjects[i] == null) {
								indexedObjects[i] = obj;
								indexed.staticIndex = i;
								added = true;
								break;
							}
						}

						if (!added) {
							indexed.staticIndex = indexedObjects.Count;
							indexedObjects.Add(obj);
						}

						changed = true;
						EditorUtility.SetDirty(obj);
					}
				}
			}

			return changed;
		}

		static bool IndexAssetTypes(Type t, List<Object> indexedObjects) {
			bool changed = false;

			var guids = AssetDatabase.FindAssets("t:" + t.Name);
			if (guids.Length > 0) {
				var progress = new EditorProgressBar(null, guids.Length+1);
				foreach (var guid in guids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					progress.Step(path);
					changed = IndexAssetAtPath(path, indexedObjects) || changed;
				}
				progress.Step(null);
			}

			return changed;
		}

		[MenuItem("Bowhead/Re-index All Assets...")]
		public static void ReindexAll() {
			var gameManager = AssetDatabase.LoadAssetAtPath<GameManager>("Assets/Prefabs/GameManager.prefab");
			if (gameManager != null) {

				var progress = new EditorProgressBar("Re-indexing Bowhead Assets...", 3);

				List<Object> indexedObjects = (gameManager.staticData.indexedObjects != null) ? new List<Object>(gameManager.staticData.indexedObjects) : new List<Object>();
				bool changed;

				changed = IndexAssetTypes(typeof(StaticAsset), indexedObjects);
				progress.Step(null);
				changed = IndexAssetTypes(typeof(StaticVersionedAsset), indexedObjects) || changed;
				progress.Step(null);
				changed = IndexAssetTypes(typeof(StaticVersionedAssetWithSerializationCallback), indexedObjects) || changed;
				progress.Close();

				for (int i = 0; i < indexedObjects.Count; ++i) {
					var obj = (Indexed)indexedObjects[i];
					if ((obj != null) && (obj.staticIndex != i)) {
						indexedObjects[i] = null;
						changed = true;
					}
				}
								
				if (changed) {
					if (changed) {
						gameManager.staticData.indexedObjects = indexedObjects.ToArray();
					}
					EditorUtility.SetDirty(gameManager.staticData);
				}

			}
		}
#endif

		public static T FindStaticAssetNamed<T>(string name) where T : Object {
			T t = null;

			var indexedObjects = GameManager.instance.staticData.indexedObjects;
			if (indexedObjects != null) {
				for (int i = 0; i < indexedObjects.Length; ++i) {
					var obj = indexedObjects[i] as T;
					if ((obj != null) && (obj.name == name)) {
						return obj;
					}
				}
			}

			return t;
		}

		public static T[] GetAllStaticAssets<T>() where T : Object {
			List<T> assets = new List<T>();

			var indexedObjects = GameManager.instance.staticData.indexedObjects;
			if (indexedObjects != null) {
				for (int i = 0; i < indexedObjects.Length; ++i) {
					var obj = indexedObjects[i] as T;
					if (obj != null) {
						assets.Add(obj);
					}
				}
			}
			return assets.ToArray();
		}
	}

	public abstract class StaticAsset : ScriptableObject, StaticData.Indexed {
		[HideInInspector]
		[SerializeField]
		int _index;

		public int staticIndex {
			get {
				return _index;
			}
#if UNITY_EDITOR
			set {
				_index = value;
			}
#endif
		}

		public virtual void ClientPrecache() { }
	}

	public abstract class StaticVersionedAsset : VersionedObject, StaticData.Indexed {
		[HideInInspector]
		[SerializeField]
		int _index;

		public int staticIndex {
			get {
				return _index;
			}
#if UNITY_EDITOR
			set {
				_index = value;
			}
#endif
		}
	}

	public abstract class StaticVersionedAssetWithSerializationCallback : VersionedObjectWithSerializationCallback, StaticData.Indexed {
		[HideInInspector]
		[SerializeField]
		int _index;

		public int staticIndex {
			get {
				return _index;
			}
#if UNITY_EDITOR
			set {
				_index = value;
			}
#endif
		}
	}

	public interface SerializeStaticAssetRef {
		void Serialize(Archive archive);
		object Copy();
		bool Equals(SerializeStaticAssetRef other);
    }

	[ReplicatedUsing(typeof(StaticAssetRefSerializer))]
	public struct StaticAssetRef<T> : SerializeStaticAssetRef where T : Object, StaticData.Indexed {
		int _index;
		T _obj;

		public static implicit operator StaticAssetRef<T>(T indexed) {
			StaticAssetRef<T> r = new StaticAssetRef<T>();
			if (indexed != null) {
				r._index = (ushort)(indexed.staticIndex + 1);
				r._obj = (T)GameManager.instance.staticData.indexedObjects[indexed.staticIndex];
				Assert.IsTrue(ReferenceEquals(r._obj, indexed));
			}
			return r;
		}

		public static implicit operator T(StaticAssetRef<T> r) {
			return r.obj;
		}

		public void Serialize(Archive archive) {
			ushort id = (ushort)_index;
			archive.Serialize(ref id);
			_index = id;
			if (archive.isLoading) {
				if (_index != 0) {
					_obj = (T)GameManager.instance.staticData.indexedObjects[_index - 1];
				} else {
					_obj = null;
				}
			}
		}

		public object Copy() {
			StaticAssetRef<T> r = new StaticAssetRef<T>();
			r._index = _index;
			r._obj = _obj;
			return r;
		}

		public bool Equals(SerializeStaticAssetRef other) {
			return _index == ((StaticAssetRef<T>)other)._index;
		}

		public T obj {
			get {
				return _obj;
			}
		}
	}

	public class StaticAssetRefSerializer : SerializableObjectNonReferenceFieldSerializer<StaticAssetRefSerializer> {
		
		public override bool Serialize(Archive archive, ISerializableObjectReferenceCollector collector, ref object field, object lastFieldState) {
			SerializeStaticAssetRef r = (SerializeStaticAssetRef)field;
			r.Serialize(archive);
			field = r.Copy();
			return archive.isLoading;
		}

		public override bool FieldsAreEqual(object a, object b) {
			return (a != null) && (b != null) && ((SerializeStaticAssetRef)a).Equals(b);
		}

		public override object Copy(object toCopy) {
			return ((SerializeStaticAssetRef)toCopy).Copy();
		}

		public static bool Serialize<T>(Archive archive, ref T asset) where T : Object, StaticData.Indexed {
			StaticAssetRef<T> assetRef = asset;
			object boxed = assetRef;
			var r = instance.Serialize(archive, null, ref boxed, null);
			assetRef = (StaticAssetRef<T>)boxed;
			asset = assetRef.obj;
			return r;
		}
	}

#if UNITY_EDITOR
	public class StaticDataProcessor : AssetPostprocessor {
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
			if (importedAssets.Length > 0) {
				StaticData.ReindexAll();
			}
		}		
    }
#endif
}