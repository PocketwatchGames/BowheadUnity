// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEditor;
using Bowhead;
using Bowhead.Actors;
using Bowhead.Actors.Spells;
using Bowhead.Client;

namespace Bowhead.Editor {
	public static class MenuItems {
		[MenuItem("Assets/Create/Bowhead/Client Data")]
		static void CreateClientData() {
			Utils.CreateAsset<ClientData>();
		}

		[MenuItem("Assets/Create/Bowhead/HUD Description")]
		static void CreateHUDDescription() {
			Utils.CreateAsset<Client.UI.HUDDescription>();
		}

		//[MenuItem("Assets/Create/Bowhead/Map Description")]
		//static void CreateMapDescription() {
		//	Utils.CreateAsset<MapDescription>();
		//}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Actor MetaClass", priority = 0)]
		static void CreateActorMetaClass() {
			Utils.CreateAsset<ActorMetaClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Actor Property", priority = 1)]
		static void CreateActorProperty() {
			Utils.CreateAsset<ActorProperty>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Actor Property Class", priority = 2)]
		static void CreateActorPropertClass() {
			Utils.CreateAsset<ActorPropertyClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Actor Property MetaClass", priority = 3)]
		static void CreateActorPropertyMetaClass() {
			Utils.CreateAsset<ActorPropertyMetaClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Area Of Effect Class", priority = 50)]
		static void CreateAreaOfEffectClass() {
			Utils.CreateAsset<AreaOfEffectClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Area Of Effect Sounds", priority = 51)]
		static void CreateAreaOfEffectSounds() {
			Utils.CreateAsset<AreaOfEffectSounds>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Blood Sprays", priority = 100)]
		static void CreateBloodSprays() {
			Utils.CreateAsset<BloodSprays>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Damage MetaClass", priority = 150)]
		static void CreateDamageMetaClass() {
			Utils.CreateAsset<DamageMetaClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Direct Damage Class", priority = 151)]
		static void CreateDirectDamageClass() {
			Utils.CreateAsset<DirectDamageClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Explosion Damage Class", priority = 152)]
		static void CreateExplosionDamageClass() {
			Utils.CreateAsset<ExplosionDamageClass>();
		}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Destroyable Goal Actor Class", priority = 200)]
		//static void CreateDestroyableGoalActorClass() {
		//	Utils.CreateAsset<DestroyableGoalActorClass>();
		//}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Touch Goal Actor Class", priority = 201)]
		//static void CreateTouchGoalActorClass() {
		//	Utils.CreateAsset<TouchGoalActorClass>();
		//}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Physical Contact Matrix", priority = 250)]
		static void CreatePhysicalContactMatrix() {
			Utils.CreateAsset<PhysicalContactMatrix>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Physical Material Class", priority = 251)]
		static void CreatePhysicalMaterialClass() {
			Utils.CreateAsset<PhysicalMaterialClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Direct Projectile Class", priority = 300)]
		static void CreateDirectProjectileClass() {
			Utils.CreateAsset<DirectProjectileClass>();
		}
		
		[MenuItem("Assets/Create/Bowhead/Gameplay/Explosion Projectile Class", priority = 301)]
		static void CreateExplosionProjectileClass() {
			Utils.CreateAsset<ExplosionProjectileClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Ability Class", priority = 350)]
		static void CreateAbilityClass() {
			Utils.CreateAsset<AbilityClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Spell MetaClass", priority = 353)]
		static void CreateSpellMetaClass() {
			Utils.CreateAsset<SpellMetaClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Standard Spell", priority = 354)]
		static void CreateStandardSpellClass() {
			Utils.CreateAsset<StandardSpellClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Tag Attached Spell Effect Class", priority = 355)]
		static void CreateTagAttachedSpellEffectClass() {
			Utils.CreateAsset<TagAttachedSpellEffectClass>();
		}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Bleed Spell Effect Class", priority = 356)]
		static void CreateBleedSpellEffectClass() {
			Utils.CreateAsset<BleedSpellEffectClass>();
		}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Unit ActionCue", priority = 400)]
		//static void CreateUnitActionCue() {
		//	Utils.CreateAsset<UnitActionCue>();
		//}

		[MenuItem("Assets/Create/Bowhead/Gameplay/Unit Action MetaClass", priority = 401)]
		static void CreateUnitActionMetaClass() {
			Utils.CreateAsset<UnitActionMetaClass>();
		}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Unit Class", priority = 402)]
		//static void CreateUnitClass() {
		//	Utils.CreateAsset<UnitClass>();
		//}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Unit Counter Action", priority = 403)]
		//static void CreateUnitCounterActionClass() {
		//	Utils.CreateAsset<UnitCounterActionClass>();
		//}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Unit Melee Action", priority = 404)]
		//static void CreateUnitMeleeActionClass() {
		//	Utils.CreateAsset<UnitMeleeActionClass>();
		//}

		//[MenuItem("Assets/Create/Bowhead/Gameplay/Unit Ranged Action", priority = 405)]
		//static void CreateUnitRangedActionClass() {
		//	Utils.CreateAsset<UnitRangedActionClass>();
		//}

		[MenuItem("Assets/Create/Bowhead/Gameplay/XP Curve", priority = 500)]
		static void CreateXPCurve() {
			Utils.CreateAsset<XPCurve>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/Drop Item Table", priority = 0)]
		static void CreateItemDropTable() {
			Utils.CreateAsset<MetaGame.ItemDropTable>();
		}

		//[MenuItem("Assets/Create/Bowhead/MetaGame/Unit Drop Table", priority = 1)]
		//static void CreateUnitDropTable() {
		//	Utils.CreateAsset<MetaGame.UnitDropTable>();
		//}

		[MenuItem("Assets/Create/Bowhead/MetaGame/Item MetaClass", priority = 100)]
		static void CreateInventoryItemMetaClass() {
			Utils.CreateAsset<MetaGame.ItemMetaClass>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/Inventory Socket Item Class", priority = 200)]
		static void CreateInventorySocketItemClass() {
			Utils.CreateAsset<MetaGame.InventorySocketItemClass>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/Inventory Grant Ability Item Class", priority = 201)]
		static void CreateInventoryGrantAbilityItemClass() {
			Utils.CreateAsset<MetaGame.InventoryGrantAbilityItemClass>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/Soul Fragment Item Class", priority = 202)]
		static void CreateSoulFragmentItemClass() {
			Utils.CreateAsset<MetaGame.SoulFragmentItemClass>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/ItemStat", priority = 300)]
		static void CreateInventoryItemStat() {
			Utils.CreateAsset<MetaGame.ItemStat>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/ItemStat Class", priority = 301)]
		static void CreateInventoryItemStatClass() {
			Utils.CreateAsset<MetaGame.ItemStatClass>();
		}

		[MenuItem("Assets/Create/Bowhead/MetaGame/ItemStat MetaClass", priority = 302)]
		static void CreateInventoryItemStatMetaClass() {
			Utils.CreateAsset<MetaGame.ItemStatMetaClass>();
		}

		//[MenuItem("Assets/Create/Bowhead/MetaGame/Mission", priority = 400)]
		//static void CreateMission() {
		//	Utils.CreateAsset<MetaGame.Mission>();
		//}

		//[MenuItem("Assets/Create/Bowhead/MetaGame/Mission Objective", priority = 401)]
		//static void CreateMissionObjective() {
		//	Utils.CreateAsset<MetaGame.MissionObjective>();
		//}

		//[MenuItem("Assets/Create/Bowhead/MetaGame/Mission Config", priority = 402)]
		//static void CreateMissionConfig() {
		//	Utils.CreateAsset<MetaGame.MissionConfig>();
		//}
	}
}