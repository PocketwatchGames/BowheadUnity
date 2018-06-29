// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Server.Actors;
using Bowhead.Actors;
using System;

public static partial class WorldUtils {
	#region get block

	public static EVoxelBlockType GetBlock(this World world, Vector3 pos) {
		
		EVoxelBlockType blockType;
		if (world.worldStreaming.GetVoxelAt(World.Vec3ToWorld(pos), out blockType)) {
			return blockType;
		}

		return EVoxelBlockType.AIR;
	}

	public static EVoxelBlockType GetBlock(this World world, float x, float y, float z) {
		return world.GetBlock(new Vector3(x, y, z));
	}

	public static float GetFirstOpenBlockUp(int checkDist, Vector3 from) {
		RaycastHit hit;
		if (Physics.Raycast(new Vector3(from.x, 500, from.z), Vector3.down, out hit, checkDist, Bowhead.Layers.ToLayerMask(Bowhead.ELayers.Terrain))) {
			return hit.point.y;
		}
		return from.y;
	}

	public static bool GetFirstSolidBlockDown(int checkDist, ref Vector3 from) {
		RaycastHit hit;
		if (Physics.Raycast(from, Vector3.down, out hit, checkDist, Bowhead.Layers.ToLayerMask(Bowhead.ELayers.Terrain))) {
			from.y = hit.point.y + 1;
			return true;
		}
		return false;
	}
	#endregion

	#region static block properties


	public static bool IsCapBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.SNOW) return true;
		return false;
	}


	public static bool IsSolidBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.WATER
			|| type == EVoxelBlockType.AIR
			|| type == EVoxelBlockType.SNOW
			|| type == EVoxelBlockType.FLOWERS1
			|| type == EVoxelBlockType.FLOWERS2
			|| type == EVoxelBlockType.FLOWERS3
			|| type == EVoxelBlockType.FLOWERS4) {
			return false;
		}
		return true;
	}

	public static bool IsClimbable(EVoxelBlockType type, bool skilledClimber) {
		if (type == EVoxelBlockType.LEAVES || type == EVoxelBlockType.NEEDLES || type == EVoxelBlockType.WOOD) {
			return true;
		}
		if (skilledClimber) {
			if (type == EVoxelBlockType.DIRT || type == EVoxelBlockType.ROCK || type == EVoxelBlockType.GRASS) {
				return true;
			}
		}
		return false;
	}

	public static bool IsHangable(EVoxelBlockType type, bool skilledClimber) {
		if (type == EVoxelBlockType.DIRT || type == EVoxelBlockType.ROCK || type == EVoxelBlockType.GRASS) {
			return true;
		}
		return false;
	}


	public static void GetSlideThreshold(EVoxelBlockType foot, EVoxelBlockType mid, EVoxelBlockType head, out float slideFriction, out float slideThreshold) {
		slideThreshold = 100;
		slideFriction = 0.5f;

		if (mid == EVoxelBlockType.SNOW) {
			slideThreshold = 4;
			slideFriction = 0.25f;
		} else if (foot == EVoxelBlockType.DIRT) {
			slideThreshold = 25;
		} else if (foot == EVoxelBlockType.GRASS) {
			slideThreshold = 25;
		} else if (foot == EVoxelBlockType.ROCK) {
			slideThreshold = 25;
		} else if (foot == EVoxelBlockType.SAND) {
			slideThreshold = 4;
		}
	}

	public static float GetWorkModifier(EVoxelBlockType foot, EVoxelBlockType mid, EVoxelBlockType head) {
		float workModifier = 0;

		if (mid == EVoxelBlockType.SNOW) {
			//workModifier = 1f;
		}
		//else if (mid == EVoxelBlockType.LongGrass || foot == EVoxelBlockType.LongGrass || head == EVoxelBlockType.LongGrass)
		//{
		//	workModifier = 15f;
		//}
		else if (foot == EVoxelBlockType.GRASS) {
			workModifier = 1f;
		} else if (foot == EVoxelBlockType.SAND) {
			workModifier = 1f;
		}
		return workModifier;

	}

	public static float GetFallDamage(EVoxelBlockType type) {
		if (type == EVoxelBlockType.SNOW)
			return 0.5f;
		else if (type == EVoxelBlockType.SAND)
			return 0.75f;
		else if (type == EVoxelBlockType.DIRT || type == EVoxelBlockType.GRASS)
			return 0.9f;
		return 1.0f;
	}

	public static bool IsTransparentBlock(EVoxelBlockType type) {
		if (type == EVoxelBlockType.AIR
			|| type == EVoxelBlockType.WATER
			|| type == EVoxelBlockType.NEEDLES
			|| type == EVoxelBlockType.FLOWERS1
			|| type == EVoxelBlockType.FLOWERS2
			|| type == EVoxelBlockType.FLOWERS3
			|| type == EVoxelBlockType.FLOWERS4
			|| type == EVoxelBlockType.LEAVES
			|| type == EVoxelBlockType.SNOW) {
			return true;
		}
		return false;
	}

	public static bool IsDiggable(EVoxelBlockType type) {
		if (type == EVoxelBlockType.WATER) return false;
		return true;
	}


	#endregion
}

namespace Bowhead.Server {

	public abstract class BowheadGame<T> : GameMode<T> where T: GameState<T>{
		public const WorldStreaming.EGenerator WORLD_GENERATOR_TYPE = WorldStreaming.EGenerator.PROC_V1;

