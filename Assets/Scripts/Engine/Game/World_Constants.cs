// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;

// A world space chunk position.
public struct WorldChunkPos_t {
	public int cx, cy, cz;

	public WorldChunkPos_t(int x, int y, int z) {
		cx = x;
		cy = y;
		cz = z;
	}

	public static bool operator ==(WorldChunkPos_t a, WorldChunkPos_t b) {
		return (a.cx == b.cx) && (a.cy == b.cy) && (a.cz == b.cz);
	}

	public static bool operator !=(WorldChunkPos_t a, WorldChunkPos_t b) {
		return !(a == b);
	}

	public static WorldChunkPos_t operator -(WorldChunkPos_t a) {
		return new WorldChunkPos_t(-a.cx, -a.cy, -a.cz);
	}

	public static WorldChunkPos_t operator +(WorldChunkPos_t a, WorldChunkPos_t b) {
		return new WorldChunkPos_t(a.cx + b.cx, a.cy + b.cy, a.cz + b.cz);
	}

	public static WorldChunkPos_t operator -(WorldChunkPos_t a, WorldChunkPos_t b) {
		return new WorldChunkPos_t(a.cx - b.cx, a.cy - b.cy, a.cz - b.cz);
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

	public bool Equals(WorldChunkPos_t pos) {
		return pos == this;
	}

	public override string ToString() {
		return "cx = " + cx + ", cy = " + cy + ", cz = " + cz;
	}
};

// A world-space voxel position.
public struct WorldVoxelPos_t {
	public int vx, vy, vz;

	public WorldVoxelPos_t(int x, int y, int z) {
		vx = x;
		vy = y;
		vz = z;
	}

	public static bool operator ==(WorldVoxelPos_t a, WorldVoxelPos_t b) {
		return (a.vx == b.vx) && (a.vy == b.vy) && (a.vz == b.vz);
	}

	public static bool operator !=(WorldVoxelPos_t a, WorldVoxelPos_t b) {
		return !(a == b);
	}

	public static WorldVoxelPos_t operator -(WorldVoxelPos_t a) {
		return new WorldVoxelPos_t(-a.vx, -a.vy, -a.vz);
	}

	public static WorldVoxelPos_t operator + (WorldVoxelPos_t a, WorldVoxelPos_t b) {
		return new WorldVoxelPos_t(a.vx + b.vx, a.vy + b.vy, a.vz + b.vz);
	}

	public static WorldVoxelPos_t operator - (WorldVoxelPos_t a, WorldVoxelPos_t b) {
		return new WorldVoxelPos_t(a.vx - b.vx, a.vy - b.vy, a.vz - b.vz);
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

	public override string ToString() {
		return "vx = " + vx + ", vy = " + vy + ", vz = " + vz;
	}
};

// This is the local voxel position inside a chunk, valid range is 0 to VOXEL_CHUNK_SIZE - 1
public struct LocalVoxelPos_t {
	public int vx, vy, vz;

	public LocalVoxelPos_t(int x, int y, int z) {
		vx = x;
		vy = y;
		vz = z;
	}

	public static bool operator ==(LocalVoxelPos_t a, LocalVoxelPos_t b) {
		return (a.vx == b.vx) && (a.vy == b.vy) && (a.vz == b.vz);
	}

	public static bool operator !=(LocalVoxelPos_t a, LocalVoxelPos_t b) {
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

	public override string ToString() {
		return "vx = " + vx + ", vy = " + vy + ", vz = " + vz;
	}
};

public enum EVoxelBlockType : byte {
	Air = 0,
	Dirt,
	Grass,
	Water,
	Sand,
	Snow,
	Rock,
	Ice,
	Wood,
	Leaves,
	Needles,
	Flowers1,
	Flowers2,
	Flowers3,
	Flowers4,
	SandRocky,
	NumBlockTypes // must be here because we require a constant for array initialization in TableStorage
};

public enum EVoxelBlockContents : int {
	None,
	Water,
	Solid
};

[Flags]
public enum EVoxelBlockFlags : byte {
	FullVoxel = 0x80,
	AllFlags = FullVoxel
};

public struct Voxel_t {
	public byte raw;

	public Voxel_t(byte raw) {
		this.raw = raw;
	}

	public Voxel_t(EVoxelBlockType type) {
		raw = (byte)type;
	}

	public Voxel_t(EVoxelBlockType type, EVoxelBlockFlags flags) {
		raw = (byte)((int)type | (int)flags);
	}

	public EVoxelBlockType type {
		get {
			return (EVoxelBlockType)(raw & (byte)(~EVoxelBlockFlags.AllFlags));
		}
		set {
			raw = (byte)((int)flags | (int)value);
		}
	}

	public EVoxelBlockFlags flags {
		get {
			return (EVoxelBlockFlags)(raw & (byte)EVoxelBlockFlags.AllFlags);
		}
		set {
			raw = (byte)((int)type | (int)value);
		}
	}

