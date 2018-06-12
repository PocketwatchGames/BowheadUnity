using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

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

    public enum EBlockType {
        BLOCK_TYPE_AIR = 0,
        BLOCK_TYPE_DIRT,
        BLOCK_TYPE_GRASS,
        BLOCK_TYPE_WATER,
        BLOCK_TYPE_SAND,
        BLOCK_TYPE_SNOW,
        BLOCK_TYPE_ROCK,
        BLOCK_TYPE_ICE,
        BLOCK_TYPE_WOOD,
        BLOCK_TYPE_LEAVES,
        BLOCK_TYPE_NEEDLES,
        BLOCK_TYPE_FLOWERS1,
        BLOCK_TYPE_FLOWERS2,
        BLOCK_TYPE_FLOWERS3,
        BLOCK_TYPE_FLOWERS4,
        NUM_BLOCK_TYPES,
        BLOCK_TYPE_MAX = 0x20 - 1 // top 3 bits used for flags
    }

    enum EChunkDataFlags {
        CHUNK_DF_NONE = 0,
        CHUNK_DF_AIR = 0x1,
        CHUNK_DF_SOLID = 0x2
    };

    class WorldVoxelChunkData_t {
        public EBlockType[] blocktypes = new EBlockType[WorldGen.WORLD_VOXELS_PER_CHUNK];
        public EChunkDataFlags flags;
    };


    public class WorldGen {

#if ONE_CHUNK
        const int WORLD_VOXEL_CHUNK_VIS_MAX_XY = 0;
        const int WORLD_VOXEL_CHUNK_VIS_MAX_Z = 0;
#else
        const int WORLD_VOXEL_CHUNK_VIS_MAX_XY = 16;
        const int WORLD_VOXEL_CHUNK_VIS_MAX_Z = 2;
#endif
        public const int WORLD_VOXEL_CHUNK_SIZE_XY = 32;
        public const int WORLD_VOXEL_CHUNK_SIZE_Z = 128;
        public const int WORLD_VOXELS_PER_CHUNK = WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_Z;

        const byte BLOCK_FULL_VOXEL_FLAG = 0x80;
        const byte BLOCK_FLAGS_MASK = BLOCK_FULL_VOXEL_FLAG;
        const byte BLOCK_TYPE_MASK = unchecked((byte)(~BLOCK_FLAGS_MASK));

        const int noiseFloatPregenSize = 512;
        const float waterLevel = 64;

        static FastNoise noise;

        int worldToChunk(int w, int CHUNK_SIZE) {
            if (w < 0) {
                return ((w + 1) / CHUNK_SIZE) - 1;
            }
            return w / CHUNK_SIZE;
        }

        int worldToLocalVoxel(int w, int CHUNK_SIZE) {
            return w & (CHUNK_SIZE - 1);
        }

        int chunkToWorld(int c, int CHUNK_SIZE) {
            return c * CHUNK_SIZE;
        }

        Vector3Int worldToChunk(Vector3Int pos) {

            Vector3Int c = new Vector3Int(worldToChunk(pos.x, WORLD_VOXEL_CHUNK_SIZE_XY),
                worldToChunk(pos.y, WORLD_VOXEL_CHUNK_SIZE_XY),
                worldToChunk(pos.z, WORLD_VOXEL_CHUNK_SIZE_Z));
            return c;
        }

        Vector3Int worldToLocalVoxel(Vector3Int pos) {
            Vector3Int v = new Vector3Int(worldToLocalVoxel(pos.x, WORLD_VOXEL_CHUNK_SIZE_XY),
        worldToLocalVoxel(pos.y, WORLD_VOXEL_CHUNK_SIZE_XY),
        worldToLocalVoxel(pos.z, WORLD_VOXEL_CHUNK_SIZE_Z));
            return v;
        }

        Vector3 worldToVec3(Vector3Int pos) {
            var c = worldToChunk(pos);
            var v = worldToLocalVoxel(pos);
            return new Vector3(
                (float)((c.x * WORLD_VOXEL_CHUNK_SIZE_XY) + v.x),
                (float)((c.y * WORLD_VOXEL_CHUNK_SIZE_XY) + v.y),
                (float)((c.z * WORLD_VOXEL_CHUNK_SIZE_Z) + v.z)
            );
        }

        Vector3Int chunkToWorld(Vector3Int pos) {
            return new Vector3Int(
                chunkToWorld(pos.x, WORLD_VOXEL_CHUNK_SIZE_XY),
                chunkToWorld(pos.y, WORLD_VOXEL_CHUNK_SIZE_XY),
                chunkToWorld(pos.z, WORLD_VOXEL_CHUNK_SIZE_Z)
            );
        }

        void G_GenVoxels(Vector3Int pos, WorldVoxelChunkData_t chunk /*, WorldVoxelChunkMinimapData_t minimap */) {

            chunk.flags = EChunkDataFlags.CHUNK_DF_NONE;
            bool solid = false;
            bool air = true;

            var wpos = chunkToWorld(pos);
            var v3 = worldToVec3(wpos);

            for (int x = 0; x < WORLD_VOXEL_CHUNK_SIZE_XY; ++x) {
                for (int y = 0; y < WORLD_VOXEL_CHUNK_SIZE_XY; ++y) {
                    var xpos = (int)v3.x + x;
                    var ypos = (int)v3.y + y;

                    bool isEmptyAtBottom = false;
                    var lowerGroundHeight = GetLowerGroundHeight(xpos, ypos);
                    var upperGroundHeight = GetUpperGroundHeight(xpos, ypos, lowerGroundHeight);
                    float waterDepth = CalculateRiver(xpos, ypos, lowerGroundHeight, 0, 0, ref upperGroundHeight);
                    bool isRoad = CalculateRoad(xpos, ypos, lowerGroundHeight, 0, 0, ref upperGroundHeight);

                    var minimapBlock = EBlockType.BLOCK_TYPE_AIR;

                    for (int z = 0; z < WORLD_VOXEL_CHUNK_SIZE_Z; ++z) {
                        var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (z * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                        var zpos = v3.z + z;

                        EBlockType bt;
                        bool isCave = false;
                        if (zpos > lowerGroundHeight && zpos <= upperGroundHeight) {
                            // Let's see about some caves er valleys!
                            float caveNoise = GetPerlinValue((int)xpos, (int)ypos, (int)zpos, 0.01f) * (0.015f * zpos) + 0.1f;
                            caveNoise += GetPerlinValue((int)xpos, (int)ypos, (int)zpos, 0.1f) * 0.06f + 0.1f;
                            caveNoise += GetPerlinValue((int)xpos, (int)ypos, (int)zpos, 0.2f) * 0.02f + 0.01f;
                            isCave = caveNoise > GetPerlinNormal((int)xpos, (int)ypos, (int)zpos, 0.01f) * 0.3f + 0.4f;
                        }

                        if (zpos <= upperGroundHeight && !isCave) {
                            bt = GetBlockType(xpos, ypos, (int)zpos, (int)upperGroundHeight, isRoad, false);
                        }
                        else {
                            if (zpos < waterLevel) {
                                float temperature = GetTemperature((int)xpos, (int)ypos, (int)zpos);
                                if (IsFrozen(temperature, GetHumidity((int)xpos, (int)ypos))) {
                                    bt = EBlockType.BLOCK_TYPE_ICE;
                                }
                                else {
                                    bt = EBlockType.BLOCK_TYPE_WATER;
                                }
                            }
                            else {
                                bt = EBlockType.BLOCK_TYPE_AIR;
                            }
                        }
                        if (bt == EBlockType.BLOCK_TYPE_AIR) {
                            if (z == 0) {
                                isEmptyAtBottom = true;
                            }
                            if (waterDepth > 0 && !isEmptyAtBottom) {
                                float temperature = GetTemperature((int)xpos, (int)ypos, (int)zpos);
                                if (IsFrozen(temperature, GetHumidity((int)xpos, (int)ypos))) {
                                    bt = EBlockType.BLOCK_TYPE_ICE;
                                }
                                else {
                                    bt = EBlockType.BLOCK_TYPE_WATER;
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

                        if ((EBlockType)((byte)bt & BLOCK_TYPE_MASK) != EBlockType.BLOCK_TYPE_AIR) {
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
                chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
            }
            if (air) {
                chunk.flags |= EChunkDataFlags.CHUNK_DF_AIR;
            }

            if (solid && air) {
                for (int x = 0; x < WORLD_VOXEL_CHUNK_SIZE_XY; ++x) {
                    for (int y = 0; y < WORLD_VOXEL_CHUNK_SIZE_XY; ++y) {
                        var xpos = (int)v3.x + x;
                        var ypos = (int)v3.y + y;
                        for (int z = WORLD_VOXEL_CHUNK_SIZE_Z - 1; z >= 0; --z) {
                            var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (z * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                            var zpos = (int)v3.z + z;
                            var bt = (EBlockType)((byte)chunk.blocktypes[ofs] & BLOCK_TYPE_MASK);
                            if (bt != EBlockType.BLOCK_TYPE_AIR) {
                                if (bt == EBlockType.BLOCK_TYPE_WATER || bt == EBlockType.BLOCK_TYPE_LEAVES || bt == EBlockType.BLOCK_TYPE_NEEDLES || bt == EBlockType.BLOCK_TYPE_WOOD)
                                    break;

                                float rock = GetRock(xpos, ypos, zpos);
                                float rockCutoff = 0.35f;
                                if (rock > rockCutoff) {
                                    float rockLimit = (1.0f - Mathf.Pow((rock - rockCutoff) / (1.0f - rockCutoff), 0.5f)) * 100 + 1;
                                    if (GetWhiteNoise(xpos, ypos, zpos) < 1.0f / rockLimit) {
                                        BuildRock(x, y, z, chunk);
                                    }
                                }

                                if (bt == EBlockType.BLOCK_TYPE_ICE || bt == EBlockType.BLOCK_TYPE_SAND)
                                    break;


                                float humidity = GetHumidity(xpos, ypos);
                                float forestPower = (1.0f - (GetPerlinNormal(xpos, ypos, NoiseFloatScale._01) * GetPerlinNormal(xpos + 64325, ypos + 6543, NoiseFloatScale._005))) * Mathf.Pow(humidity, 2) * (1.0f - Mathf.Pow(rock, 4));
                                float cutoff = 0.2f;
                                if (forestPower > cutoff) {
                                    float forestLimit = Mathf.Pow(1.0f - (forestPower - cutoff) / (1.0f - cutoff), 8) * 100 + 4;
                                    if (GetWhiteNoise(xpos, ypos, zpos) < 1.0f / forestLimit) {
                                        float temperature = GetTemperature(xpos, ypos, zpos);

                                        int treeType; // dead
                                        if (humidity + temperature / 100.0f + GetPerlinNormal(xpos, ypos, NoiseFloatScale._1) * 1.5f < 1.5f) {
                                            if (temperature + 30 * GetPerlinNormal(xpos + 422, ypos + 5357, NoiseFloatScale._1) > 60)
                                                treeType = 3;
                                            else
                                                treeType = 2; // pine
                                        }
                                        else if (IsFrozen(temperature, humidity))
                                            treeType = 0;
                                        else
                                            treeType = 1;
                                        BuildTree(x, y, z, treeType, chunk);
                                        break;
                                    }
                                }

                                if (bt == EBlockType.BLOCK_TYPE_GRASS || bt == EBlockType.BLOCK_TYPE_DIRT) {
                                    if (z < WORLD_VOXEL_CHUNK_SIZE_Z - 1) {
                                        float flowerPower1 = 0.3f * GetPerlinNormal(xpos, ypos, NoiseFloatScale._5)
                                            + 0.2f * GetPerlinNormal(xpos + 67435, ypos + 653, NoiseFloatScale._01)
                                            + 0.5f * GetPerlinNormal(xpos + 6435, ypos + 65453, NoiseFloatScale._005);
                                        float flowerPower2 = 0.4f * GetPerlinNormal(xpos + 256, ypos + 54764, NoiseFloatScale._5)
                                            + 0.3f * GetPerlinNormal(xpos + 6746435, ypos + 63, NoiseFloatScale._01)
                                            + 0.3f * GetPerlinNormal(xpos + 649835, ypos + 6543, NoiseFloatScale._005);
                                        float flowerPower3 = 0.2f * GetPerlinNormal(xpos + 7657376, ypos + 5421, NoiseFloatScale._5)
                                            + 0.3f * GetPerlinNormal(xpos + 67435, ypos + 658963, NoiseFloatScale._01)
                                            + 0.5f * GetPerlinNormal(xpos + 64935, ypos + 695453, NoiseFloatScale._005);
                                        float flowerPower4 = 0.3f * GetPerlinNormal(xpos + 15, ypos + 6532, NoiseFloatScale._5)
                                            + 0.1f * GetPerlinNormal(xpos + 6735, ypos + 63, NoiseFloatScale._01)
                                            + 0.6f * GetPerlinNormal(xpos + 645, ypos + 6553, NoiseFloatScale._005);
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
                                            var ofs2 = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + ((z + 1) * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                            chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                            chunk.blocktypes[ofs2] = (EBlockType)(EBlockType.BLOCK_TYPE_FLOWERS1 + flowerType);
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

        }

        static EBlockType GetBlockType(int x, int y, int z, int upperGroundHeight, bool isRoad, bool isRiver) {

            float humidity = GetHumidity(x, y);
            if (z == upperGroundHeight + 1) {
                float temperature = GetTemperature(x, y, z);
                if (IsFrozen(temperature, humidity)) {
                    if (IsSnow(x, y, z))
                        return EBlockType.BLOCK_TYPE_SNOW;
                    else
                        return EBlockType.BLOCK_TYPE_ICE;
                }
            }

            if (z == upperGroundHeight && z < waterLevel + 10 * (GetPerlinNormal(x, y, z, 0.2f) * GetPerlinNormal(x + 452, y + 6432, z + 784, 0.1f))) {
                return EBlockType.BLOCK_TYPE_SAND;
            }


            float rock = GetRock(x, y, z);
            if (z < upperGroundHeight) {
                if (rock > 0.5f) {
                    return EBlockType.BLOCK_TYPE_ROCK;
                }
                return EBlockType.BLOCK_TYPE_DIRT;
            }
            if (isRoad) {
                return EBlockType.BLOCK_TYPE_DIRT;
            }
            else if (humidity < 0.25f) {
                return EBlockType.BLOCK_TYPE_SAND;
            }
            else if ((0.95f * GetPerlinNormal(x, y, z, 0.01f) + 0.05f * GetPerlinNormal(x + 5432, y + 12, z + 874423, 0.1f)) * humidity * Mathf.Pow(rock, 0.25f) < 0.1f) {
                if (rock > 0.5f)
                    return EBlockType.BLOCK_TYPE_ROCK;
                else
                    return EBlockType.BLOCK_TYPE_DIRT;
            }
            else {
                return EBlockType.BLOCK_TYPE_GRASS;
            }

            //	return EBlockType.BLOCK_TYPE_DIRT;

        }


        static float GetLowerGroundHeight(int x, int y) {
            int maxGroundHeight = 128;

            float distToOriginSquared = 1.0f - Mathf.Sqrt((float)(Mathf.Pow(x, 2) + Mathf.Pow(y, 2))) / GetDistImportance(x, y);
            distToOriginSquared = Mathf.Clamp(distToOriginSquared, 0f, 1f);

            float lowerGroundHeight = 0;
            lowerGroundHeight += GetPerlinNormal(x, y, NoiseFloatScale._001) * 0.55f * distToOriginSquared;
            lowerGroundHeight += GetPerlinNormal(x, y, NoiseFloatScale._005) * 0.35f * distToOriginSquared;
            lowerGroundHeight += GetPerlinNormal(x, y, 0, 0.02f) * 0.05f;
            lowerGroundHeight += GetPerlinNormal(x, y, NoiseFloatScale._1) * 0.05f;

            lowerGroundHeight *= maxGroundHeight;


            return lowerGroundHeight + 1;


        }

        static float GetUpperGroundHeight(int x, int y, float lowerGroundHeight) {
            float mountainHeight = lowerGroundHeight;

            mountainHeight += CalculatePlateauPower(x, y, mountainHeight, 6000, 0, 1, 1000, 128, 64, 1, 78456, 14);
            mountainHeight += CalculateHillPower(x, y, 6000, 2, 500, 256, 1, 5, 0, 0);
            mountainHeight -= 128 * GetPerlinNormal(x + 1000, y + 4395, NoiseFloatScale._001) * GetPerlinNormal(x + 18000, y + 43095, NoiseFloatScale._0005);
            mountainHeight += CalculatePlateauPower(x, y, mountainHeight, 6000, 100, 3, 1000, 128, 64, 1, 7846, 1464);
            mountainHeight -= 64 * GetPerlinNormal(x + 100, y + 435, NoiseFloatScale._001) * GetPerlinNormal(x + 1000, y + 4095, NoiseFloatScale._0005);
            mountainHeight += CalculatePlateauPower(x, y, mountainHeight, 2000, 100, 2, 1000, 32, 16, 8, 736, 3242);
            mountainHeight -= 16 * GetPerlinNormal(x + 100, y + 435, NoiseFloatScale._005) * GetPerlinNormal(x + 1070, y + 43905, 0, 0.0025f);
            mountainHeight += CalculateHillPower(x, y, 1000, 3, 100, 64, 1, 10, 2554, 7648);
            mountainHeight += CalculatePlateauPower(x, y, mountainHeight, 2000, 100, 3, 1000, 16, 8, 1, 7336, 32842);
            mountainHeight -= 4 * GetPerlinNormal(x + 10670, y + 4385, 0, 0.02f) * GetPerlinNormal(x + 1070, y + 485, NoiseFloatScale._01);
            mountainHeight -= 3 * (1.0f - Mathf.Pow(2.0f * Mathf.Min(0.5f, GetRock(x, y, (int)mountainHeight)), 3));

            //float hillHeight = lowerGroundHeight;
            //hillHeight += 32 * GetPerlinNormal(x + 100, y, NoiseFloatScale._01);
            //hillHeight += 16 * GetPerlinNormal(x + 100, y, 0, 0.02f);
            //hillHeight += 8 * GetPerlinNormal(x + 100, y, NoiseFloatScale._1);

            //float hillInfluence = Mathf.Pow(GetPerlinNormal(x + 104350, y, NoiseFloatScale._001), 3);
            //float curHeight = hillHeight * hillInfluence + (1.f - hillInfluence) * mountainHeight;
            float curHeight = mountainHeight;

            float distToOriginSquared = 1.0f - Mathf.Sqrt((float)(Mathf.Pow(x, 2) + Mathf.Pow(y, 2))) / GetDistImportance(x, y);
            distToOriginSquared = Mathf.Clamp(distToOriginSquared, 0f, 1f);
            curHeight *= distToOriginSquared;


            return curHeight;

        }

        static float CalculatePlateauPower(int x, int y, float startingHeight, float plateauRegionSize, float detailSize, float regionPower, float plateauHorizontalScale, float maxPlateau, int plateauStepMax, int plateauStepMin, int offsetX, int offsetY) {
            float inverseRegionSize = 1.0f / plateauRegionSize;
            float inversePlateauScale = 1.0f / plateauHorizontalScale;
            float plateauRegionPower = Mathf.Pow(GetPerlinNormal(x + 100 + offsetX, y + offsetY, 0, inverseRegionSize), regionPower);

            if (detailSize > 0) {
                float inverseDetailSize = 1.0f / detailSize;
                plateauRegionPower *= GetPerlinNormal(x + 157400 + offsetX, y + 54254 + offsetY, 0, inverseDetailSize);
            }

            float plateauStep = plateauStepMin + (plateauStepMax - plateauStepMin) * GetPerlinNormal(x + 1000 + offsetX, y + 1000 + offsetY, 0, inversePlateauScale);

            float newHeight = (int)((startingHeight + plateauRegionPower * maxPlateau) / plateauStep) * plateauStep - startingHeight;
            return Mathf.Max(0, newHeight);
        }

        static float CalculateHillPower(int x, int y, float regionSize, float regionPower, float hillSizeHorizontal, float hillHeight, float minSteepness, float maxSteepness, int offsetX, int offsetY) {
            float regionScaleInverse = 1.0f / regionSize;
            float hillScaleInverse = 1.0f / hillSizeHorizontal;

            float hillRegionPower = Mathf.Pow(GetPerlinNormal(x + 1040 + offsetX, (y + 3234 + offsetY), 0, regionScaleInverse), regionPower);
            float hillRegionSteepness = GetPerlinNormal(x + 100 + offsetX, y + 3243 + offsetY, 0, regionScaleInverse) * (maxSteepness - minSteepness) + minSteepness;
            float height = GetPerlinNormal(x + 10 + offsetX, y + 1070 + offsetY, 0, hillScaleInverse) * hillHeight;
            float hill = (1.0f / (1.0f + Mathf.Exp(-hillRegionSteepness * (GetPerlinNormal(x + 180 + offsetX, y + 180 + offsetY, 0, hillScaleInverse) - 0.5f)))) * height * hillRegionPower;
            return hill;
        }


        static float GetWhiteNoise(int x, int y, int z) {
            var v = noise.GetWhiteNoise(x, y, z);
            return (v + 1) / 2;
        }

        static float GetPerlinNormal(int x, int y, int z, float scale) {
            noise.SetFrequency(scale);
            var v = noise.GetPerlin(x, y, z);
            return (v + 1) / 2;
        }
        static float GetPerlinValue(int x, int y, int z, float scale) {
            noise.SetFrequency(scale);
            var v = noise.GetPerlin(x, y, z);
            return v;
        }
        static float GetPerlinNormal(int x, int y, float scale) {
            if (Utils.positive_modulo(Mathf.FloorToInt((float)x / noiseFloatPregenSize), 2) == 0)
                x = Utils.positive_modulo(x, noiseFloatPregenSize);
            else
                x = noiseFloatPregenSize - Utils.positive_modulo(x, noiseFloatPregenSize) - 1;
            if (Utils.positive_modulo(Mathf.FloorToInt((float)y / noiseFloatPregenSize), 2) == 0)
                y = Utils.positive_modulo(y, noiseFloatPregenSize);
            else
                y = noiseFloatPregenSize - Utils.positive_modulo(y, noiseFloatPregenSize) - 1;
            noise.SetFrequency(scale);
            float v = noise.GetPerlin(x, y);
            return (v + 1) / 2;
        }
        //static float GetPerlinValue(int x, int y, NoiseFloatScale scale)
        //{
        //	const float v = noiseFloatsPregen[scale][x + y * noiseFloatPregenSize];
        //	return v;
        //}



        static float GetDistImportance(int x, int y) {
            float scale = 0.0025f;
            return 2000 + 10000 * GetPerlinNormal(x, y, 0, scale);
        }


        static bool IsFrozen(float temperature, float humidity) {
            return temperature < 32 && humidity > 0.25f;
        }

        static bool IsSnow(int x, int y, int z) {
            float snow =
                0.2f * GetPerlinValue(x, y, z, 0.5f) +
                0.8f * GetPerlinValue(x, y, z, 0.01f);
            return (snow > -0.5f);
        }

        static float GetRock(int x, int y, int z) {
            return 0.2f * GetPerlinNormal(x, y, z, 0.1f) +
                0.8f * GetPerlinNormal(x, y, z, 0.001f);
        }

        static float GetHumidity(int x, int y) {
            return
                Mathf.Pow(0.05f * GetPerlinNormal((x + 4342), (y + 87886), NoiseFloatScale._5) +
                0.15f * GetPerlinNormal((x + 42), (y + 8786), NoiseFloatScale._01) +
                0.8f * GetPerlinNormal((x + 3423), (y + 123142), NoiseFloatScale._001), 1.25f);
        }

        static float GetTemperature(int x, int y, int z) {
            float temperature = 0 - (int)(z / 3) +
                5 * GetPerlinNormal((x + 432), (y + 8786), NoiseFloatScale._5) +
                20 * GetPerlinNormal((x + 1540), (y + 76846), NoiseFloatScale._01) +
                110 * GetPerlinNormal((x + 1454), (y + 766), NoiseFloatScale._001);
            return temperature;
        }

        static void BuildRock(int x, int y, int z, WorldVoxelChunkData_t chunk) {
            int xMinus = x - y % 3;
            int xPlus = x + (x + y + z) % 3;
            int yMinus = y - x % 3;
            int yPlus = y + (y + z) % 3;
            int zMinus = z - (z + x) % 3;
            int zPlus = z + (x + y) % 3;
            for (int i = xMinus; i < xPlus; i++) {
                for (int j = yMinus; j < yPlus; j++) {
                    for (int k = zMinus; k < zPlus; k++) {
                        if (i >= 0 && i < WORLD_VOXEL_CHUNK_SIZE_XY && j >= 0 && j < WORLD_VOXEL_CHUNK_SIZE_XY && k >= 0 && k < WORLD_VOXEL_CHUNK_SIZE_Z) {
                            var ofs = i + (j * WORLD_VOXEL_CHUNK_SIZE_XY) + (k * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                            chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                            chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_ROCK;
                        }
                    }
                }
            }
        }

        static void BuildTree(int x, int y, int z, int treeType, WorldVoxelChunkData_t chunk) {


            // Foliage
            if (treeType == 0) {
                // Trunk
                int height = 1 + x % 4 + y % 4 + z % 4;
                for (int i = z; i < z + height && i < WORLD_VOXEL_CHUNK_SIZE_Z; i++) {
                    var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (i * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_WOOD;
                }
            }
            else if (treeType == 1) {
                // Trunk
                int height = 2 + x % 3 + y % 3 + z % 3;
                for (int i = z; i < z + height && i < WORLD_VOXEL_CHUNK_SIZE_Z; i++) {
                    var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (i * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_WOOD;
                }
                int radius = x % 2 + y % 2 + z % 2;
                if (radius > 0) {
                    for (int i = -radius; i <= radius; i++) {
                        for (int j = -radius; j <= radius; j++) {
                            for (int k = -radius; k <= radius; k++) {
                                int lx = x + i;
                                int ly = y + j;
                                int lz = z + k + height;
                                if (lx >= 0 && lx < WORLD_VOXEL_CHUNK_SIZE_XY && ly >= 0 && ly < WORLD_VOXEL_CHUNK_SIZE_XY && lz >= 0 && lz < WORLD_VOXEL_CHUNK_SIZE_Z) {
                                    var ofs = lx + (ly * WORLD_VOXEL_CHUNK_SIZE_XY) + (lz * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_LEAVES;
                                }
                            }
                        }
                    }
                }
            }
            else if (treeType == 2) {
                int height = 3 + x % 2 + y % 2 + z % 2;
                for (int i = z; i < z + height && i < WORLD_VOXEL_CHUNK_SIZE_Z; i++) {
                    var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (i * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_WOOD;
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
                                if (lx >= 0 && lx < WORLD_VOXEL_CHUNK_SIZE_XY && ly >= 0 && ly < WORLD_VOXEL_CHUNK_SIZE_XY && lz >= 0 && lz < WORLD_VOXEL_CHUNK_SIZE_Z) {
                                    var ofs = lx + (ly * WORLD_VOXEL_CHUNK_SIZE_XY) + (lz * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_NEEDLES;
                                }

                            }
                        }
                    }
                }
            }
            else if (treeType == 3) {
                int height = 4 + x % 10 + y % 10 + z % 10;
                for (int i = z; i < z + height && i < WORLD_VOXEL_CHUNK_SIZE_Z; i++) {
                    var ofs = x + (y * WORLD_VOXEL_CHUNK_SIZE_XY) + (i * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                    chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                    chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_WOOD;
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
                                int lz = z + i;
                                for (int k = 1; k <= radius; k++) {
                                    int lx = x + branchDir.x * k;
                                    int ly = y + branchDir.y * k;
                                    if (lx >= 0 && lx < WORLD_VOXEL_CHUNK_SIZE_XY && ly >= 0 && ly < WORLD_VOXEL_CHUNK_SIZE_XY && lz >= 0 && lz < WORLD_VOXEL_CHUNK_SIZE_Z) {
                                        var ofs = lx + (ly * WORLD_VOXEL_CHUNK_SIZE_XY) + (lz * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                        chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                        chunk.blocktypes[ofs] = k == 1 ? EBlockType.BLOCK_TYPE_WOOD : EBlockType.BLOCK_TYPE_NEEDLES;
                                    }
                                    if (k < radius) {
                                        lx = x + branchDir.x * k + branchDir.y;
                                        ly = y + branchDir.y * k + branchDir.x;
                                        if (lx >= 0 && lx < WORLD_VOXEL_CHUNK_SIZE_XY && ly >= 0 && ly < WORLD_VOXEL_CHUNK_SIZE_XY && lz >= 0 && lz < WORLD_VOXEL_CHUNK_SIZE_Z) {
                                            var ofs = lx + (ly * WORLD_VOXEL_CHUNK_SIZE_XY) + (lz * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                            chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                            chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_NEEDLES;
                                        }
                                        lx = x + branchDir.x * k - branchDir.y;
                                        ly = y + branchDir.y * k - branchDir.x;
                                        if (lx >= 0 && lx < WORLD_VOXEL_CHUNK_SIZE_XY && ly >= 0 && ly < WORLD_VOXEL_CHUNK_SIZE_XY && lz >= 0 && lz < WORLD_VOXEL_CHUNK_SIZE_Z) {
                                            var ofs = lx + (ly * WORLD_VOXEL_CHUNK_SIZE_XY) + (lz * WORLD_VOXEL_CHUNK_SIZE_XY * WORLD_VOXEL_CHUNK_SIZE_XY);
                                            chunk.flags |= EChunkDataFlags.CHUNK_DF_SOLID;
                                            chunk.blocktypes[ofs] = EBlockType.BLOCK_TYPE_NEEDLES;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        static bool CalculateRoad(int x, int y, float lowerGroundHeight, int offsetX, int offsetY, ref float height) {
            if (height <= waterLevel)
                return false;

            float roadWidthMin = 0.002f;
            float roadWidthRange = 0.005f;
            float slopeWidthMin = 0.01f;
            float slopeWidthRange = 0.05f;
            float power =
                0.4f * GetPerlinNormal(x + offsetX + 4235, y + offsetY + 324576, NoiseFloatScale._01) +
                0.3f * GetPerlinNormal(x + offsetX + 254, y + offsetY + 6563, NoiseFloatScale._005) +
                0.3f * GetPerlinNormal(x + offsetX + 224, y + offsetY + 6476563, NoiseFloatScale._001);
            float range = GetPerlinNormal(x + offsetX + 7646745, y + offsetY + 24, NoiseFloatScale._01) * roadWidthRange + roadWidthMin;
            float falloff = GetPerlinNormal(x + offsetX + 104, y + offsetY + 235, NoiseFloatScale._01) * slopeWidthRange + slopeWidthMin;
            if (power > 0.5f - range - falloff && power < 0.5f + range + falloff) {
                bool isRoad = false;
                if (power > 0.5f - range && power < 0.5f + range) {
                    power = 1;
                    isRoad = true;
                }
                else
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

        static float CalculateRiver(int x, int y, float lowerGroundHeight, int offsetX, int offsetY, ref float height) {
            if (height <= waterLevel)
                return 0;

            float powerScaleInverse = 0.005f;
            float roadWidthMin = 0.001f;
            float roadWidthRange = 0.1f;
            float humidity = GetHumidity(x, y);
            float power =
                0.4f * GetPerlinNormal(x + offsetX, y + offsetY, NoiseFloatScale._0005) +
                0.3f * GetPerlinNormal(x + offsetX + 25254, y + offsetY + 65363, 0, powerScaleInverse * 0.5f) +
                0.3f * GetPerlinNormal(x + offsetX + 2254, y + offsetY + 6563, 0, powerScaleInverse * 0.1f);
            float range = Mathf.Pow(0.2f * Mathf.Pow(humidity, 3) + 0.8f * (0.8f * GetPerlinNormal(x + offsetX + 7646745, y + offsetY + 24, NoiseFloatScale._0005) + 0.2f * GetPerlinNormal(x + offsetX + 7645, y + offsetY + 234, NoiseFloatScale._01)), 4) * roadWidthRange + roadWidthMin;

            float slopeWidthMin = 0.01f;
            float slopeWidthRange = 0.1f;
            float falloff = (height - lowerGroundHeight) * powerScaleInverse * GetPerlinNormal(x + offsetX + 104, y + offsetY + 235, NoiseFloatScale._01) * slopeWidthRange + slopeWidthMin;
            if (power > 0.5f - range - falloff && power < 0.5f + range + falloff) {
                float depth = 0;
                if (power > 0.5f - range && power < 0.5f + range) {
                    power = 1.0f - Mathf.Abs(power - 0.5f) / (range + falloff);

                    depth = 1;
                    float influence = 0.75f;
                    float newHeight = Mathf.Min(height, height * ((1.0f - (influence * power))) + lowerGroundHeight * (influence * power)) - 1;
                    height = newHeight;
                }
                else {
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