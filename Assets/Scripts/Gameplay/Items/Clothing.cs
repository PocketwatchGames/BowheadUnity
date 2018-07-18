using System.Collections;
using System.Collections.Generic;
using Bowhead.Actors;
using UnityEngine;

namespace Bowhead {
    public class Clothing : Item<Clothing, ClothingData> {
		#region State

		#endregion

		public override void OnSlotChange(int newSlot, int oldSlot, Pawn owner) {

			base.OnSlotChange(newSlot, oldSlot, owner);

			if (oldSlot == (int)Player.InventorySlot.CLOTHING) {
				if (data.maxStaminaBonus > 0) {
					owner.maxStamina -= data.maxStaminaBonus;
					owner.stamina = Mathf.Min(owner.stamina, owner.maxStamina);
				}
				if (data.maxHealthBonus > 0) {
					owner.maxHealth -= data.maxHealthBonus;
					owner.health = Mathf.Min(owner.health, owner.maxHealth);
				}
			}
			else if (newSlot == (int)Player.InventorySlot.CLOTHING) {
				if (data.maxStaminaBonus > 0) {
					owner.maxStamina += data.maxStaminaBonus;
				}
				if (data.maxHealthBonus > 0) {
					owner.maxHealth += data.maxHealthBonus;
				}
			}
		}
	}
}
