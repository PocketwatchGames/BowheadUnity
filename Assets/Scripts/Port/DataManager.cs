using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class DataManager {

        private static Dictionary<System.Type, Dictionary<string, EntityData>> _allData = new Dictionary<System.Type, Dictionary<string, EntityData>>();
        private static Dictionary<System.Type, Dictionary<string, ItemData>> _allItemData = new Dictionary<System.Type, Dictionary<string, ItemData>>();
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
        public static T GetItemData<T>(string name) where T : ItemData {
            Dictionary<string, ItemData> classLookup;
            if (_allItemData.TryGetValue(typeof(T), out classLookup)) {
                ItemData data;
                if (classLookup.TryGetValue(name, out data)) {
                    return data as T;
                }
            }
            return null;
        }
        public static T GetPrefab<T>(string name) where T : MonoBehaviour {
            GameObject o;
            if (_entityPrefabs.TryGetValue(name, out o)) {
                return o.GetComponent<T>();
            }
            return null;
        }

        public static void Add(EntityData d) {

            Dictionary<string, EntityData> classLookup;
            if (!_allData.TryGetValue(d.GetType(), out classLookup)) {
                classLookup = new Dictionary<string, EntityData>();
                _allData.Add(d.GetType(), classLookup);
            }
            classLookup.Add(d.name, d);

        }

        public static void Add(ItemData d) {

            Dictionary<string, ItemData> classLookup;
            if (!_allItemData.TryGetValue(d.GetType(), out classLookup)) {
                classLookup = new Dictionary<string, ItemData>();
                _allItemData.Add(d.GetType(), classLookup);
            }
            classLookup.Add(d.name, d);

        }

        public static void initData() {
            var entities = Resources.LoadAll("Data/Entities");
            foreach (var d in entities) {
                Add(d as EntityData);
            }

            var items = Resources.LoadAll("Data/Items");
            foreach (var d in items) {
                Add(d as ItemData);
            }

            var prefabs = Resources.LoadAll<GameObject>("Prefabs");
            foreach (var p in prefabs) {
                _entityPrefabs.Add(p.name, p);
            }
        }
    }
}
