using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead;

namespace Bowhead {
	using Bowhead.Actors;
    public class DataManager {

        private static Dictionary<System.Type, Dictionary<string, EntityData>> _dataByClass = new Dictionary<System.Type, Dictionary<string, EntityData>>();
        private static Dictionary<string, EntityData> _allData = new Dictionary<string, EntityData>();

        public static T GetData<T>(string name) where T : EntityData {
            Dictionary<string, EntityData> classLookup;
            var t = typeof(T);
            if (_dataByClass.TryGetValue(t, out classLookup)) {
                EntityData data;
                if (classLookup.TryGetValue(name.ToLowerInvariant(), out data)) {
                    return (T)data;
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

        public static void InitData() {

            foreach (var d in StaticData.GetAllStaticAssets<PawnData>()) {
                Add(d, typeof(PawnData));
            }

            foreach (var d in StaticData.GetAllStaticAssets<InteractableData>()) {
                Add(d);
            }

            foreach (var d in StaticData.GetAllStaticAssets<ItemData>()) {
                Add(d, typeof(ItemData));
            }
        }
    }
}
