using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {



    abstract public class Entity : MonoBehaviour {

        [HideInInspector]
        public GameWorld world;
        [SerializeField]
        private EntityData _data;

        public EntityData Data { get { return _data; } }

        virtual public D GetData<D>() where D : EntityData {
            return _data as D;
        }

        public virtual void Create(EntityData d, GameWorld w) {
            _data = d;
            world = w;
        }

    }



}