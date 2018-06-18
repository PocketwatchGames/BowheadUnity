using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead;

namespace Port {
    public class DataManager {

        private static Dictionary<System.Type, Dictionary<string, EntityData>> _dataByClass = new Dictionary<System.Type, Dictionary<string, EntityData>>();
        private static Dictionary<string, EntityData> _allData = new Dictionary<string, EntityData>();
        private static Dictionary<string, GameObject> _entityPrefabs = new Dictionary<string, GameObject>();

        public static T GetData<T>(string name) where T : EntityData {
            Dictionary<string, EntityData> classLookup;
            var t = typeof(T);
            if (_dataByClass.TryGetValue(t, out classLookup)) {
                EntityData data;
                if (classLookup.TryGetValue(name.ToLowerInvariant(), out data)) {
                    return data as T;
                }
            }
            return null;
        }
        public static EntityData GetData(string name) {
            EntityData data;
            if (_allData.TryGetValue(name.ToLowerInvariant(), out data)) {
                return data;
            }
            return null;
        }
        public static T GetPrefab<T>(string name) where T : MonoBehaviour {
            GameObject o;
            if (_entityPrefabs.TryGetValue(name.ToLowerInvariant(), out o)) {
                return o.GetComponent<T>();
            }
            return null;
        }
        public static GameObject GetPrefab(string name) {
            GameObject o;
            if (_entityPrefabs.TryGetValue(name.ToLowerInvariant(), out o)) {
                return o;
            }
            return null;
        }


        public static void Add(EntityData d, System.Type baseType = null) {

            _allData.Add(d.name.ToLowerInvariant(), d);
            Dictionary<string, EntityData> classLookup;
            if (!_dataByClass.TryGetValue(d.GetType(), out classLookup)) {
                classLookup = new Dictionary<string, EntityData>();
                _dataByClass.Add(d.GetType(), classLookup);
            }
            classLookup.Add(d.name.ToLowerInvariant(), d);

            if (baseType != null) {
                Dictionary<string, EntityData> baseClassLookup;
                if (!_dataByClass.TryGetValue(baseType, out baseClassLookup)) {
                    baseClassLookup = new Dictionary<string, EntityData>();
                    _dataByClass.Add(baseType, baseClassLookup);
                }
                baseClassLookup.Add(d.name.ToLowerInvariant(), d);
            }

        }

        public static void initData() {
           
            var actors = Resources.LoadAll("Data/Actors");
            foreach (var d in actors) {
                Add(d as EntityData, typeof(ActorData));
            }

            var items = Resources.LoadAll("Data/Items");
            foreach (var d in items) {
                Add(d as EntityData, typeof(ItemData));
            }

            var prefabs = Resources.LoadAll<GameObject>("Prefabs");
            foreach (var p in prefabs) {
                _entityPrefabs.Add(p.name.ToLowerInvariant(), p);
            }
        }
    }
}
