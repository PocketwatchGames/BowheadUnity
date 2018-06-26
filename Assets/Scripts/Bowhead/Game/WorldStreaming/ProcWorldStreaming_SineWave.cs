// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using static World;

namespace Bowhead {
	public partial class WorldStreaming {

		struct ProcWorldStreaming_SineWave_Job_t : IJob {
			PinnedChunkData_t chunk;
			WorldChunkPos_t cpos;
			bool checkSolid;

			public void Execute() {
				chunk = GenerateVoxelsSinWave(cpos, chunk);

				if (checkSolid && IsSolidXZPlane(chunk)) {
					chunk.flags |= EChunkFlags.SOLID_XZ_PLANE;
				}

				unsafe {
					chunk.pinnedFlags[0] = chunk.flags;
				}
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
								chunk.voxeldata[ofs] = EVoxelBlockType.DIRT/*|EVoxelBlockType.FULL_VOXEL_FLAG*/;
								solid = true;
							} else {
								air = true;
								chunk.voxeldata[ofs] = EVoxelBlockType.AIR;
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

			public class Streaming : IWorldStreaming {
				public JobHandle ScheduleChunkGenerationJob(WorldChunkPos_t cpos, PinnedChunkData_t chunk, bool checkSolid) {
					return new ProcWorldStreaming_SineWave_Job_t() {
						cpos = cpos,
						chunk = chunk,
						checkSolid = checkSolid
					}.Schedule();
				}

				public void Dispose() { }
			};
		}

		
	}

}