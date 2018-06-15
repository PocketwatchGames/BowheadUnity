using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class World {
	partial class ChunkMeshGen {
		static PinnedChunkData_t GenerateVoxels(WorldChunkPos_t pos, PinnedChunkData_t chunk) {
			bool solid = false;
			bool air = false;

			chunk.flags = EChunkFlags.NONE;

			for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
				for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
					for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
						var ofs = x + (z * VOXEL_CHUNK_SIZE_XZ) + (y * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);

						if ((pos.cy + y) < (VOXEL_CHUNK_SIZE_Y/2)) {
							chunk.blocktypes[ofs] = EVoxelBlockType.DIRT;
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
	}
}
