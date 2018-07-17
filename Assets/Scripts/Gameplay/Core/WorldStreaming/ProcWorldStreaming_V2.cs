// Copyright (c) 2018 Pocketwatch Games LLC.

#define DISABLE_WORLD_STREAMING_FILE

using UnityEngine;
using Unity.Jobs;
using static World;

namespace Bowhead {
	partial class WorldStreaming {
		struct ProcWorldStreaming_V2_Job_t : IJob {
			WorldChunkPos_t cpos;
			PinnedChunkData_t chunk;
			bool checkSolid;

			public void Execute() {
				chunk = GenerateVoxels(cpos, chunk);

				if (checkSolid && IsSolidXZPlane(chunk)) {
					chunk.flags |= EChunkFlags.SOLID_XZ_PLANE;
				}

				unsafe {
					chunk.pinnedDecorationCount[0] = chunk.decorationCount;
					chunk.pinnedFlags[0] = chunk.flags;
				}
			}

			public class Streaming : IWorldStreaming {

				WorldFile _worldFile;

				public Streaming() {
#if UNITY_EDITOR
					string path = Utils.projectRootDirectory + "/WorldData";
#else
					string path = System.IO.Directory.GetCurrentDirectory() + "/WorldData";
#endif
					if (!System.IO.Directory.Exists(path)) {
						System.IO.Directory.CreateDirectory(path);
					}

					_worldFile = WorldFile.OpenOrCreate(path + "/EditorWorld");
				}

				public JobHandle ScheduleChunkGenerationJob(WorldChunkPos_t cpos, PinnedChunkData_t chunk, bool checkSolid) {
					return new ProcWorldStreaming_V2_Job_t() {
						cpos = cpos,
						chunk = chunk,
						checkSolid = checkSolid
					}.Schedule();
				}

				public World.Streaming.IMMappedChunkData MMapChunkData(World.Streaming.IChunk chunk) {
#if DISABLE_WORLD_STREAMING_FILE
					return _worldFile.MMapChunkData(chunk);
#else
					return null;
#endif
				}

				public void WriteChunkData(World.Streaming.IChunkIO chunk) {
#if DISABLE_WORLD_STREAMING_FILE
					_worldFile.WriteChunkToFile(chunk);
#else
					return null;
#endif
				}

				public void Dispose() {
					_worldFile?.Dispose();
				}
			};

			const float waterLevel = 0;
			const float minCaveElevation = 0;

			static int positive_modulo(int i, int n) {
				return (i % n + n) % n;
			}

			static class NoiseFloatScale {
				public const float _1 = 0.1f;
				public const float _01 = 0.01f;
				public const float _001 = 0.001f;
				public const float _0001 = 0.0001f;
				public const float _5 = 0.5f;
				public const float _05 = 0.01f;
				public const float _005 = 0.005f;
				public const float _0005 = 0.0005f;
			}

			static PinnedChunkData_t GenerateVoxels(WorldChunkPos_t cpos, PinnedChunkData_t chunk) {
				FastNoise_t noise = FastNoise_t.New();

				chunk.decorationCount = 0;
				chunk.flags = EChunkFlags.ALL_LAYERS_FLAGS;
				bool solid = false;
				bool air = true;

				var wpos = ChunkToWorld(cpos);
				var v3 = WorldToVec3(wpos);

				bool hasRoad = false;

				for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
					for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
						var xpos = (int)v3.x + x;
						var zpos = (int)v3.z + z;

						float slopedElevation;
						int plateauElevation;
						GenerateElevation(ref noise, xpos, zpos, out plateauElevation, out slopedElevation);

						if (plateauElevation < v3.y)
							continue;


						AssignBlocks(ref chunk, ref noise, ref solid, v3, x, z, plateauElevation);


						CreateRivers(ref chunk, ref noise, v3, x, z, plateauElevation);
						hasRoad = CreateRoads(ref chunk, ref noise, v3, x, z, plateauElevation);

					}
				}

				if (solid) {
					chunk.flags |= EChunkFlags.SOLID;
				}
				if (air) {
					chunk.flags |= EChunkFlags.AIR;
				}

				if (solid && air) {
					AddDecorations(v3, hasRoad, ref chunk, ref noise);
				}

				return chunk;
			}

			private static void CreateRamps(ref FastNoise_t noise, int xpos, int zpos, float smoothElevation, ref int elevation) {
				float diff = Mathf.Repeat(smoothElevation, plateauHeight);
				float slopeRange = 2f;
				if (diff < slopeRange) {
					float range = 0.01f;
					float power =
						0.4f * GetPerlinNormal(ref noise, xpos + 4235, zpos + 324576, NoiseFloatScale._01) +
						0.3f * GetPerlinNormal(ref noise, xpos + 254, zpos + 6563, NoiseFloatScale._005) +
						0.3f * GetPerlinNormal(ref noise, xpos + 224, zpos + 6476563, NoiseFloatScale._001);
					if (power > 0.5f - range && power < 0.5f + range) {
						float smoothedElevation = smoothElevation;
						for (int i = -2; i <= 2; i++) {
							for (int j = -2; j <= 2; j++) {
								if (i != 0 || j != 0) {
									smoothedElevation += GetRegionElevation(ref noise, xpos + i, zpos + j);
								}
							}
						}
						float baseElevation = smoothedElevation/25 - diff;
						elevation = (int)(diff / slopeRange * (elevation- baseElevation) + baseElevation);
					}
				}
			}

