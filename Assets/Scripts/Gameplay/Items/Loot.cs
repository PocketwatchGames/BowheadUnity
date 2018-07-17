using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bowhead.Actors;

namespace Bowhead {
    public class Loot : Item<Loot, LootData> {

        #region State

        public int count = 1;

        #endregion

        public bool use(Pawn owner) {
            bool success = false;
			if (data.useType == LootData.UseType.Food) {
				success = UseFood(this, owner);
			}
			else if (data.useType == LootData.UseType.Water) {
                success = UseWater(this, owner);
			}
            else
            {
                success = true;
            }
            if (success && data.statusEffect != null)
            {
                owner.AddStatusEffect(data.statusEffect, data.statusEffectTime);
            }
			return success;
        }

        static bool UseWater(Loot item, Pawn owner) {
            // TODO: This static cast is not good
            Player player = owner as Player;
            if (player == null)
                return false;
            if (player.water >= player.maxWater) {
                return false;
            }
            player.water = Mathf.Min(player.water + item.data.power, player.maxWater);
            return true;
        }

        static bool UseFood(Loot item, Pawn owner) {
            if (owner.health >= owner.maxHealth) {
                return false;
            }
            owner.health = Mathf.Min(owner.health + item.data.power, owner.maxHealth);
            return true;
        }

    }
}
