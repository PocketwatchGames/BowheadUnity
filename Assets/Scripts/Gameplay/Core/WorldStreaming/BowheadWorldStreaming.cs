// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Unity.Jobs;
using static World;

namespace Bowhead {
	public static partial class WorldStreaming {
		public enum EGenerator {
			SINEWAVE,
			PROC_V1,
			PROC_V2,
		};

		static WorldStreaming() {
			FastNoise_t.New();
		}

		public interface IWorldStreaming : System.IDisposable {
			JobHandle ScheduleChunkGenerationJob(WorldChunkPos_t cpos, PinnedChunkData_t chunk, bool checkSolid);
			Streaming.IAsyncChunkIO AsyncReadChunkData(Streaming.IChunkIO chunk);
			void WriteChunkData(Streaming.IChunkIO chunk);
		};

		public static IWorldStreaming NewProceduralWorldStreaming(ulong seed, EGenerator generator) {
			switch (generator) {
				case EGenerator.PROC_V1:
					return new ProcWorldStreaming_V1_Job_t.Streaming();
				case EGenerator.PROC_V2:
					return new ProcWorldStreaming_V2_Job_t.Streaming();
				case EGenerator.SINEWAVE:
					return new ProcWorldStreaming_SineWave_Job_t.Streaming();
				default: throw new System.NotImplementedException();
			}
		}

		static unsafe bool IsSolidXZPlane(PinnedChunkData_t chunk) {

			byte* solidFlags = stackalloc byte[VOXELS_PER_CHUNK_XZ];
			for (int i = 0; i < VOXELS_PER_CHUNK_XZ; ++i) {
				solidFlags[i] = 0;
			}

			int numSolid = 0;
			int ofs = 0;

			for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
				for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
					int ofsxz = z*VOXEL_CHUNK_SIZE_XZ;

					for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
						if (solidFlags[ofsxz] == 0) {
							var block = chunk.voxeldata[ofs].type;
							if (block != EVoxelBlockType.Air) {
								solidFlags[ofsxz] = 1;
								++numSolid;
								if (numSolid == VOXELS_PER_CHUNK_XZ) {
									return true;
								}
							}
						}

						++ofsxz;
						++ofs;
					}
				}
			}

			return false;
		}
	};
}