			private static void CreateRivers(ref PinnedChunkData_t chunk, ref FastNoise_t noise, Vector3 v3, int cx, int cz, int elevation) {
				if (elevation <= waterLevel)
					return;
				var x = (int)v3.x + cx;
				var z = (int)v3.z + cz;

				float powerScaleInverse = 0.005f;
				float roadWidthMin = 0.001f;
				float roadWidthRange = 0.1f;
				float humidity = GetHumidity(ref noise, x, z);
				float power =
					0.4f * GetPerlinNormal(ref noise, x, z, NoiseFloatScale._0005) +
					0.3f * GetPerlinNormal(ref noise, x + 25254, z + 65363, 0, powerScaleInverse * 0.5f) +
					0.3f * GetPerlinNormal(ref noise, x + 2254, z + 6563, 0, powerScaleInverse * 0.1f);
				float range =
					roadWidthMin + roadWidthRange * Mathf.Pow(0.2f * Mathf.Pow(humidity, 3) + 0.8f * (
					0.8f * GetPerlinNormal(ref noise, x + 7646745, z + 24, NoiseFloatScale._0005) +
					0.2f * GetPerlinNormal(ref noise, x + 7645, z + 234, NoiseFloatScale._01)), 4);

				if (power > 0.5f - range && power < 0.5f + range) {
					float depth = 0;
					if (power > 0.5f - range && power < 0.5f + range) {
						for (int i = elevation - 2; i <= elevation; i++) {
							int cy = (int)(i - v3.y);
							if (cy >= 0 && cy < VOXEL_CHUNK_SIZE_Y) {
								float temperature = GetTemperature(ref noise, x, i, z);
								int ofs = cx + (cz * VOXEL_CHUNK_SIZE_XZ) + ((int)(i - v3.y) * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
								if (IsFrozen(temperature, GetHumidity(ref noise, x, z))) {
									chunk.voxeldata[ofs] = EVoxelBlockType.Ice;
								}
								else {
									chunk.voxeldata[ofs] = EVoxelBlockType.Water;
								}
							}
						}
					}
				}
			}

			private static bool CreateRoads(ref PinnedChunkData_t chunk, ref FastNoise_t noise, Vector3 v3, int cx, int cz, int elevation) {
				if (elevation <= waterLevel)
					return false;
				var x = (int)v3.x + cx;
				var z = (int)v3.z + cz;

				float powerScaleInverse = 0.005f;
				float roadWidthMin = 0.001f;
				float roadWidthRange = 0.02f;
				float humidity = GetHumidity(ref noise, x, z);
				float power =
					0.4f * GetPerlinNormal(ref noise, x + 43, z+5634, NoiseFloatScale._0005) +
					0.3f * GetPerlinNormal(ref noise, x + 254, z + 6563, 0, powerScaleInverse * 0.5f) +
					0.3f * GetPerlinNormal(ref noise, x + 22554, z + 657463, 0, powerScaleInverse * 0.1f);
				float range =
					roadWidthMin + roadWidthRange * Mathf.Pow(0.2f * Mathf.Pow(humidity, 3) + 0.8f * (
					0.8f * GetPerlinNormal(ref noise, x + 766745, z + 2446, NoiseFloatScale._0005) +
					0.2f * GetPerlinNormal(ref noise, x + 75, z + 2344, NoiseFloatScale._01)), 4);

				if (power > 0.5f - range && power < 0.5f + range) {
					float depth = 0;
					if (power > 0.5f - range && power < 0.5f + range) {
						for (int i = elevation - 2; i <= elevation; i++) {
							int cy = (int)(i - v3.y);
							if (cy >= 0 && cy < VOXEL_CHUNK_SIZE_Y) {
								float temperature = GetTemperature(ref noise, x, i, z);
								int ofs = cx + (cz * VOXEL_CHUNK_SIZE_XZ) + ((int)(i - v3.y) * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
								chunk.voxeldata[ofs] = EVoxelBlockType.Dirt;
							}
						}
						return true;
					}
				}
				return false;
			}

			private static void AssignBlocks(ref PinnedChunkData_t chunk, ref FastNoise_t noise, ref bool solid, Vector3 v3, int x, int z, float elevation) {
				var xpos = (int)v3.x + x;
				var zpos = (int)v3.z + z;
				bool fullVoxel = true;

				for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
					var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
					var ypos = v3.y + y;

					EVoxelBlockType bt;

					if (ypos <= elevation) {
						bt = GetBlockType(ref noise, xpos, (int)ypos, zpos, elevation, out fullVoxel);
					}
					else {
						if (ypos < waterLevel) {
							float temperature = GetTemperature(ref noise, (int)xpos, (int)ypos, zpos);
							if (IsFrozen(temperature, GetHumidity(ref noise, xpos, zpos))) {
								bt = EVoxelBlockType.Ice;
							}
							else {
								bt = EVoxelBlockType.Water;
							}
						}
						else {
							bt = EVoxelBlockType.Air;
						}
					}
					if (bt != EVoxelBlockType.Air) {
						solid = true;
					}

					if (fullVoxel) {
						chunk.voxeldata[ofs] = bt.WithFlags(EVoxelBlockFlags.FullVoxel);
					}
					else {
						chunk.voxeldata[ofs] = bt;
					}
				}
			}

			private static void AddDecorations(Vector3 chunkPos, bool hasRoad, ref PinnedChunkData_t chunk, ref FastNoise_t noise) {
				for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
					for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
						var xpos = (int)chunkPos.x + x;
						var zpos = (int)chunkPos.z + z;
						for (int y = VOXEL_CHUNK_SIZE_Y - 1; y >= 0; --y) {
							var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
							var ypos = (int)chunkPos.y + y;
							var bt = chunk.voxeldata[ofs].type;
							if (bt != EVoxelBlockType.Air) {
								if (bt == EVoxelBlockType.Water || bt == EVoxelBlockType.Leaves || bt == EVoxelBlockType.Needles || bt == EVoxelBlockType.Wood)
									break;

								float rock = GetRock(ref noise, xpos, ypos, zpos);
								float rockCutoff = 0.35f;
								if (rock > rockCutoff) {
									float rockLimit = (1.0f - Mathf.Pow((rock - rockCutoff) / (1.0f - rockCutoff), 0.5f)) * 100 + 1;
									if (GetWhiteNoise(ref noise, xpos, ypos, zpos) < 1.0f / rockLimit) {
										BuildRock(ref noise, x, y, z, chunk);
									}
								}

								if (bt == EVoxelBlockType.Ice || bt == EVoxelBlockType.Sand)
									break;


								float humidity = GetHumidity(ref noise, xpos, zpos);
								float forestPower = (1.0f - (GetPerlinNormal(ref noise, xpos, zpos, NoiseFloatScale._01) * GetPerlinNormal(ref noise, xpos + 64325, zpos + 6543, NoiseFloatScale._005))) * Mathf.Pow(humidity, 1) * (1.0f - Mathf.Pow(rock, 2));
								float cutoff = 0.125f;
								if (forestPower > cutoff) {
									float forestLimit = Mathf.Pow(1.0f - (forestPower - cutoff) / (1.0f - cutoff), 8) * 400 + 8;
									if (GetWhiteNoise(ref noise, xpos, ypos, zpos) < 1.0f / forestLimit) {
										float temperature = GetTemperature(ref noise, xpos, ypos, zpos);

										int treeType; // dead
										if (humidity + temperature / 100.0f + GetPerlinNormal(ref noise, xpos, zpos, NoiseFloatScale._1) * 1.5f < 1.5f) {
											if (temperature + 30 * GetPerlinNormal(ref noise, xpos + 422, zpos + 5357, NoiseFloatScale._1) > 60)
												treeType = 3;
											else
												treeType = 2; // pine
										}
										else if (IsFrozen(temperature, humidity))
											treeType = 0;
										else
											treeType = 1;
										BuildTree(ref noise, x, y, z, treeType, chunk);
										break;
									}
								}

								if (bt == EVoxelBlockType.Grass || bt == EVoxelBlockType.Dirt) {
									if (y < VOXEL_CHUNK_SIZE_Y - 1) {
										float flowerPower1 = 0.3f * GetPerlinNormal(ref noise, xpos, zpos, NoiseFloatScale._5)
											+ 0.2f * GetPerlinNormal(ref noise, xpos + 67435, zpos + 653, NoiseFloatScale._01)
											+ 0.5f * GetPerlinNormal(ref noise, xpos + 6435, zpos + 65453, NoiseFloatScale._005);
										float flowerPower2 = 0.4f * GetPerlinNormal(ref noise, xpos + 256, zpos + 54764, NoiseFloatScale._5)
											+ 0.3f * GetPerlinNormal(ref noise, xpos + 6746435, zpos + 63, NoiseFloatScale._01)
											+ 0.3f * GetPerlinNormal(ref noise, xpos + 649835, zpos + 6543, NoiseFloatScale._005);
										float flowerPower3 = 0.2f * GetPerlinNormal(ref noise, xpos + 7657376, zpos + 5421, NoiseFloatScale._5)
											+ 0.3f * GetPerlinNormal(ref noise, xpos + 67435, zpos + 658963, NoiseFloatScale._01)
											+ 0.5f * GetPerlinNormal(ref noise, xpos + 64935, zpos + 695453, NoiseFloatScale._005);
										float flowerPower4 = 0.3f * GetPerlinNormal(ref noise, xpos + 15, zpos + 6532, NoiseFloatScale._5)
											+ 0.1f * GetPerlinNormal(ref noise, xpos + 6735, zpos + 63, NoiseFloatScale._01)
											+ 0.6f * GetPerlinNormal(ref noise, xpos + 645, zpos + 6553, NoiseFloatScale._005);
										cutoff = 0.75f;
										int flowerType = 0;
										float flowerPower = 0;
										if (flowerPower1 > flowerPower) {
											flowerPower = flowerPower1;
											flowerType = 0;
										}
										if (flowerPower2 > flowerPower) {
											flowerPower = flowerPower2;
											flowerType = 1;
										}
										if (flowerPower3 > flowerPower) {
											flowerPower = flowerPower3;
											flowerType = 2;
										}
										if (flowerPower4 > flowerPower) {
											flowerPower = flowerPower4;
											flowerType = 3;
										}
										if (flowerPower > cutoff) {
											var ofs2 = x + (z * VOXEL_CHUNK_SIZE_XZ) + ((y + 1) * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
											chunk.voxeldata[ofs2] = (EVoxelBlockType)((int)EVoxelBlockType.Flowers1 + flowerType);
										}
									}
								}
								//else if (blockType == BlockType.Grass && r.Next((int)(1000 * forestPower) + 10) == 0)
								//{
								//	chunk.setBlock(x, (byte)y, z, new Block(BlockType.RedFlower));
								//}
								//else if (blockType == BlockType.Grass && r.Next((int)(50 * forestPower) + 1) == 1)
								//{
								//	chunk.setBlock(x, (byte)y, z, new Block(BlockType.LongGrass));
								//}
								break;
							}
						}
					}
				}

				int voxelsPerTower = 600;
				Vector2Int towerRegion = new Vector2Int(Mathf.FloorToInt((chunkPos.x*VOXEL_CHUNK_SIZE_XZ)/ voxelsPerTower), Mathf.FloorToInt((chunkPos.z * VOXEL_CHUNK_SIZE_XZ) / voxelsPerTower));
				Vector2Int towerWorldPosition = new Vector2Int(towerRegion.x*voxelsPerTower+(int)(GetWhiteNoise(ref noise, towerRegion.x, 0, towerRegion.y) * voxelsPerTower), towerRegion.y * voxelsPerTower + (int)(GetWhiteNoise(ref noise, towerRegion.x+100, 0, towerRegion.y) * voxelsPerTower));
				var towerChunkPos = new Vector2Int(towerWorldPosition.x / VOXEL_CHUNK_SIZE_XZ, towerWorldPosition.y / VOXEL_CHUNK_SIZE_XZ);
				if (towerChunkPos.x == chunkPos.x && towerChunkPos.y == chunkPos.z) {
					int towerHeight = (int)(GetWhiteNoise(ref noise, chunkPos.x+100, chunkPos.y, chunkPos.z) * 30 + 12);
					int x = 8;
					int z = 8;
					for (int y = VOXEL_CHUNK_SIZE_Y - 1; y >= 0; --y) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						var bt = chunk.voxeldata[ofs].type;
						if (bt != EVoxelBlockType.Air) {
							ConstructTower(ref noise, chunkPos, x, y, z, towerHeight, ref chunk);
							break;
						}
					}
				}
				else if (hasRoad && GetWhiteNoise(ref noise, chunkPos.x + 63452, 0, chunkPos.z) < 0.003f) {
					int x = 8;
					int z = 8;
					for (int y = VOXEL_CHUNK_SIZE_Y - 1; y >= 0; --y) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						var bt = chunk.voxeldata[ofs].type;
						if (bt != EVoxelBlockType.Air) {
							ConstructStore(ref noise, chunkPos, x, y, z, ref chunk);
							break;
						}
					}
				}
				else if (GetWhiteNoise(ref noise, chunkPos.x + 6345, 0, chunkPos.z) < 0.002f) {
					chunk.AddDecoration(
						new Decoration_t() {
							pos = chunkPos + new Vector3(VOXEL_CHUNK_SIZE_XZ / 2, VOXEL_CHUNK_SIZE_Y - 1, VOXEL_CHUNK_SIZE_XZ / 2),
							type = EDecorationType.Horse
						}
					);
				}
				else if (GetWhiteNoise(ref noise, chunkPos.x + 632, 0, chunkPos.z) < 0.01f) {
					chunk.AddDecoration(
						new Decoration_t() {
							pos = chunkPos + new Vector3(VOXEL_CHUNK_SIZE_XZ / 2, VOXEL_CHUNK_SIZE_Y - 1, VOXEL_CHUNK_SIZE_XZ / 2),
							type = EDecorationType.Chest
						}
					);
				}
			}

			private static void GenerateElevation(ref FastNoise_t noise, int xpos, int zpos, out int plateauElevation, out float slopedElevation) {

				slopedElevation = GetRegionElevation(ref noise, xpos, zpos);
				plateauElevation = Mathf.CeilToInt(slopedElevation / plateauHeight)*plateauHeight;

				CreateRamps(ref noise, xpos, zpos, slopedElevation, ref plateauElevation);


				GetLocalElevation(ref noise, xpos, zpos, ref plateauElevation, ref slopedElevation);
			}

			static EVoxelBlockType GetBlockType(ref FastNoise_t noise, int x, int y, int z, float upperGroundHeight, out bool fullVoxel) {

                fullVoxel = false;
				float humidity = GetHumidity(ref noise, x, z);
				if (y == upperGroundHeight + 1) {
					float temperature = GetTemperature(ref noise, x, y, z);
					if (IsFrozen(temperature, humidity)) {
						if (IsSnow(ref noise, x, y, z))
							return EVoxelBlockType.Snow;
						else
							return EVoxelBlockType.Ice;
					}
				}

				if (y == upperGroundHeight && y < waterLevel + 10 * (GetPerlinNormal(ref noise, x, y, z, 0.2f) * GetPerlinNormal(ref noise, x + 452, y + 784, z + 6432, 0.1f))) {
					return EVoxelBlockType.Sand;
				}


				float rock = GetRock(ref noise, x, y, z);
				if (y < upperGroundHeight) {
					if (rock > 0.5f) {
                        return EVoxelBlockType.Rock;
					}
					return EVoxelBlockType.Dirt;
				}
				if (humidity < 0.25f) {
					return EVoxelBlockType.Sand;
				} else if ((0.95f * GetPerlinNormal(ref noise, x, y, z, 0.01f) + 0.05f * GetPerlinNormal(ref noise, x + 5432, y + 874423, z + 12, 0.1f)) * humidity * Mathf.Pow(rock, 0.25f) < 0.1f) {
                    if (rock > 0.5f)
                    {
                        fullVoxel = GetWhiteNoise(ref noise, x, y, z) > 0.5;
                        return EVoxelBlockType.Rock;
                    } else
                    {
                        return EVoxelBlockType.Dirt;
                    }
                } else
                {
					return EVoxelBlockType.Grass;
				}

				//	return EVoxelBlockType.BLOCK_TYPE_DIRT;

			}

			const int plateauHeight = 6;
			const int maxRegionHeight = 1024;
			const float islandRadius = 1000;
			static float GetRegionElevation(ref FastNoise_t noise, int x, int z) {

				//float distToOriginSquared = Mathf.Clamp01(1.0f-(Mathf.Pow(x, 2) + Mathf.Pow(z, 2))/(islandRadius* islandRadius));
				//float distToOrigin = Mathf.Sqrt(distToOriginSquared);

				float elevation = 0;
				elevation += GetPerlinNormal(ref noise, x, z, 0.001f) * 0.05f;
				elevation += GetPerlinNormal(ref noise, x, z, 0.0002f) * 0.05f;
				elevation += GetPerlinNormal(ref noise, x, z, 0.0001f) * 0.35f;
				elevation += GetPerlinNormal(ref noise, x, z, 0.00002f) * 0.55f;

				elevation *= maxRegionHeight;


				elevation += GetPerlinNormal(ref noise, x, z, 0.1f) * 4;
				elevation += GetPerlinNormal(ref noise, x, z, 0.05f) * 8;
				elevation += GetPerlinNormal(ref noise, x, z, 0.01f) * 16;
				elevation += GetPerlinNormal(ref noise, x, z, 0.005f) * 24;

				return elevation - maxRegionHeight / 2;
			}

			static void GetLocalElevation(ref FastNoise_t noise, int x, int z, ref int plateauElevation, ref float slopedElevation) {

				float hillElevation = 0;
				hillElevation += GetPerlinNormal(ref noise, x, z, NoiseFloatScale._01);
				hillElevation += GetPerlinNormal(ref noise, x, z, NoiseFloatScale._05);
				hillElevation += GetPerlinNormal(ref noise, x, z, NoiseFloatScale._1);

				slopedElevation += (int)hillElevation;
				plateauElevation += (int)hillElevation;
			}

			static float CalculatePlateauPower(ref FastNoise_t noise, int x, int z, float startingHeight, float plateauRegionSize, float detailSize, float regionPower, float plateauHorizontalScale, float maxPlateau, int plateauStepMax, int plateauStepMin, int offsetX, int offsetZ) {
				float inverseRegionSize = 1.0f / plateauRegionSize;
				float inversePlateauScale = 1.0f / plateauHorizontalScale;
				float plateauRegionPower = Mathf.Pow(GetPerlinNormal(ref noise, x + 100 + offsetX, z + offsetZ, 0, inverseRegionSize), regionPower);

				if (detailSize > 0) {
					float inverseDetailSize = 1.0f / detailSize;
					plateauRegionPower *= GetPerlinNormal(ref noise, x + 157400 + offsetX, z + 54254 + offsetZ, 0, inverseDetailSize);
				}

				float plateauStep = plateauStepMin + (plateauStepMax - plateauStepMin) * GetPerlinNormal(ref noise, x + 1000 + offsetX, z + 1000 + offsetZ, 0, inversePlateauScale);

				float newHeight = (int)((startingHeight + plateauRegionPower * maxPlateau) / plateauStep) * plateauStep - startingHeight;
				return Mathf.Max(0, newHeight);
			}

			static float CalculateHillPower(ref FastNoise_t noise, int x, int z, float regionSize, float regionPower, float hillSizeHorizontal, float hillHeight, float minSteepness, float maxSteepness, int offsetX, int offsetY) {
				float regionScaleInverse = 1.0f / regionSize;
				float hillScaleInverse = 1.0f / hillSizeHorizontal;

				float hillRegionPower = Mathf.Pow(GetPerlinNormal(ref noise, x + 1040 + offsetX, (z + 3234 + offsetY), 0, regionScaleInverse), regionPower);
				float hillRegionSteepness = GetPerlinNormal(ref noise, x + 100 + offsetX, z + 3243 + offsetY, 0, regionScaleInverse) * (maxSteepness - minSteepness) + minSteepness;
				float height = GetPerlinNormal(ref noise, x + 10 + offsetX, z + 1070 + offsetY, 0, hillScaleInverse) * hillHeight;
				float hill = (1.0f / (1.0f + Mathf.Exp(-hillRegionSteepness * (GetPerlinNormal(ref noise, x + 180 + offsetX, z + 180 + offsetY, 0, hillScaleInverse) - 0.5f)))) * height * hillRegionPower;
				return hill;
			}


			static float GetWhiteNoise(ref FastNoise_t noise, float x, float y, float z) {
				var v = noise.GetWhiteNoise(x, y, z);
				return (v + 1) / 2;
			}

			static float GetPerlinNormal(ref FastNoise_t noise, float x, float y, float z, float scale) {
				noise.SetFrequency(scale);
				var v = noise.GetPerlin(x, y, z);
				return (v + 1) / 2;
			}
			static float GetPerlinValue(ref FastNoise_t noise, float x, float y, float z, float scale) {
				noise.SetFrequency(scale);
				var v = noise.GetPerlin(x, y, z);
				return v;
			}
			static float GetPerlinNormal(ref FastNoise_t noise, float x, float y, float scale) {
				noise.SetFrequency(scale);
				float v = noise.GetPerlin(x, y);
				return (v + 1) / 2;
			}
			//static float GetPerlinValue(int x, int y, NoiseFloatScale scale)
			//{
			//	const float v = noiseFloatsPregen[scale][x + y * noiseFloatPregenSize];
			//	return v;
			//}



			static bool IsFrozen(float temperature, float humidity) {
				return temperature < 32 && humidity > 0.25f;
			}

			static bool IsSnow(ref FastNoise_t noise, int x, int y, int z) {
				float snow =
					0.2f * GetPerlinValue(ref noise, x, y, z, 0.5f) +
					0.8f * GetPerlinValue(ref noise, x, y, z, 0.01f);
				return (snow > -0.5f);
			}

			static float GetRock(ref FastNoise_t noise, int x, int y, int z) {
				return 0.2f * GetPerlinNormal(ref noise, x, y, z, 0.1f) +
					0.8f * GetPerlinNormal(ref noise, x, y, z, 0.001f);
			}

			static float GetHumidity(ref FastNoise_t noise, int x, int z) {
				return
					Mathf.Pow(0.05f * GetPerlinNormal(ref noise, (x + 4342), (z + 87886), NoiseFloatScale._5) +
					0.15f * GetPerlinNormal(ref noise, (x + 42), (z + 8786), NoiseFloatScale._01) +
					0.8f * GetPerlinNormal(ref noise, (x + 3423), (z + 123142), NoiseFloatScale._001), 1.25f);
			}

			static float GetTemperature(ref FastNoise_t noise, int x, int y, int z) {
				float temperature = 0 - (int)(80 * y / maxRegionHeight) +
					5 * GetPerlinNormal(ref noise, (x + 432), (z + 8786), NoiseFloatScale._5) +
					20 * GetPerlinNormal(ref noise, (x + 1540), (z + 76846), NoiseFloatScale._01) +
					110 * GetPerlinNormal(ref noise, (x + 1454), (z + 766), NoiseFloatScale._001);
				return temperature;
			}

			static void BuildRock(ref FastNoise_t noise, int x, int y, int z, PinnedChunkData_t chunk) {
				int xMinus = x - y % 3;
				int xPlus = x + (x + y + z) % 3;
				int yMinus = y - x % 3;
				int yPlus = y + (y + z) % 3;
				int zMinus = z - (z + x) % 3;
				int zPlus = z + (x + y) % 3;
				for (int i = xMinus; i < xPlus; i++) {
					for (int j = yMinus; j < yPlus; j++) {
						for (int k = zMinus; k < zPlus; k++) {
							if (i >= 0 && i < VOXEL_CHUNK_SIZE_XZ && j >= 0 && j < VOXEL_CHUNK_SIZE_Y && k >= 0 && k < VOXEL_CHUNK_SIZE_XZ) {
								var ofs = i + (k * VOXEL_CHUNK_SIZE_XZ) + (j * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
								chunk.flags |= EChunkFlags.SOLID;
								chunk.voxeldata[ofs] = EVoxelBlockType.Rock.WithFlags(EVoxelBlockFlags.FullVoxel);
							}
						}
					}
				}
			}

			static void ConstructTower(ref FastNoise_t noise, Vector3 chunkPos, int x, int y, int z, int towerHeight, ref PinnedChunkData_t chunk) {

				for (int i = -1; i <= 1; i++) {
					for (int j = -1; j <= 1; j++) {
						Vector3Int pos = new Vector3Int(x + i, 0, z + j);
						int groundHeight = 0;
						for (int k = towerHeight; k >= groundHeight; k--) {
							pos.y = k + y;
							if (pos.x >= 0 && pos.x < VOXEL_CHUNK_SIZE_XZ && pos.z >= 0 && pos.z < VOXEL_CHUNK_SIZE_XZ && pos.y >= 0 && pos.y < VOXEL_CHUNK_SIZE_Y) {
								var ofs = pos.x + (pos.z * VOXEL_CHUNK_SIZE_XZ) + (pos.y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
								chunk.flags |= EChunkFlags.SOLID;
								if (k == towerHeight) {
									chunk.voxeldata[ofs] = EVoxelBlockType.Rock.WithFlags(EVoxelBlockFlags.FullVoxel);
								}
								else {
									chunk.voxeldata[ofs] = EVoxelBlockType.Rock;
								}
							}
						}
					}
				}
				for (int i = -4; i <= 4; i++) {
					for (int j = -4; j <= 4; j++) {
						if (i > 1 || i < -1 || j > 1 || j < -1) {
							Vector3Int pos = new Vector3Int(x + i, 0, z + j);
							int groundHeight = 0;
							for (int k = towerHeight; k >= groundHeight; k--) {
								int stepIndex = 0;
								if (j < -1) {
									if (i < -1)
										stepIndex = 0;
									else if (i < 2)
										stepIndex = 1;
									else
										stepIndex = 2;
								}
								else if (j < 2) {
									if (i > 1) {
										stepIndex = 3;
									}
									else {
										stepIndex = 7;
									}
								}
								else {
									if (i > 1) {
										stepIndex = 4;
									}
									else if (i > -2) {
										stepIndex = 5;
									}
									else {
										stepIndex = 6;
									}
								}
								int modElevation = (k + stepIndex) % 7;

								pos.y = k + y;
								if (pos.x >= 0 && pos.x < VOXEL_CHUNK_SIZE_XZ && pos.z >= 0 && pos.z < VOXEL_CHUNK_SIZE_XZ && pos.y >= 0 && pos.y < VOXEL_CHUNK_SIZE_Y) {
									var ofs = pos.x + (pos.z * VOXEL_CHUNK_SIZE_XZ) + (pos.y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
									if (modElevation < 3) {
										if (i == -4 || i == 4 || j == -4 || j == 4) {
											chunk.voxeldata[ofs] = EVoxelBlockType.Rock.WithFlags(EVoxelBlockFlags.FullVoxel);
										}
										else if (modElevation == 2) {
											chunk.voxeldata[ofs] = EVoxelBlockType.Air;
										}
										else {
											chunk.voxeldata[ofs] = EVoxelBlockType.Rock;
										}
										chunk.flags |= EChunkFlags.SOLID;
									}
									else {
										chunk.voxeldata[ofs] = EVoxelBlockType.Air;
									}
								}
							}
						}
					}
				}

				{
					chunk.AddDecoration(new Decoration_t() {
						pos = chunkPos + new Vector3(x + 0.5f, y + towerHeight + 2, z + 0.5f),
						type = EDecorationType.MapReveal
					});
				}
			}

			static void ConstructStore(ref FastNoise_t noise, Vector3 chunkPos, int x, int y, int z, ref PinnedChunkData_t chunk) {

				int height = 8;
				for (int i = -6; i <= 6; i++) {
					for (int j = -6; j <= 0; j++) {
						Vector3Int pos = new Vector3Int(x + i, 0, z + j);
						int groundHeight = 0;
						for (int k = height; k >= groundHeight; k--) {
							pos.y = k + y;
							if (pos.x >= 0 && pos.x < VOXEL_CHUNK_SIZE_XZ && pos.z >= 0 && pos.z < VOXEL_CHUNK_SIZE_XZ && pos.y >= 0 && pos.y < VOXEL_CHUNK_SIZE_Y) {
								var ofs = pos.x + (pos.z * VOXEL_CHUNK_SIZE_XZ) + (pos.y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
								chunk.flags |= EChunkFlags.SOLID;
								chunk.voxeldata[ofs] = EVoxelBlockType.Wood.WithFlags(EVoxelBlockFlags.FullVoxel);
							}
						}
					}
				}

				{
					chunk.AddDecoration(new Decoration_t() {
						pos = chunkPos + new Vector3(x + 0.5f, y + 2, z + 1.5f),
						type = EDecorationType.Merchant
					});
				}
			}

			static void BuildTree(ref FastNoise_t noise, int x, int y, int z, int treeType, PinnedChunkData_t chunk) {


				// Foliage
				if (treeType == 0) {
					// Trunk
					int height = 1 + x % 4 + y % 4 + z % 4;
					for (int i = y; i < y + height && i < VOXEL_CHUNK_SIZE_Y; i++) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (i * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						chunk.flags |= EChunkFlags.SOLID;
						chunk.voxeldata[ofs] = EVoxelBlockType.Wood;
					}
				} else if (treeType == 1) {
					// Trunk
					int height = 2 + x % 3 + y % 3 + z % 3;
					for (int i = y; i < y + height && i < VOXEL_CHUNK_SIZE_Y; i++) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (i * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						chunk.flags |= EChunkFlags.SOLID;
						chunk.voxeldata[ofs] = EVoxelBlockType.Wood;
					}
					int radius = x % 2 + y % 2 + z % 2;
					if (radius > 0) {
						for (int i = -radius; i <= radius; i++) {
							for (int j = -radius; j <= radius; j++) {
								for (int k = -radius; k <= radius; k++) {
									int lx = x + i;
									int lz = z + j;
									int ly = y + k + height;
									if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
										var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
										chunk.flags |= EChunkFlags.SOLID;
										chunk.voxeldata[ofs] = EVoxelBlockType.Leaves.WithFlags(EVoxelBlockFlags.FullVoxel);
									}
								}
							}
						}
					}
				} else if (treeType == 2) {
					int height = 3 + x % 2 + y % 2 + z % 2;
					for (int i = y; i < y + height && i < VOXEL_CHUNK_SIZE_Y; i++) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (i * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						chunk.flags |= EChunkFlags.SOLID;
						chunk.voxeldata[ofs] = EVoxelBlockType.Wood;
					}
					int radius = 1 + x % 2 + y % 2 + z % 2;
					if (radius > 0) {
						for (int k = -radius; k <= radius * 2; k++) {
							int r = radius - (k + radius) / 3;
							for (int i = -r; i <= r; i++) {
								for (int j = -r; j <= r; j++) {
									int lx = x + i;
									int ly = y + k + height;
                                    int lz = z + j;
                                    if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
										var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
										chunk.flags |= EChunkFlags.SOLID;
										chunk.voxeldata[ofs] = EVoxelBlockType.Needles;
									}

								}
							}
						}
					}
				} else if (treeType == 3) {
					int height = 4 + x % 10 + y % 10 + z % 10;
					for (int i = y; i < y + height && i < VOXEL_CHUNK_SIZE_Y; i++) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (i * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						chunk.flags |= EChunkFlags.SOLID;
						chunk.voxeldata[ofs] = EVoxelBlockType.Wood;
					}
					for (int i = 3; i <= height; i++) {
						int radius = (int)((1.0f - (float)(i - 3) / (height - 3)) * height / 3 + 1) + (x + y + z * z + i * i) % 2;
						Vector2Int branchDir;
						float density = (float)((x + y + z + i * i) % 4 + 1) / 6f;
						if (radius > 1) {
							for (int dir = 0; dir < 4; dir++) {
								if ((x + y + z + i + dir + radius) % 6 < 6 * density) {
									if (dir == 0)
										branchDir = new Vector2Int(0, 1);
									else if (dir == 1)
										branchDir = new Vector2Int(0, -1);
									else if (dir == 2)
										branchDir = new Vector2Int(-1, 0);
									else
										branchDir = new Vector2Int(1, 0);
									int ly = y + i;
									for (int k = 1; k <= radius; k++) {
										int lx = x + branchDir.x * k;
										int lz = z + branchDir.y * k;
										if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
											var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
											chunk.flags |= EChunkFlags.SOLID;
											chunk.voxeldata[ofs] = k == 1 ? EVoxelBlockType.Wood : EVoxelBlockType.Needles.WithFlags(EVoxelBlockFlags.FullVoxel);
										}
										if (k < radius) {
											lx = x + branchDir.x * k + branchDir.y;
											lz = z + branchDir.y * k + branchDir.x;
											if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
												var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
												chunk.flags |= EChunkFlags.SOLID;
												chunk.voxeldata[ofs] = EVoxelBlockType.Needles.WithFlags(EVoxelBlockFlags.FullVoxel);
											}
											lx = x + branchDir.x * k - branchDir.y;
											lz = z + branchDir.y * k - branchDir.x;
											if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
												var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
												chunk.flags |= EChunkFlags.SOLID;
												chunk.voxeldata[ofs] = EVoxelBlockType.Needles.WithFlags(EVoxelBlockFlags.FullVoxel);
											}
										}
									}
								}
							}
						}
					}
				}
			}


		}
	}
}