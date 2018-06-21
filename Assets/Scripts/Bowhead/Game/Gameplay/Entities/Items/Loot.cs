using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
	using Player = Actors.Player;
	using Pawn = Actors.Pawn;
    public class Loot : Item<Loot, LootData> {

        #region State

        public int count;

        #endregion

        public bool use(Actor actor) {
            if (data.use == null) {
                return true;
            }
            return data.use(this, actor);
        }

        static bool UseWater(Loot item, Actor actor) {
            // TODO: This static cast is not good
            Player player = actor as Player;
            if (player == null)
                return false;
            if (player.thirst >= player.maxThirst) {
                return false;
            }
            player.thirst = Mathf.Min(player.thirst + item.data.power, player.maxThirst);
            return true;
        }

        static bool UseFood(Loot item, Pawn actor) {
            if (actor.health >= actor.maxHealth) {
                return false;
            }
            actor.health = Mathf.Min(actor.health + item.data.power, actor.maxHealth);
            return true;
        }

    }
}
