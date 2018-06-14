using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {


    abstract public class Item {


        [HideInInspector]
        public World world;
        [SerializeField]
        private ItemData _data;

        public ItemData Data { get { return _data; } }

        virtual public D GetData<D>() where D : ItemData {
            return _data as D;
        }

        public virtual void init(ItemData d, World w) {
            _data = d;
            world = w;
        }

        public static Item Create(ItemData itemData, World w) {
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

            i.init(itemData, w);
            return i;
        }

        // Use this for initialization
        public static ItemData GetData(string dataName) { return DataManager.GetItemData<ItemData>(dataName); }


        virtual public void updateCast(float dt, Actor actor) {
        }

        public virtual void onSlotChange() {

        }



    }
}