	public EVoxelBlockContents contents {
		get {
			return World.GetBlockContents(type);
		}
	}

	public static Voxel_t operator | (Voxel_t voxel, EVoxelBlockFlags flags) {
		voxel.flags |= flags;
		return voxel;
	}

	public static Voxel_t operator & (Voxel_t voxel, EVoxelBlockFlags flags) {
		voxel.flags &= flags;
		return voxel;
	}

	public static implicit operator Voxel_t (EVoxelBlockType type) {
		Voxel_t v = default(Voxel_t);
		v.type = type;
		return v;
	}
};

public enum EChunkLayers : int {
	Terrain,
	Water,
	Trees
};

public enum EDecorationType : int {
	MonsterSpawn,
	Merchant,
	Horse,
	Chest,
	MapReveal
};

public static class WorldConstantExtensions {
	public static int ToIndex(this EChunkLayers layer) {
		return (int)layer;
	}

	public static Voxel_t WithFlags(this EVoxelBlockType type, EVoxelBlockFlags flags = 0) {
		return new Voxel_t(type, flags);
	}
};

public partial class World {

	public struct Decoration_t {
		public const int MAX_DECORATIONS_PER_CHUNK = 32;
		public EDecorationType type;
		public Vector3 pos;
	};

	public static readonly int[] ChunkLayers = new int[] {
		Layers.Terrain,
		Layers.Water,
		Layers.Trees
	};

	public static readonly string[] ChunkLayerNames = new string[] {
		null,
		"Water",
		"Trees"
	};

	public static readonly int MAX_CHUNK_LAYERS = Enum.GetNames(typeof(EChunkLayers)).Length;
	public const int MAX_CHUNK_SUBMESHES = 16;
#if DEV_STREAMING
	public const int MAX_MATERIALS_PER_SUBMESH = 2;
#else
	public const int MAX_MATERIALS_PER_SUBMESH = 4;
#endif

	public const int VOXEL_CHUNK_VIS_MAX_XZ = 16;
	public const int VOXEL_CHUNK_VIS_MAX_Y_UP = 2;
	public const int VOXEL_CHUNK_VIS_MAX_Y_DOWN = 2;
	public const int VOXEL_CHUNK_VIS_MAX_Y = VOXEL_CHUNK_VIS_MAX_Y_UP + VOXEL_CHUNK_VIS_MAX_Y_DOWN;
	public const int VOXEL_CHUNK_SIZE_XZ = 16;
	public const int VOXEL_CHUNK_SIZE_Y = 64;
	public const int VOXELS_PER_CHUNK = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_Y;
	public const int VOXELS_PER_CHUNK_XZ = VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ;

	public const uint BLOCK_SMG_WATER = 0x1;
	public const uint BLOCK_SMG_DIRT_ROCK = 0x2;
	public const uint BLOCK_SMG_GRASS = 0x4;
	public const uint BLOCK_SMG_FLOWERS = 0x8;
	public const uint BLOCK_SMG_OTHER = 0x10;
	public const uint BLOCK_BLEND_COLORS = 0x80000000;

	public static int MaxVoxelChunkLine(int dim) {
		return (dim*2) + 1;
	}
	
	[Flags]
	public enum EChunkFlags {
		NONE = 0,
		AIR = 0x1,
		SOLID = 0x2,
		SOLID_XZ_PLANE = 0x4, // no vertical columns of air
		LAYER_DEFAULT = 0x8,
		LAYER_WATER = 0x10,
		LAYER_TREES = 0x20,
		ALL_LAYERS_FLAGS = LAYER_DEFAULT|LAYER_WATER|LAYER_TREES
	};

	public static int WorldToChunk(int w, int CHUNK_SIZE) {
		if (w < 0) {
			return ((w + 1) / CHUNK_SIZE) - 1;
		}
		return w / CHUNK_SIZE;
	}

	public static int WorldToLocalVoxel(int w, int CHUNK_SIZE) {
		if (w < 0) {
			return CHUNK_SIZE - 1 - (-(w + 1) & (CHUNK_SIZE - 1));
		}
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

	public static WorldVoxelPos_t Vec3ToWorld(Vector3 p) {
		return new WorldVoxelPos_t() {
			vx = Mathf.FloorToInt(p.x),
			vy = Mathf.FloorToInt(p.y),
			vz = Mathf.FloorToInt(p.z)
		};
	}

	public static WorldVoxelPos_t ChunkToWorld(WorldChunkPos_t p) {
		return new WorldVoxelPos_t {
			vx = ChunkToWorld(p.cx, VOXEL_CHUNK_SIZE_XZ),
			vy = ChunkToWorld(p.cy, VOXEL_CHUNK_SIZE_Y),
			vz = ChunkToWorld(p.cz, VOXEL_CHUNK_SIZE_XZ)
		};
	}

}
