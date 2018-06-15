using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class World {

	public const int VOXEL_CHUNK_VIS_MAX_XZ = 16;
	public const int VOXEL_CHUNK_VIS_MAX_Y = 2;
	public const int VOXEL_CHUNK_SIZE_XZ = 32;
	public const int VOXEL_CHUNK_SIZE_Y = 128;
	public const int VOXELS_PER_CHUNK = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_Y;

	public static int MaxVoxelChunkLine(int dim) {
		return (dim*2) + 1;
	}

	[Flags]
	public enum EVoxelBlockType : byte {
		AIR = 0,
		DIRT,
		GRASS,
		WATER,
		SAND,
		SNOW,
		ROCK,
		ICE,
		WOOD,
		LEAVES,
		NEEDLES,
		FLOWERS1,
		FLOWERS2,
		FLOWERS3,
		FLOWERS4,
		NUM_BLOCK_TYPES,
		MAX = 0x20-1,
		FULL_VOXEL_FLAG = 0x80,
		FLAGS_MASK = FULL_VOXEL_FLAG
	};

	[Flags]
	public enum EChunkFlags {
		NONE = 0,
		AIR = 0x1,
		SOLID = 0x2
	};

	public struct WorldChunkPos_t {
		public int cx, cy, cz;

		public WorldChunkPos_t(int x, int y, int z) {
			cx = x;
			cy = y;
			cz = z;
		}

		public static bool operator == (WorldChunkPos_t a, WorldChunkPos_t b) {
			return (a.cx == b.cx) && (a.cy == b.cy) && (a.cz == b.cz);
		}

		public static bool operator != (WorldChunkPos_t a, WorldChunkPos_t b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			return cx.GetHashCode() ^ cy.GetHashCode() ^ cz.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is WorldChunkPos_t) {
				return ((WorldChunkPos_t)obj) == this;
			}
			return false;
		}
	};

	public struct WorldVoxelPos_t {
		public int vx, vy, vz;

		public WorldVoxelPos_t(int x, int y, int z) {
			vx = x;
			vy = y;
			vz = z;
		}

		public static bool operator == (WorldVoxelPos_t a, WorldVoxelPos_t b) {
			return (a.vx == b.vx) && (a.vy == b.vy) && (a.vz == b.vz);
		}

		public static bool operator != (WorldVoxelPos_t a, WorldVoxelPos_t b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			return vx.GetHashCode() ^ vy.GetHashCode() ^ vz.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is WorldVoxelPos_t) {
				return ((WorldVoxelPos_t)obj) == this;
			}
			return false;
		}
	};

	public struct LocalVoxelPos_t {
		public int vx, vy, vz;

		public LocalVoxelPos_t(int x, int y, int z) {
			vx = x;
			vy = y;
			vz = z;
		}

		public static bool operator == (LocalVoxelPos_t a, LocalVoxelPos_t b) {
			return (a.vx == b.vx) && (a.vy == b.vy) && (a.vz == b.vz);
		}

		public static bool operator != (LocalVoxelPos_t a, LocalVoxelPos_t b) {
			return !(a == b);
		}

		public override int GetHashCode() {
			return vx.GetHashCode() ^ vy.GetHashCode() ^ vz.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is LocalVoxelPos_t) {
				return ((LocalVoxelPos_t)obj) == this;
			}
			return false;
		}
	};

	public static int WorldToChunk(int w, int CHUNK_SIZE) {
		if (w < 0) {
			return ((w + 1) / CHUNK_SIZE) - 1;
		}
		return w / CHUNK_SIZE;
	}

	public static int WorldToLocalVoxel(int w, int CHUNK_SIZE) {
		return w & (CHUNK_SIZE - 1);
	}

	public static int ChunkToWorld(int c, int CHUNK_SIZE) {
		return c * CHUNK_SIZE;
	}

	public static WorldChunkPos_t WorldToChunk(WorldVoxelPos_t p) {
		return new WorldChunkPos_t {
			cx = WorldToChunk(p.vx, VOXEL_CHUNK_SIZE_XZ),
			cy = WorldToChunk(p.vy, VOXEL_CHUNK_SIZE_Y),
			cz = WorldToChunk(p.vz, VOXEL_CHUNK_SIZE_XZ)
		};
	}

	public static LocalVoxelPos_t WorldToLocalVoxel(WorldVoxelPos_t p) {
		return new LocalVoxelPos_t {
			vx = WorldToLocalVoxel(p.vx, VOXEL_CHUNK_SIZE_XZ),
			vy = WorldToLocalVoxel(p.vy, VOXEL_CHUNK_SIZE_Y),
			vz = WorldToLocalVoxel(p.vz, VOXEL_CHUNK_SIZE_XZ)
		};
	}

	public static Vector3 WorldToVec3(WorldVoxelPos_t p) {
		var c = WorldToChunk(p);
		var v = WorldToLocalVoxel(p);
		return new Vector3(
			(float)((c.cx * VOXEL_CHUNK_SIZE_XZ) + v.vx),
			(float)((c.cy * VOXEL_CHUNK_SIZE_Y) + v.vy),
			(float)((c.cz * VOXEL_CHUNK_SIZE_XZ) + v.vz)
		);
	}

	public static WorldVoxelPos_t ChunkToWorld(WorldChunkPos_t p) {
		return new WorldVoxelPos_t {
			vx = ChunkToWorld(p.cx, VOXEL_CHUNK_SIZE_XZ),
			vy = ChunkToWorld(p.cy, VOXEL_CHUNK_SIZE_Y),
			vz = ChunkToWorld(p.cz, VOXEL_CHUNK_SIZE_XZ)
		};
	}

}
