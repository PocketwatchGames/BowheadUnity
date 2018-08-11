// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Server.Actors;
using Bowhead.Actors;
using System;
using System.Collections.Generic;

public static partial class WorldUtils {
	#region get block

	public static bool IsBlockLoaded(this World world, Vector3 pos) {
		Voxel_t voxel;
		return world.worldStreaming.GetVoxelAt(World.Vec3ToWorld(pos), out voxel);
	}

	public static EVoxelBlockType GetBlock(this World world, Vector3 pos) {

		Voxel_t voxel;
		if (world.worldStreaming.GetVoxelAt(World.Vec3ToWorld(pos), out voxel)) {
			return voxel.type;
		}

		return EVoxelBlockType.Air;
	}


	public static EVoxelBlockType GetBlock(this World world, float x, float y, float z) {
		return world.GetBlock(new Vector3(x, y, z));
	}

	public static float GetFirstOpenBlockUp(int checkDist, Vector3 from) {
		RaycastHit hit;
		if (Physics.Raycast(new Vector3(from.x, 500, from.z), Vector3.down, out hit, checkDist, Layers.PawnCollisionMask)) {
			return hit.point.y;
		}
		return from.y;
	}

	public static bool GetFirstSolidBlockDown(int checkDist, ref Vector3 from) {
		RaycastHit hit;
		if (Physics.Raycast(from, Vector3.down, out hit, checkDist, Layers.PawnCollisionMask)) {
			from.y = hit.point.y + 1;
			return true;
		}
		return false;
	}
	#endregion

	#region static block properties

	public static bool IsCapBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.Snow) return true;
		return false;
	}


	public static bool IsSolidBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.Water
			|| type == EVoxelBlockType.Air
			|| type == EVoxelBlockType.Snow
			|| type == EVoxelBlockType.Flowers1
			|| type == EVoxelBlockType.Flowers2
			|| type == EVoxelBlockType.Flowers3
			|| type == EVoxelBlockType.Flowers4) {
			return false;
		}
		return true;
	}


	public static bool IsTransparentBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.Air
			|| type == EVoxelBlockType.Water
			|| type == EVoxelBlockType.Needles
			|| type == EVoxelBlockType.Flowers1
			|| type == EVoxelBlockType.Flowers2
			|| type == EVoxelBlockType.Flowers3
			|| type == EVoxelBlockType.Flowers4
			|| type == EVoxelBlockType.Leaves
			|| type == EVoxelBlockType.Snow) {
			return true;
		}
		return false;
	}

	public static bool IsDiggable(EVoxelBlockType type) {
		if (type == EVoxelBlockType.Water) return false;
		return true;
	}


	#endregion
}

namespace Bowhead.Server {

	public abstract class BowheadGame<T> : GameMode<T> where T : GameState<T> {
		public const WorldStreaming.EGenerator WORLD_GENERATOR_TYPE = WorldStreaming.EGenerator.PROC_V2;

		static SpawnPointData[] _monsterSpawns = SpawnPointData.GetAllSpawnTypes(ESpawnPointType.Monster);
		static SpawnPointData[] _merchantSpawns = SpawnPointData.GetAllSpawnTypes(ESpawnPointType.Merchant);
		static SpawnPointData[] _horseSpawns = SpawnPointData.GetAllSpawnTypes(ESpawnPointType.Horse);
		static SpawnPointData[] _chestSpawns = SpawnPointData.GetAllSpawnTypes(ESpawnPointType.Chest);
		static SpawnPointData[] _mapRevealSpawns = SpawnPointData.GetAllSpawnTypes(ESpawnPointType.MapReveal);

		public BowheadGame(ServerWorld world) : base(world) {
			data = Resources.Load<WorldData>("DefaultWorld");

			// mixed mode server owns the world instance
			if (!GameManager.instance.dedicatedServer) {
				world.worldStreaming.SetWorldAtlasClientData(data.atlasData.atlasClientData.Load());
			}
		}

		public WorldData data;

		int numCritters;
		FastNoise_t noise = FastNoise_t.New();

        public event Action<Pawn, float> onAudioEvent;

