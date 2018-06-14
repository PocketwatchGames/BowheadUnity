using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Loot : Item {

        #region State

        public int count;

        #endregion


        new public LootData Data { get { return GetData<LootData>(); } }
        public static LootData GetData(string dataName) { return DataManager.GetItemData<LootData>(dataName); }


        public bool use(Actor actor) {
            if (Data.use == null) {
                return true;
            }
            return Data.use(this, actor);
        }

        static bool UseWater(Loot item, Actor actor) {
            // TODO: This static cast is not good
            Player player = actor as Player;
            if (player == null)
                return false;
            if (player.thirst >= player.maxThirst) {
                return false;
            }
            player.thirst = Mathf.Min(player.thirst + item.Data.power, player.maxThirst);
            return true;
        }

        static bool UseFood(Loot item, Actor actor) {
            if (actor.health >= actor.maxHealth) {
                return false;
            }
            actor.health = Mathf.Min(actor.health + item.Data.power, actor.maxHealth);
            return true;
        }

    }
}
