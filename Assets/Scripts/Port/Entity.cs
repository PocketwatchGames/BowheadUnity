using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Entity {

        public class CState {
        }

        public class CData {
            public string Name;
        }

        public World world;

        CData _data;
        CState _state;

        public CData Data { get { return _data; } }
        public CState State { get { return _state; } }

        private static Dictionary<System.Type, Dictionary<string, CData>> _allData;

        public static T GetData<T>(string name) where T : CData {
            Dictionary<string, CData> classLookup;
            if (_allData.TryGetValue(typeof(T), out classLookup)) {
                CData data;
                if (classLookup.TryGetValue(name, out data)) {
                    return data as T;
                }
            }
            return null;
        }
        protected T GetData<T>() where T : CData {
            return _data as T;
        }
        protected T GetState<T>() where T : CState {
            return _state as T;
        }

        public static T createData<E, T>(string name) where T : CData, new() {

            var i = new T();
            i.Name = name;

            Dictionary<string, CData> classLookup;
            if (!_allData.TryGetValue(typeof(E), out classLookup)) {
                classLookup = new Dictionary<string, CData>();
                _allData.Add(typeof(E), classLookup);
            }

		    return i;
	    }

        protected Entity(CData d, CState s) {
            _data = d;
            _state = s;
        }

    }
}