        protected override void PrepareForMatchInProgress() {
			base.PrepareForMatchInProgress();

			//for (int i = 0; i < 100; i++) {
			//	WorldItem worldItem = null;
			//	var pos = new Vector3(UnityEngine.Random.Range(-500f, 500f) + 0.5f, 500f, UnityEngine.Random.Range(-500f, 500f) + 0.5f);
			//	int itemType = UnityEngine.Random.Range(0, 4);
			//	switch (itemType) {
			//		case 0: {
			//			worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, pos, null, null, null);
			//			var money = MoneyData.Get("Money").CreateItem();
			//			worldItem.item = money;
			//			money.count = 100;
			//			break;
			//		} case 1: {
			//			worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, pos, null, null, null);
			//			var item = ClothingData.Get("Chainmail").CreateItem();
			//			worldItem.item = item;
			//			break;
			//		} case 2: {
			//			worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, pos, null, null, null);
			//			var item = WeaponData.Get("2HSword").CreateItem();
			//			worldItem.item = item;
			//			break;
			//		} case 3: {
			//			worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, pos, null, null, null);
			//			var item = WeaponData.Get("SpellHeal").CreateItem();
			//			worldItem.item = item;
			//			break;
			//		}
			//	}
			//}

		}

		protected override WorldStreaming.IWorldStreaming CreateWorldStreaming() {
			return WorldStreaming.NewProceduralWorldStreaming(0, WORLD_GENERATOR_TYPE);
		}

		protected override void OnChunkLoaded(World.Streaming.IChunk chunk) {
			SpawnDecorations(chunk);
		}

		protected override void OnChunkUnloaded(World.Streaming.IChunk chunk) { }

		void SpawnDecorations(World.Streaming.IChunk chunk) {
			var decorationCount = chunk.decorationCount;
			if (decorationCount > 0) {
				var decorations = chunk.decorations;
				var wpos = World.WorldToVec3(World.ChunkToWorld(chunk.chunkPos));

				for (int i = 0; i < decorationCount; ++i) {
					SpawnDecoration(chunk, wpos, decorations[i]);
				}
			}
		}

		float GetWhiteNoise(ref FastNoise_t noise, float x, float y, float z) {
			var v = noise.GetWhiteNoise(x, y, z);
			return (v + 1) / 2;
		}
		void SpawnDecoration(World.Streaming.IChunk chunk, Vector3 chunkWorldPos, World.Decoration_t decoration) {
			SpawnPointData spawnPoint;

			switch (decoration.type) {
				default:
					return;
				case EDecorationType.MonsterSpawn:
					if (_monsterSpawns.Length < 1) {
						return;
					}
					spawnPoint = _monsterSpawns.GetAtIndexZeroToOne(GetWhiteNoise(ref noise, chunkWorldPos.x,chunkWorldPos.y,chunkWorldPos.z));
				break;
				case EDecorationType.Merchant:
					if (_merchantSpawns.Length < 1) {
						return;
					}
					spawnPoint = _merchantSpawns.GetAtIndexZeroToOne(GetWhiteNoise(ref noise, chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z));
					break;
				case EDecorationType.Horse:
					if (_horseSpawns.Length < 1) {
						return;
					}
					spawnPoint = _horseSpawns.GetAtIndexZeroToOne(GetWhiteNoise(ref noise, chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z));
					break;
				case EDecorationType.Chest:
					if (_chestSpawns.Length < 1) {
						return;
					}
					spawnPoint = _chestSpawns.GetAtIndexZeroToOne(GetWhiteNoise(ref noise, chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z));
					break;
				case EDecorationType.MapReveal:
					if (_mapRevealSpawns.Length < 1) {
						return;
					}
					spawnPoint = _mapRevealSpawns.GetAtIndexZeroToOne(GetWhiteNoise(ref noise, chunkWorldPos.x, chunkWorldPos.y, chunkWorldPos.z));
				break;
			}

			spawnPoint.Spawn<Actor>(this, decoration.pos, 0);

		}

		#region perlin utils
		public float GetPerlinNormal(int x, int y, int z, float scale) {
			noise.SetFrequency(scale);
			var v = noise.GetPerlin(x, y, z);
			return (v + 1) / 2;
		}

		public float GetPerlinValue(int x, int y, int z, float scale) {
			noise.SetFrequency(scale);
			return noise.GetPerlin(x, y, z);
		}

		#endregion

		#region entity creation

		protected override ServerTeam GetTeamForSpawningPlayer(ServerPlayerController playerController) {
			// TODO: base assigns every player to a new team
			return base.GetTeamForSpawningPlayer(playerController);
		}

