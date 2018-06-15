using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class World {
	partial class ChunkMeshGen {

		const int noiseFloatPregenSize = 512;
        const float waterLevel = 64;

		static PinnedChunkData_t GenerateVoxels(WorldChunkPos_t cpos, PinnedChunkData_t chunk) {
			//return GenerateVoxelsSinWave(cpos, chunk);
			return GenerateVoxels_V1(cpos, chunk);
		}

	static PinnedChunkData_t GenerateVoxelsSinWave(WorldChunkPos_t cpos, PinnedChunkData_t chunk) {
			var Y_OFS = (VOXEL_CHUNK_SIZE_Y * MaxVoxelChunkLine(VOXEL_CHUNK_VIS_MAX_Y)) / 8;

			bool solid = false;
			bool air = false;

			chunk.flags = EChunkFlags.NONE;

			WorldVoxelPos_t pos = ChunkToWorld(cpos);
			Vector3 v3 = WorldToVec3(pos);

			for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
				for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
					var cs = Mathf.Cos((v3.x + x) / 64);
					var ss = Mathf.Sin((v3.z + z) / 64);

					for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
						var ypos = v3.y + y;

						if (ypos < ((cs + ss) * (Y_OFS/2))) {
							chunk.blocktypes[ofs] = EVoxelBlockType.DIRT/*|EVoxelBlockType.FULL_VOXEL_FLAG*/;
							solid = true;
						} else {
							air = true;
							chunk.blocktypes[ofs] = EVoxelBlockType.AIR;
						}
					}
				}
			}

			if (solid) {
				chunk.flags |= EChunkFlags.SOLID;
			}

			if (air) {
				chunk.flags |= EChunkFlags.AIR;
			}

			return chunk;
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

		static PinnedChunkData_t GenerateVoxels_V1(WorldChunkPos_t cpos, PinnedChunkData_t chunk) {
			FastNoise_t noise = FastNoise_t.New();

			chunk.flags = EChunkFlags.NONE;
            bool solid = false;
            bool air = true;

            var wpos = ChunkToWorld(cpos);
            var v3 = WorldToVec3(wpos);

            for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
                for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
                    var xpos = (int)v3.x + x;
                    var zpos = (int)v3.z + z;

                    bool isEmptyAtBottom = false;
                    var lowerGroundHeight = GetLowerGroundHeight(ref noise, xpos, zpos);
                    var upperGroundHeight = GetUpperGroundHeight(ref noise, xpos, zpos, lowerGroundHeight);
                    float waterDepth = CalculateRiver(ref noise, xpos, zpos, lowerGroundHeight, 0, 0, ref upperGroundHeight);
                    bool isRoad = CalculateRoad(ref noise, xpos, zpos, lowerGroundHeight, 0, 0, ref upperGroundHeight);

                    var minimapBlock = EVoxelBlockType.AIR;

                    for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
                        var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
                        var ypos = v3.y + y;

                        EVoxelBlockType bt;
                        bool isCave = false;
                        if (ypos > lowerGroundHeight && ypos <= upperGroundHeight) {
                            // Let's see about some caves er valleys!
                            float caveNoise = GetPerlinValue(ref noise, (int)xpos, (int)ypos, (int)zpos, 0.01f) * (0.015f * ypos) + 0.1f;
                            caveNoise += GetPerlinValue(ref noise, (int)xpos, (int)ypos, (int)zpos, 0.1f) * 0.06f + 0.1f;
                            caveNoise += GetPerlinValue(ref noise, (int)xpos, (int)ypos, (int)zpos, 0.2f) * 0.02f + 0.01f;
                            isCave = caveNoise > GetPerlinNormal(ref noise, (int)xpos, (int)ypos, (int)zpos, 0.01f) * 0.3f + 0.4f;
                        }

                        if (ypos <= upperGroundHeight && !isCave) {
                            bt = GetBlockType(ref noise, xpos, (int)ypos, zpos, (int)upperGroundHeight, isRoad, false);
                        }
                        else {
                            if (ypos < waterLevel) {
                                float temperature = GetTemperature(ref noise, (int)xpos, (int)ypos, (int)zpos);
                                if (IsFrozen(temperature, GetHumidity(ref noise, (int)xpos, (int)zpos))) {
                                    bt = EVoxelBlockType.ICE;
                                }
                                else {
                                    bt = EVoxelBlockType.WATER;
                                }
                            }
                            else {
                                bt = EVoxelBlockType.AIR;
                            }
                        }
                        if (bt == EVoxelBlockType.AIR) {
                            if (y == 0) {
                                isEmptyAtBottom = true;
                            }
                            if (waterDepth > 0 && !isEmptyAtBottom) {
                                float temperature = GetTemperature(ref noise, (int)xpos, (int)ypos, (int)zpos);
                                if (IsFrozen(temperature, GetHumidity(ref noise, (int)xpos, (int)zpos))) {
                                    bt = EVoxelBlockType.ICE;
                                }
                                else {
                                    bt = EVoxelBlockType.WATER;
                                }
                                waterDepth--;
                                solid = true;
                            }
                            else {
                                air = true;
                            }
                        }
                        else {
                            solid = true;
                        }

                        //bt |= BLOCK_FULL_VOXEL_FLAG;

                        if ((EVoxelBlockType)((byte)bt & BLOCK_TYPE_MASK) != EVoxelBlockType.AIR) {
                            minimapBlock = bt;
                        }

                        chunk.blocktypes[ofs] = bt;
                    }

                    //if (minimap) {
                    //    (*minimap)[x + (y * WORLD_VOXEL_CHUNK_SIZE_XY)] = minimapBlock;
                    //}
                }
            }

            if (solid) {
                chunk.flags |= EChunkFlags.SOLID;
            }
            if (air) {
                chunk.flags |= EChunkFlags.AIR;
            }

            if (solid && air) {
                for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
                    for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
                        var xpos = (int)v3.x + x;
                        var zpos = (int)v3.z + z;
                        for (int y = VOXEL_CHUNK_SIZE_Y - 1; y >= 0; --y) {
                            var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
                            var ypos = (int)v3.y + y;
                            var bt = (EVoxelBlockType)((byte)chunk.blocktypes[ofs] & BLOCK_TYPE_MASK);
                            if (bt != EVoxelBlockType.AIR) {
                                if (bt == EVoxelBlockType.WATER || bt == EVoxelBlockType.LEAVES || bt == EVoxelBlockType.NEEDLES || bt == EVoxelBlockType.WOOD)
                                    break;

                                float rock = GetRock(ref noise, xpos, ypos, zpos);
                                float rockCutoff = 0.35f;
                                if (rock > rockCutoff) {
                                    float rockLimit = (1.0f - Mathf.Pow((rock - rockCutoff) / (1.0f - rockCutoff), 0.5f)) * 100 + 1;
                                    if (GetWhiteNoise(ref noise, xpos, ypos, zpos) < 1.0f / rockLimit) {
                                        BuildRock(ref noise, x, y, z, chunk);
                                    }
                                }

                                if (bt == EVoxelBlockType.ICE || bt == EVoxelBlockType.SAND)
                                    break;


                                float humidity = GetHumidity(ref noise, xpos, zpos);
                                float forestPower = (1.0f - (GetPerlinNormal(ref noise, xpos, zpos, NoiseFloatScale._01) * GetPerlinNormal(ref noise, xpos + 64325, zpos + 6543, NoiseFloatScale._005))) * Mathf.Pow(humidity, 2) * (1.0f - Mathf.Pow(rock, 4));
                                float cutoff = 0.2f;
                                if (forestPower > cutoff) {
                                    float forestLimit = Mathf.Pow(1.0f - (forestPower - cutoff) / (1.0f - cutoff), 8) * 100 + 4;
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

                                if (bt == EVoxelBlockType.GRASS || bt == EVoxelBlockType.DIRT) {
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
                                            chunk.flags |= EChunkFlags.SOLID;
                                            chunk.blocktypes[ofs2] = (EVoxelBlockType)((int)EVoxelBlockType.FLOWERS1 + flowerType);
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
            }

			return chunk;
		}

		static EVoxelBlockType GetBlockType(ref FastNoise_t noise, int x, int y, int z, int upperGroundHeight, bool isRoad, bool isRiver) {

			float humidity = GetHumidity(ref noise, x, y);
			if (z == upperGroundHeight + 1) {
				float temperature = GetTemperature(ref noise, x, y, z);
				if (IsFrozen(temperature, humidity)) {
					if (IsSnow(ref noise, x, y, z))
						return EVoxelBlockType.SNOW;
					else
						return EVoxelBlockType.ICE;
				}
			}

			if (z == upperGroundHeight && z < waterLevel + 10 * (GetPerlinNormal(ref noise, x, y, z, 0.2f) * GetPerlinNormal(ref noise, x + 452, y + 784, z + 6432, 0.1f))) {
				return EVoxelBlockType.SAND;
			}


			float rock = GetRock(ref noise, x, y, z);
			if (z < upperGroundHeight) {
				if (rock > 0.5f) {
					return EVoxelBlockType.ROCK;
				}
				return EVoxelBlockType.DIRT;
			}
			if (isRoad) {
				return EVoxelBlockType.DIRT;
			} else if (humidity < 0.25f) {
				return EVoxelBlockType.SAND;
			} else if ((0.95f * GetPerlinNormal(ref noise, x, y, z, 0.01f) + 0.05f * GetPerlinNormal(ref noise, x + 5432, y + 874423, z + 12, 0.1f)) * humidity * Mathf.Pow(rock, 0.25f) < 0.1f) {
				if (rock > 0.5f)
					return EVoxelBlockType.ROCK;
				else
					return EVoxelBlockType.DIRT;
			} else {
				return EVoxelBlockType.GRASS;
			}

			//	return EVoxelBlockType.BLOCK_TYPE_DIRT;

		}


		static float GetLowerGroundHeight(ref FastNoise_t noise, int x, int y) {
			int maxGroundHeight = 128;

			float distToOriginSquared = 1.0f - Mathf.Sqrt((float)(Mathf.Pow(x, 2) + Mathf.Pow(y, 2))) / GetDistImportance(ref noise, x, y);
			distToOriginSquared = Mathf.Clamp(distToOriginSquared, 0f, 1f);

			float lowerGroundHeight = 0;
			lowerGroundHeight += GetPerlinNormal(ref noise, x, y, NoiseFloatScale._001) * 0.55f * distToOriginSquared;
			lowerGroundHeight += GetPerlinNormal(ref noise, x, y, NoiseFloatScale._005) * 0.35f * distToOriginSquared;
			lowerGroundHeight += GetPerlinNormal(ref noise, x, y, 0, 0.02f) * 0.05f;
			lowerGroundHeight += GetPerlinNormal(ref noise, x, y, NoiseFloatScale._1) * 0.05f;

			lowerGroundHeight *= maxGroundHeight;


			return lowerGroundHeight + 1;


		}

		static float GetUpperGroundHeight(ref FastNoise_t noise, int x, int y, float lowerGroundHeight) {
			float mountainHeight = lowerGroundHeight;

			mountainHeight += CalculatePlateauPower(ref noise, x, y, mountainHeight, 6000, 0, 1, 1000, 128, 64, 1, 78456, 14);
			mountainHeight += CalculateHillPower(ref noise, x, y, 6000, 2, 500, 256, 1, 5, 0, 0);
			mountainHeight -= 128 * GetPerlinNormal(ref noise, x + 1000, y + 4395, NoiseFloatScale._001) * GetPerlinNormal(ref noise, x + 18000, y + 43095, NoiseFloatScale._0005);
			mountainHeight += CalculatePlateauPower(ref noise, x, y, mountainHeight, 6000, 100, 3, 1000, 128, 64, 1, 7846, 1464);
			mountainHeight -= 64 * GetPerlinNormal(ref noise, x + 100, y + 435, NoiseFloatScale._001) * GetPerlinNormal(ref noise, x + 1000, y + 4095, NoiseFloatScale._0005);
			mountainHeight += CalculatePlateauPower(ref noise, x, y, mountainHeight, 2000, 100, 2, 1000, 32, 16, 8, 736, 3242);
			mountainHeight -= 16 * GetPerlinNormal(ref noise, x + 100, y + 435, NoiseFloatScale._005) * GetPerlinNormal(ref noise, x + 1070, y + 43905, 0, 0.0025f);
			mountainHeight += CalculateHillPower(ref noise, x, y, 1000, 3, 100, 64, 1, 10, 2554, 7648);
			mountainHeight += CalculatePlateauPower(ref noise, x, y, mountainHeight, 2000, 100, 3, 1000, 16, 8, 1, 7336, 32842);
			mountainHeight -= 4 * GetPerlinNormal(ref noise, x + 10670, y + 4385, 0, 0.02f) * GetPerlinNormal(ref noise, x + 1070, y + 485, NoiseFloatScale._01);
			mountainHeight -= 3 * (1.0f - Mathf.Pow(2.0f * Mathf.Min(0.5f, GetRock(ref noise, x, y, (int)mountainHeight)), 3));

			//float hillHeight = lowerGroundHeight;
			//hillHeight += 32 * GetPerlinNormal(x + 100, y, NoiseFloatScale._01);
			//hillHeight += 16 * GetPerlinNormal(x + 100, y, 0, 0.02f);
			//hillHeight += 8 * GetPerlinNormal(x + 100, y, NoiseFloatScale._1);

			//float hillInfluence = Mathf.Pow(GetPerlinNormal(x + 104350, y, NoiseFloatScale._001), 3);
			//float curHeight = hillHeight * hillInfluence + (1.f - hillInfluence) * mountainHeight;
			float curHeight = mountainHeight;

			float distToOriginSquared = 1.0f - Mathf.Sqrt((float)(Mathf.Pow(x, 2) + Mathf.Pow(y, 2))) / GetDistImportance(ref noise, x, y);
			distToOriginSquared = Mathf.Clamp(distToOriginSquared, 0f, 1f);
			curHeight *= distToOriginSquared;


			return curHeight;

		}

		static float CalculatePlateauPower(ref FastNoise_t noise, int x, int y, float startingHeight, float plateauRegionSize, float detailSize, float regionPower, float plateauHorizontalScale, float maxPlateau, int plateauStepMax, int plateauStepMin, int offsetX, int offsetY) {
			float inverseRegionSize = 1.0f / plateauRegionSize;
			float inversePlateauScale = 1.0f / plateauHorizontalScale;
			float plateauRegionPower = Mathf.Pow(GetPerlinNormal(ref noise, x + 100 + offsetX, y + offsetY, 0, inverseRegionSize), regionPower);

			if (detailSize > 0) {
				float inverseDetailSize = 1.0f / detailSize;
				plateauRegionPower *= GetPerlinNormal(ref noise, x + 157400 + offsetX, y + 54254 + offsetY, 0, inverseDetailSize);
			}

			float plateauStep = plateauStepMin + (plateauStepMax - plateauStepMin) * GetPerlinNormal(ref noise, x + 1000 + offsetX, y + 1000 + offsetY, 0, inversePlateauScale);

			float newHeight = (int)((startingHeight + plateauRegionPower * maxPlateau) / plateauStep) * plateauStep - startingHeight;
			return Mathf.Max(0, newHeight);
		}

		static float CalculateHillPower(ref FastNoise_t noise, int x, int y, float regionSize, float regionPower, float hillSizeHorizontal, float hillHeight, float minSteepness, float maxSteepness, int offsetX, int offsetY) {
			float regionScaleInverse = 1.0f / regionSize;
			float hillScaleInverse = 1.0f / hillSizeHorizontal;

			float hillRegionPower = Mathf.Pow(GetPerlinNormal(ref noise, x + 1040 + offsetX, (y + 3234 + offsetY), 0, regionScaleInverse), regionPower);
			float hillRegionSteepness = GetPerlinNormal(ref noise, x + 100 + offsetX, y + 3243 + offsetY, 0, regionScaleInverse) * (maxSteepness - minSteepness) + minSteepness;
			float height = GetPerlinNormal(ref noise, x + 10 + offsetX, y + 1070 + offsetY, 0, hillScaleInverse) * hillHeight;
			float hill = (1.0f / (1.0f + Mathf.Exp(-hillRegionSteepness * (GetPerlinNormal(ref noise, x + 180 + offsetX, y + 180 + offsetY, 0, hillScaleInverse) - 0.5f)))) * height * hillRegionPower;
			return hill;
		}


		static float GetWhiteNoise(ref FastNoise_t noise, int x, int y, int z) {
			var v = noise.GetWhiteNoise(x, z, y);
			return (v + 1) / 2;
		}

		static float GetPerlinNormal(ref FastNoise_t noise, int x, int y, int z, float scale) {
			noise.SetFrequency(scale);
			var v = noise.GetPerlin(x, z, y);
			return (v + 1) / 2;
		}
		static float GetPerlinValue(ref FastNoise_t noise, int x, int y, int z, float scale) {
			noise.SetFrequency(scale);
			var v = noise.GetPerlin(x, z, y);
			return v;
		}
		static float GetPerlinNormal(ref FastNoise_t noise, int x, int y, float scale) {
			if (Port.Utils.positive_modulo(Mathf.FloorToInt((float)x / noiseFloatPregenSize), 2) == 0)
				x = Port.Utils.positive_modulo(x, noiseFloatPregenSize);
			else
				x = noiseFloatPregenSize - Port.Utils.positive_modulo(x, noiseFloatPregenSize) - 1;
			if (Port.Utils.positive_modulo(Mathf.FloorToInt((float)y / noiseFloatPregenSize), 2) == 0)
				y = Port.Utils.positive_modulo(y, noiseFloatPregenSize);
			else
				y = noiseFloatPregenSize - Port.Utils.positive_modulo(y, noiseFloatPregenSize) - 1;
			noise.SetFrequency(scale);
			float v = noise.GetPerlin(x, y);
			return (v + 1) / 2;
		}
		//static float GetPerlinValue(int x, int y, NoiseFloatScale scale)
		//{
		//	const float v = noiseFloatsPregen[scale][x + y * noiseFloatPregenSize];
		//	return v;
		//}

		static float GetDistImportance(ref FastNoise_t noise, int x, int y) {
			float scale = 0.0025f;
			return 2000 + 10000 * GetPerlinNormal(ref noise, x, y, 0, scale);
		}


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

		static float GetHumidity(ref FastNoise_t noise, int x, int y) {
			return
				Mathf.Pow(0.05f * GetPerlinNormal(ref noise, (x + 4342), (y + 87886), NoiseFloatScale._5) +
				0.15f * GetPerlinNormal(ref noise, (x + 42), (y + 8786), NoiseFloatScale._01) +
				0.8f * GetPerlinNormal(ref noise, (x + 3423), (y + 123142), NoiseFloatScale._001), 1.25f);
		}

		static float GetTemperature(ref FastNoise_t noise, int x, int y, int z) {
			float temperature = 0 - (int)(y / 3) +
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
							chunk.blocktypes[ofs] = EVoxelBlockType.ROCK;
						}
					}
				}
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
					chunk.blocktypes[ofs] = EVoxelBlockType.WOOD;
				}
			} else if (treeType == 1) {
				// Trunk
				int height = 2 + x % 3 + y % 3 + z % 3;
				for (int i = y; i < y + height && i < VOXEL_CHUNK_SIZE_Y; i++) {
					var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (i * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
					chunk.flags |= EChunkFlags.SOLID;
					chunk.blocktypes[ofs] = EVoxelBlockType.WOOD;
				}
				int radius = x % 2 + y % 2 + z % 2;
				if (radius > 0) {
					for (int i = -radius; i <= radius; i++) {
						for (int j = -radius; j <= radius; j++) {
							for (int k = -radius; k <= radius; k++) {
								int lx = x + i;
								int ly = y + j;
								int lz = z + k + height;
								if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
									var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
									chunk.flags |= EChunkFlags.SOLID;
									chunk.blocktypes[ofs] = EVoxelBlockType.LEAVES;
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
					chunk.blocktypes[ofs] = EVoxelBlockType.WOOD;
				}
				int radius = 1 + x % 2 + y % 2 + z % 2;
				if (radius > 0) {
					for (int k = -radius; k <= radius * 2; k++) {
						int r = radius - (k + radius) / 3;
						for (int i = -r; i <= r; i++) {
							for (int j = -r; j <= r; j++) {
								int lx = x + i;
								int ly = y + j;
								int lz = z + k + height;
								if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
									var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
									chunk.flags |= EChunkFlags.SOLID;
									chunk.blocktypes[ofs] = EVoxelBlockType.NEEDLES;
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
					chunk.blocktypes[ofs] = EVoxelBlockType.WOOD;
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
								int ly = z + i;
								for (int k = 1; k <= radius; k++) {
									int lx = x + branchDir.x * k;
									int lz = z + branchDir.y * k;
									if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
										var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
										chunk.flags |= EChunkFlags.SOLID;
										chunk.blocktypes[ofs] = k == 1 ? EVoxelBlockType.WOOD : EVoxelBlockType.NEEDLES;
									}
									if (k < radius) {
										lx = x + branchDir.x * k + branchDir.y;
										lz = z + branchDir.y * k + branchDir.x;
										if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
											var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
											chunk.flags |= EChunkFlags.SOLID;
											chunk.blocktypes[ofs] = EVoxelBlockType.NEEDLES;
										}
										lx = x + branchDir.x * k - branchDir.y;
										lz = z + branchDir.y * k - branchDir.x;
										if (lx >= 0 && lx < VOXEL_CHUNK_SIZE_XZ && ly >= 0 && ly < VOXEL_CHUNK_SIZE_Y && lz >= 0 && lz < VOXEL_CHUNK_SIZE_XZ) {
											var ofs = lx + (lz * VOXEL_CHUNK_SIZE_XZ) + (ly * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
											chunk.flags |= EChunkFlags.SOLID;
											chunk.blocktypes[ofs] = EVoxelBlockType.NEEDLES;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		static bool CalculateRoad(ref FastNoise_t noise, int x, int y, float lowerGroundHeight, int offsetX, int offsetY, ref float height) {
			if (height <= waterLevel)
				return false;

			float roadWidthMin = 0.002f;
			float roadWidthRange = 0.005f;
			float slopeWidthMin = 0.01f;
			float slopeWidthRange = 0.05f;
			float power =
				0.4f * GetPerlinNormal(ref noise, x + offsetX + 4235, y + offsetY + 324576, NoiseFloatScale._01) +
				0.3f * GetPerlinNormal(ref noise, x + offsetX + 254, y + offsetY + 6563, NoiseFloatScale._005) +
				0.3f * GetPerlinNormal(ref noise, x + offsetX + 224, y + offsetY + 6476563, NoiseFloatScale._001);
			float range = GetPerlinNormal(ref noise, x + offsetX + 7646745, y + offsetY + 24, NoiseFloatScale._01) * roadWidthRange + roadWidthMin;
			float falloff = GetPerlinNormal(ref noise, x + offsetX + 104, y + offsetY + 235, NoiseFloatScale._01) * slopeWidthRange + slopeWidthMin;
			if (power > 0.5f - range - falloff && power < 0.5f + range + falloff) {
				bool isRoad = false;
				if (power > 0.5f - range && power < 0.5f + range) {
					power = 1;
					isRoad = true;
				} else
					power = 1.0f - (Mathf.Abs(power - 0.5f) - range) / falloff;

				float influence = 0.5f;
				float newHeight = height * ((1.0f - (influence * power))) + lowerGroundHeight * (influence * power);

				if (newHeight <= waterLevel)
					return false;

				height = newHeight;

				return isRoad;
			}
			return false;
		}

		static float CalculateRiver(ref FastNoise_t noise, int x, int y, float lowerGroundHeight, int offsetX, int offsetY, ref float height) {
			if (height <= waterLevel)
				return 0;

			float powerScaleInverse = 0.005f;
			float roadWidthMin = 0.001f;
			float roadWidthRange = 0.1f;
			float humidity = GetHumidity(ref noise, x, y);
			float power =
				0.4f * GetPerlinNormal(ref noise, x + offsetX, y + offsetY, NoiseFloatScale._0005) +
				0.3f * GetPerlinNormal(ref noise, x + offsetX + 25254, y + offsetY + 65363, 0, powerScaleInverse * 0.5f) +
				0.3f * GetPerlinNormal(ref noise, x + offsetX + 2254, y + offsetY + 6563, 0, powerScaleInverse * 0.1f);
			float range = Mathf.Pow(0.2f * Mathf.Pow(humidity, 3) + 0.8f * (0.8f * GetPerlinNormal(ref noise, x + offsetX + 7646745, y + offsetY + 24, NoiseFloatScale._0005) + 0.2f * GetPerlinNormal(ref noise, x + offsetX + 7645, y + offsetY + 234, NoiseFloatScale._01)), 4) * roadWidthRange + roadWidthMin;

			float slopeWidthMin = 0.01f;
			float slopeWidthRange = 0.1f;
			float falloff = (height - lowerGroundHeight) * powerScaleInverse * GetPerlinNormal(ref noise, x + offsetX + 104, y + offsetY + 235, NoiseFloatScale._01) * slopeWidthRange + slopeWidthMin;
			if (power > 0.5f - range - falloff && power < 0.5f + range + falloff) {
				float depth = 0;
				if (power > 0.5f - range && power < 0.5f + range) {
					power = 1.0f - Mathf.Abs(power - 0.5f) / (range + falloff);

					depth = 1;
					float influence = 0.75f;
					float newHeight = Mathf.Min(height, height * ((1.0f - (influence * power))) + lowerGroundHeight * (influence * power)) - 1;
					height = newHeight;
				} else {
					power = 1.0f - Mathf.Abs(power - 0.5f) / (range + falloff);

					float influence = 0.75f;
					float newHeight = Mathf.Min(height, height * ((1.0f - (influence * power))) + lowerGroundHeight * (influence * power)) - 1;
					height = newHeight;
				}

				return depth;
			}

			return 0;
		}
	}
}

