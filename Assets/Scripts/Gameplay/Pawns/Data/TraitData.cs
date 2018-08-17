using UnityEngine;

namespace Bowhead {
	[CreateAssetMenu(menuName = "Trait")]
	public class TraitData : EntityData {

		public string description;

		public float damageMultiplier;
		public float maxHealthBonus;
		public float resistPoison;
		public float resistFire;
		public float resistCold;
		public bool canClimbTrees;
		public bool canClimbRock;
		public bool canClimbIce;
		public float stealthBonusSound;
		public float stealthBonusSight;
		public float stealthBonusSmell;



		new public static ItemData Get(string name) {
			return DataManager.GetData<ItemData>(name);
		}

		public void Remove(Actors.Pawn pawn) {
			if (damageMultiplier != 0) {
				pawn.damageMultiplier -= damageMultiplier;
			}
			if (maxHealthBonus != 0) {
				pawn.maxHealth -= maxHealthBonus;
				pawn.health = Mathf.Min(pawn.health, pawn.maxHealth);
			}
			if (canClimbIce) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Ice] = false;
			}
			if (canClimbRock) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Rock] = false;
			}
			if (canClimbTrees) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Tree] = false;
			}
			if (stealthBonusSound != 0) {
				pawn.stealthBonusSound -= stealthBonusSound;
			}
			if (stealthBonusSight != 0) {
				pawn.stealthBonusSight -= stealthBonusSight;
			}
			if (stealthBonusSight != 0) {
				pawn.stealthBonusSmell -= stealthBonusSmell;
			}
		}

		public void Add(Actors.Pawn pawn) {
			if (damageMultiplier != 0) {
				pawn.damageMultiplier += damageMultiplier;
			}
			if (maxHealthBonus != 0) {
				pawn.maxHealth += maxHealthBonus;
			}
			if (canClimbIce) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Ice] = true;
			}
			if (canClimbRock) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Rock] = true;
			}
			if (canClimbTrees) {
				pawn.canClimbType[(int)WorldData.ClimbingType.Tree] = true;
			}
			if (stealthBonusSound != 0) {
				pawn.stealthBonusSound += stealthBonusSound;
			}
			if (stealthBonusSight != 0) {
				pawn.stealthBonusSight += stealthBonusSight;
			}
			if (stealthBonusSight != 0) {
				pawn.stealthBonusSmell += stealthBonusSmell;
			}
		}
	}

}
