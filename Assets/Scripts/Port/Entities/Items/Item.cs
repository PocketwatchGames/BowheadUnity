using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {

    public class Item : ScriptableObject {


        [HideInInspector]
        public GameWorld world;
        [SerializeField]
        private ItemData _data;

        public ItemData Data { get { return _data; } }

        virtual public D GetData<D>() where D : ItemData {
            return _data as D;
        }

        public virtual void Init(ItemData d, GameWorld w) {
            _data = d;
            world = w;
        }

        public static T Create<T>(ItemData d, GameWorld w) where T : Item, new() {
            T i = new T();
            i.Init(d, w);
            return i;
        }
        public static Item Create(ItemData itemData, GameWorld w) {
            Item i = null;
            if (itemData is MoneyData) {
                i = new Money();
            }
            else if (itemData is WeaponData) {
                i = new Weapon();
            }
            else if (itemData is ClothingData) {
                i = new Clothing();
            }
            else if (itemData is LootData) {
                i = new Loot();
            }
            else if (itemData is PackData) {
                i = new Pack();
            }

            if (i == null) {
                return null;
            }

            i.Init(itemData, w);
            return i;
        }

        // Use this for initialization
        public static ItemData GetData(string dataName) { return DataManager.GetData<ItemData>(dataName); }


        virtual public void UpdateCast(float dt, Actor actor) {
        }

        public virtual void OnSlotChange(int newSlot, Actor owner) {

        }



    }
}