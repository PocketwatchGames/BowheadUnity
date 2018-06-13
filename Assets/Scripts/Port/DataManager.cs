using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class DataManager {

        private static Dictionary<System.Type, Dictionary<string, EntityData>> _allData = new Dictionary<System.Type, Dictionary<string, EntityData>>();
        private static Dictionary<string, GameObject> _entityPrefabs = new Dictionary<string, GameObject>();

        public static T GetData<T>(string name) where T : EntityData {
            Dictionary<string, EntityData> classLookup;
            if (_allData.TryGetValue(typeof(T), out classLookup)) {
                EntityData data;
                if (classLookup.TryGetValue(name, out data)) {
                    return data as T;
                }
            }
            return null;
        }
        public static GameObject GetPrefab(string name) {
            GameObject o;
            if (_entityPrefabs.TryGetValue(name, out o)) {
                return o;
            }
            return null;
        }

        public static void Add(EntityData d) {

            Dictionary<string, EntityData> classLookup;
            if (!_allData.TryGetValue(d.GetType(), out classLookup)) {
                classLookup = new Dictionary<string, EntityData>();
                _allData.Add(d.GetType(), classLookup);
            }
            classLookup.Add(d.Name, d);

        }

        public static void initData() {
            var data = Resources.LoadAll("Data");
            foreach (var d in data) {
                Add(d as EntityData);
            }

            var prefabs = Resources.LoadAll<GameObject>("Prefabs");
            foreach (var p in prefabs) {
                _entityPrefabs.Add(p.name, p);
            }
        }
    }
}
