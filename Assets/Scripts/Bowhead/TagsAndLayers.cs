// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

namespace Bowhead {
	public static class Layers {
		public const int Default = 0;
		public const int DefaultMask = 1 << Default;

		public const int TransparentFX = 1;
		public const int TransparentFXMask = 1 << TransparentFX;

		public const int IgnoreRaycast = 2;
		public const int IgnoreRaycastMask = 1 << IgnoreRaycast;

		public const int Water = 4;
		public const int WaterMask = 1 << Water;

		public const int UI = 5;
		public const int UIMask = 1 << UI;

		public const int Block = 8;
		public const int BlockMask = 1 << Block;

		public const int Terrain = 9;
		public const int TerrainMask = 1 << Terrain;

		public const int NoSelfContactProjectiles = 10;
		public const int NoSelfContactProjectilesMask = 1 << NoSelfContactProjectiles;

		public const int Selection = 11;
		public const int SelectionMask = 1 << Selection;

		public const int Ragdoll = 12;
		public const int RagdollMask = 1 << Ragdoll;

		public const int Passability = 13;
		public const int PassabilityMask = 1 << Passability;

		public const int HitTest = 14;
		public const int HitTestMask = 1 << HitTest;

		public const int Team1 = 15;
		public const int Team1Mask = 1 << Team1;

		public const int Team2 = 16;
		public const int Team2Mask = 1 << Team2;

		public const int Team3 = 17;
		public const int Team3Mask = 1 << Team3;

		public const int Team4 = 18;
		public const int Team4Mask = 1 << Team4;

		public const int Team1Projectiles = 19;
		public const int Team1ProjectilesMask = 1 << Team1Projectiles;

		public const int Team2Projectiles = 20;
		public const int Team2ProjectilesMask = 1 << Team2Projectiles;

		public const int Team3Projectiles = 21;
		public const int Team3ProjectilesMask = 1 << Team3Projectiles;

		public const int Team4Projectiles = 22;
		public const int Team4ProjectilesMask = 1 << Team4Projectiles;

		public const int Trigger = 23;
		public const int TriggerMask = 1 << Trigger;

		public const int Gibs = 24;
		public const int GibsMask = 1 << Gibs;
				
		public const int TeamCollision = 25;
		public const int TeamCollisionMask = 1 << TeamCollision;

		public const int MonsterTeam = 26;
		public const int MonsterTeamMask = 1 << MonsterTeam;

		public const int MonsterTeamProjectiles = 27;
		public const int MonsterTeamProjectilesMask = 1 << MonsterTeamProjectiles;

		public const int WaterInteraction = 28;
		public const int WaterInteractionMask = 1 << WaterInteraction;

		public const int Gravestones = 29;
		public const int GravestonesMask = 1 << Gravestones;

		public const int Pickups = 30;
		public const int PickupsMask = 1 << Pickups;

		public const int AllTeamsMask = Team1Mask | Team2Mask | Team3Mask | Team4Mask | MonsterTeamMask;
		public const int AllProjectilesMask = NoSelfContactProjectilesMask | Team1ProjectilesMask | Team2ProjectilesMask | Team3ProjectilesMask | Team4ProjectilesMask | MonsterTeamProjectilesMask;

		public const int CameraTraceMask = TerrainMask | BlockMask;

		public static int ToLayerMask(this ELayers layers) {
			int mask = 0;

			if ((layers & ELayers.Default) != 0) {
				mask |= Layers.DefaultMask;
			}
			if ((layers & ELayers.TransparentFX) != 0) {
				mask |= Layers.TransparentFXMask;
			}
			if ((layers & ELayers.Water) != 0) {
				mask |= Layers.WaterMask;
			}
			if ((layers & ELayers.UI) != 0) {
				mask |= Layers.UIMask;
			}
			if ((layers & ELayers.Block) != 0) {
				mask |= Layers.BlockMask;
			}
			if ((layers & ELayers.Terrain) != 0) {
				mask |= Layers.TerrainMask;
			}
			if ((layers & ELayers.Units) != 0) {
				mask |= Layers.AllTeamsMask;
			}
			if ((layers & ELayers.Selection) != 0) {
				mask |= Layers.SelectionMask;
			}
			if ((layers & ELayers.Ragdoll) != 0) {
				mask |= Layers.RagdollMask;
			}
			if ((layers & ELayers.Passability) != 0) {
				mask |= Layers.PassabilityMask;
			}
			if ((layers & ELayers.HitTest) != 0) {
				mask |= Layers.HitTestMask;
			}
			if ((layers & ELayers.Projectiles) != 0) {
				mask |= Layers.AllProjectilesMask;
			}
			if ((layers & ELayers.Triggers) != 0) {
				mask |= Layers.TriggerMask;
			}
			if ((layers & ELayers.Gibs) != 0) {
				mask |= Layers.GibsMask;
			}

			return mask;
		}

		public static int GetTeamLayer(int teamNumber) {
			if (teamNumber == Actors.Team.MONSTER_TEAM_NUMBER) {
				return MonsterTeam;
			}
			if (teamNumber == Actors.Team.NPC_TEAM_NUMBER) {
				return Team1;
			}
			return Team1 + teamNumber;
		}

		public static int GetTeamLayerMask(int teamNumber) {
			return 1 << GetTeamLayer(teamNumber);
		}

		public static int GetTeamProjectilesLayer(int teamNumber) {
			if (teamNumber == Actors.Team.MONSTER_TEAM_NUMBER) {
				return MonsterTeamProjectiles;
			}
			return Team1Projectiles + teamNumber;
		}
	}

	[Flags]
	public enum ELayers {
		Default = 0x1,
		TransparentFX = 0x2,
		Water = 0x4,
		UI = 0x8,
		Block = 0x10,
		Terrain = 0x20,
		Units = 0x40,
		Selection = 0x80,
		Ragdoll = 0x100,
		Passability = 0x200,
		HitTest = 0x400,
		Projectiles = 0x800,
		Triggers = 0x1000,
		Gibs = 0x2000
	}

	public static class Tags {
		public const string Untagged = "Untagged";
		public const string Respawn = "Respawn";
		public const string Finish = "Finish";
		public const string EditorOnly = "EditorOnly";
		public const string MainCamera = "MainCamera";
		public const string Player = "Player";
		public const string GameController = "GameController";
		public const string UICamera = "UICamera";
		public const string ProjectileBounce = "ProjectileBounce";
		public const string ApexIgnore = "ApexIgnore";
		public const string Minimap = "Minimap";
	}
}