		protected override void InitPlayerSpawn(ServerPlayerController playerController) {
			base.InitPlayerSpawn(playerController);
			var player = SpawnPlayer(0);
			playerController.PossessPlayerPawn(player);

			if (data.player2) {
				var player2 = SpawnPlayer(1);
				player2.team = player.team;
			}
		}

		Player SpawnPlayer(int index) {
			return PlayerData.Get("player").Spawn<Player>(index, world, new Vector3(16, 100, 20), 0, null, null, null);
		}

		public Critter SpawnCritter(CritterData data, Vector3 pos, float yaw, Team team) {
			var critter = data.Spawn<Critter>(world, pos, yaw, null, null, team);
			
			return critter;
		}

		public void CritterSpawned() {
			++numCritters;
		}

		public void CritterKilled() {
			--numCritters;
		}


		public void SpawnRandomCritter() {

			//if (numCritters < 100) {
			//	var critterTypes = new List<CritterData> {
			//		CritterData.Get("bunny"),
			//		CritterData.Get("wolf"),
			//		CritterData.Get("cobra"),
			//	};
			//	foreach (var player in world.GetActorIterator<Player>()) {
			//		Vector3 pos = player.position;
			//		pos.y = 1000;
			//		pos.x += UnityEngine.Random.Range(-200f, 200f) + 0.5f;
			//		pos.z += UnityEngine.Random.Range(-200f, 200f) + 0.5f;

			//		SpawnCritter(critterTypes[UnityEngine.Random.Range(0,critterTypes.Count)], pos, UnityEngine.Random.Range(0,360)*Mathf.Deg2Rad, monsterTeam);
			//		break;
			//	}
			//}
		}
		#endregion

		#region time, weather and water currents

		float GetTimeOfDay() {
			// world.time is a double so can't use Mathf.Repeat()
			// see: https://randomascii.wordpress.com/2012/02/13/dont-store-that-in-a-float/ for explanation.
			var rep = world.time - (Math.Floor(world.time / data.SecondsPerDay) * data.SecondsPerDay);
			return (float)(rep / data.SecondsPerDay * 24);
		}

		public float GetRiver(int blockX, int blockY) {
			int offsetX = 0;
			int offsetY = 0;
			float powerScaleInverse = 0.001f;
			float power =
				0.4f * GetPerlinNormal((blockX + offsetX), (blockY + offsetY), 0, powerScaleInverse) +
				0.3f * GetPerlinNormal((blockX + offsetX + 25254), (blockY + offsetY + 65363), 0, powerScaleInverse * 0.5f) +
				0.3f * GetPerlinNormal((blockX + offsetX + 2254), (blockY + offsetY + 6563), 0, powerScaleInverse * 0.1f);
			return power;
		}

		public Vector3 GetCurrent(int x, int y, int z) {
			float inverseRegionSize = 0.01f;
			var center = GetRiver(x, z);
			Vector3 diff = Vector3.zero;
			diff += new Vector3(1, 0, 0) * Math.Abs(GetRiver(x - 1, z) - center);
			diff += new Vector3(1, 0, 0) * Math.Abs(GetRiver(x + 1, z) - center);
			diff += new Vector3(0, 0, 1) * Math.Abs(GetRiver(x, z - 1) - center);
			diff += new Vector3(0, 0, 1) * Math.Abs(GetRiver(x, z + 1) - center);
			float currentSpeed = diff.magnitude;
			if (currentSpeed != 0) {
				diff /= currentSpeed;
				currentSpeed *= 1000 * GetPerlinValue(x, y, z, inverseRegionSize);
			}
			return diff * Mathf.Clamp(currentSpeed, -8f, 8f);
		}

