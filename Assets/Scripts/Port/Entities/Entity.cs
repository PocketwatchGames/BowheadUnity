using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    public class EntityData : ScriptableObject {
        public string Name { get { return this.name; } }
    }



    abstract public class Entity : MonoBehaviour {

        [HideInInspector]
        public World world;
        [SerializeField]
        private EntityData _data;

        public EntityData Data { get { return _data; } }

        virtual public D GetData<D>() where D : EntityData {
            return _data as D;
        }

        public static T createData<T>(string name) where T : EntityData, new() {

            var i = new T();

            DataManager.Add(i);

		    return i;
	    }

        public virtual void init(EntityData d, World w) {
            _data = d;
            world = w;
        }

    }
}