		public BowheadGame(ServerWorld world) : base(world) {
			data = Resources.Load<WorldData>("DefaultWorld");
		}
		
		public WorldData data;

		int numCritters;
        FastNoise_t noise = FastNoise_t.New();

        protected override void PrepareForMatchInProgress() {
			base.PrepareForMatchInProgress();

			for (int i = 0; i < 100; i++) {
                WorldItem worldItem = null;
                var pos = new Vector3(UnityEngine.Random.Range(-500f, 500f) + 0.5f, 500f, UnityEngine.Random.Range(-500f, 500f) + 0.5f);
                int itemType = UnityEngine.Random.Range(0, 2);
                switch (itemType) {
                    case 0:
                        worldItem = SpawnWorldItem("Chest", pos);
                        var money = MoneyData.Get("Money").CreateItem();
                        worldItem.item = money;
                        money.count = 100;
                        break;
                    case 1:
                        worldItem = SpawnWorldItem("Map", pos);
                        worldItem.map = new WorldItem.MapReveal() { position = new Vector2(pos.x,pos.z), radius = 1000 };
                        break;
                }
            }

		}

		protected override WorldStreaming.IWorldStreaming CreateWorldStreaming() {
			return WorldStreaming.NewProceduralWorldStreaming(0, WORLD_GENERATOR_TYPE);
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
			var player = SpawnPlayer();
			playerController.PossessPlayerPawn(player);


        }

        Player SpawnPlayer() {
			var player = world.Spawn<Player>(null, default(SpawnParameters));
			player.ServerSpawn(new Vector3(16, 500, 16), PlayerData.Get("player"));
			return player;
		}

		public WorldItem SpawnWorldItem(string dataName, Vector3 pos) {
			var data = WorldItemData.Get(dataName);
			if (data == null) {
				return null;
			}
			var actor = world.Spawn<WorldItem>(null, default(SpawnParameters));
			actor.ServerSpawn(pos, data);
			return actor;
		}
				
		public Critter SpawnCritter(string dataName, Vector3 pos, Team team) {
			var data = CritterData.Get(dataName);
			if (data == null) {
				return null;
			}
			return SpawnCritter(data, pos, team);
		}

		public Critter SpawnCritter(CritterData data, Vector3 pos, Team team) {
			var critter = world.Spawn<Critter>(null, default(SpawnParameters));
			critter.ServerSpawn(pos, data, (ServerTeam)team);
			return critter;
		}

		public void CritterSpawned() {
			++numCritters;
		}

		public void CritterKilled() {
			--numCritters;
		}
		
		public void SpawnRandomCritter() {

            // despawn critters
            foreach (var c in world.GetActorIterator<Critter>()) {
                if (c.active) {
                    float closestDist = 1000;
                    foreach (var p in world.GetActorIterator<Player>()) {
                        if (p.active) {
                            var diff = c.position - p.position;
                            closestDist = Mathf.Min(closestDist, diff.magnitude);
                        }
                    }
                    if (closestDist > 500) {
                        c.Destroy();
                    }
                }
            }



            if (numCritters < 50) {
				foreach (var player in world.GetActorIterator<Player>()) {
					Vector3 pos = player.position;
					pos.y = 500;
					pos.x += UnityEngine.Random.Range(-200f, 200f) + 0.5f;
					pos.z += UnityEngine.Random.Range(-200f, 200f) + 0.5f;

					var bunnyData = CritterData.Get("bunny");
					var wolfData = CritterData.Get("wolf");

					var c = SpawnCritter((UnityEngine.Random.value < 0.5f) ? wolfData : bunnyData, pos, monsterTeam);
						
					if (c.data == bunnyData) {
						var item = LootData.Get("Raw Meat").CreateItem();
						item.count = 1;
						c.loot[0] = item;
					} else if (c.data == wolfData) {
						var weapon = WeaponData.Get("Teeth").CreateItem();
						c.SetInventorySlot(0, weapon);
					}
					break;
				}
			}
		}
		#endregion

		#region time, weather and water currents

		float GetTimeOfDay() {
			// world.time is a double so can't use Mathf.Repeat()
			// see: https://randomascii.wordpress.com/2012/02/13/dont-store-that-in-a-float/ for explanation.
			var rep = world.time - (Math.Floor(world.time/data.SecondsPerDay) * data.SecondsPerDay);
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
			} else if (currentSpeed >= data.windSpeedWindy) {
				currentSpeed = data.windSpeedWindy;
			} else if (currentSpeed >= data.windSpeedBreezy) {
				currentSpeed = data.windSpeedBreezy;
			} else {
				currentSpeed = 0;
			}

			return wind * currentSpeed;
		}
        #endregion

        #region Tick
        public override void Tick(float dt) {
            base.Tick(dt);

            SpawnRandomCritter();

        }

        #endregion
    }

    public class BowheadGame : BowheadGame<GSBowheadGame> {
		public BowheadGame(ServerWorld world) : base(world) { }
	}

	public class GSBowheadGame : GameState<GSBowheadGame> {

		public override Type hudType => typeof(Client.UI.BowheadHUD);

		protected override WorldStreaming.IWorldStreaming CreateWorldStreaming() {
			return WorldStreaming.NewProceduralWorldStreaming(0, BowheadGame.WORLD_GENERATOR_TYPE);
		}
	}
}