		public Vector3 GetWind(Vector3 p) {
			return GetWind((int)p.x, (int)p.y, (int)p.z);
		}
		public Vector3 GetWind(int x, int y, int z) {
			float inverseRegionSize = 0.001f;
			float windAngle = GetPerlinNormal(x + 6543, z + 6543, 0, inverseRegionSize) * Mathf.PI * 2;
			var wind = new Vector3(Mathf.Cos(windAngle), 0, Mathf.Sin(windAngle));
			float currentSpeed = data.minWindSpeedVariance + (data.maxWindSpeedVariance - data.minWindSpeedVariance) * Mathf.Pow(0.5f + 0.5f * GetPerlinValue((x + 88943), (y + 653), z, inverseRegionSize), 2f);

			float timeScale = 0.002f;
			float weatherSpeed = (GetPerlinNormal((int)((world.time + 46332) * timeScale), 876, 0, 1f) +
				GetPerlinNormal(18740, (int)((world.time + 7476) * timeScale), 0, 1f) +
				GetPerlinNormal((int)((world.time + 1454) * timeScale), (int)((world.time + 76746) * timeScale), 0, 1f)) / 3;
			weatherSpeed = Mathf.Pow(weatherSpeed, 2f);
			currentSpeed *= weatherSpeed;

			if (currentSpeed >= data.windSpeedStormy) {
				currentSpeed = data.windSpeedStormy;
			}
			else if (currentSpeed >= data.windSpeedWindy) {
				currentSpeed = data.windSpeedWindy;
			}
			else if (currentSpeed >= data.windSpeedBreezy) {
				currentSpeed = data.windSpeedBreezy;
			}
			else {
				currentSpeed = 0;
			}

			return wind * currentSpeed;
		}

		Dictionary<EVoxelBlockType, int> blockMapping = new Dictionary<EVoxelBlockType, int>() {
				{ EVoxelBlockType.Air, 5 },
				{ EVoxelBlockType.Dirt, 0},
				{ EVoxelBlockType.Flowers1, 1},
				{ EVoxelBlockType.Flowers2, 1},
				{ EVoxelBlockType.Flowers3, 1},
				{ EVoxelBlockType.Flowers4, 1},
				{ EVoxelBlockType.Grass, 0},
				{ EVoxelBlockType.Ice, 3},
				{ EVoxelBlockType.Leaves, 2},
				{ EVoxelBlockType.Needles, 2},
				{ EVoxelBlockType.Rock, 0},
				{ EVoxelBlockType.Sand, 1},
				{ EVoxelBlockType.Snow, 1},
				{ EVoxelBlockType.Water, 6},
				{ EVoxelBlockType.Wood, 2},
			};

		public WorldData.TerrainType GetTerrainData(Vector3 pos) {
			var block = world.GetBlock(pos);
			return data.terrainTypes[blockMapping[block]];
		}

		public WorldData.TerrainType GetTerrainData(EVoxelBlockType block) {
			return data.terrainTypes[blockMapping[block]];
		}


		#endregion

		#region Tick
		public override void Tick(float dt) {
			base.Tick(dt);

			SpawnRandomCritter();

			foreach (var c1 in world.GetActorIterator<Pawn>()) {
				if (c1.active) {
					float c1Radius = 0.5f;
					foreach (var c2 in world.GetActorIterator<Pawn>()) {
						if (c2.active && c2 != c1) {
							float c2Radius = 0.5f;
							var diff = c1.rigidBody.transform.position - c2.rigidBody.transform.position;
							float dist = diff.magnitude;
							float okRange = c1Radius + c2Radius;
							if (dist < okRange) {
								var moveDir = diff / dist;
								c1.Move(moveDir * (okRange - dist) / 2);
								c2.Move(-moveDir * (okRange - dist) / 2);
							}
						}
					}
				}
			}
		}
        #endregion


        #region World Events

        public void CreateAudioEvent(Pawn origin, float loudness)
        {
            onAudioEvent?.Invoke(origin, loudness);
        }

        #endregion
    }

    public class BowheadGame : BowheadGame<GSBowheadGame> {
		public BowheadGame(ServerWorld world) : base(world) { }
	}

	public sealed class GSBowheadGame : GameState<GSBowheadGame> {
		[Replicated(Condition = EReplicateCondition.InitialOnly)]
		StaticAssetRef<WorldData> _data;

		public override void ServerSetGameMode(GameMode gameMode) {
			base.ServerSetGameMode(gameMode);
			_data = ((BowheadGame)gameMode).data;
		}

		public override Type hudType => typeof(Client.UI.BowheadHUD);

		protected override WorldStreaming.IWorldStreaming CreateWorldStreaming() {
			return WorldStreaming.NewProceduralWorldStreaming(0, BowheadGame.WORLD_GENERATOR_TYPE);
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
			// mixed mode server owns the world instance
			var atlasClientData = data.atlasData.atlasClientData.Load();

			if (!GameManager.instance.isServer) {
				world.worldStreaming.SetWorldAtlasClientData(atlasClientData);
			}
		}

		public WorldData data {
			get {
				return _data;
			}
		}
	}
}