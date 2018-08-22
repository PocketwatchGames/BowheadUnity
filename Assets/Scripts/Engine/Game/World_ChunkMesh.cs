// Copyright (c) 2018 Pocketwatch Games LLC.

//#define BOUNDS_CHECK
//#define NO_SMOOTHING

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static UnityEngine.Debug;

#if DEBUG_VOXEL_MESH
using static World.ChunkMeshGen.Debug;
#endif

#if EDGE_COLLAPSE
using static World.ChunkMeshGen.EdgeCollapse;
#elif SURFACE_NETS
using static World.ChunkMeshGen.SurfaceNets;
#endif

public partial class World {
	public struct ChunkTimingData_t {
		public long latency;
		public long voxelTime;
		public long verts1;
		public long verts2;

		public static ChunkTimingData_t operator + (ChunkTimingData_t a, ChunkTimingData_t b) {
			var z = default(ChunkTimingData_t);
			z.latency = a.latency + b.latency;
			z.voxelTime = a.voxelTime + b.voxelTime;
			z.verts1 = a.verts1 + b.verts1;
			z.verts2 = a.verts2 + b.verts2;
			return z;
		}
	};

	public struct PinnedChunkData_t {
		public ChunkStorageArray1D_t voxeldata;
		public DecorationStorageArray1D_t decorations;
		[NativeDisableUnsafePtrRestriction]
		public unsafe EChunkFlags* pinnedFlags;
		[NativeDisableUnsafePtrRestriction]
		public unsafe int* pinnedDecorationCount;
		[NativeDisableUnsafePtrRestriction]
		public unsafe ChunkTimingData_t* pinnedTiming;
		public EChunkFlags flags;
		public ChunkTimingData_t timing;
		public int decorationCount;
		public int valid;

		public void AddDecoration(Decoration_t d) {
			decorations[decorationCount++] = d;
		}
	};

	public unsafe struct ChunkStorageArray1D_t {
		[NativeDisableUnsafePtrRestriction]
		Voxel_t* _arr;
		int _x;

		public static ChunkStorageArray1D_t New(Voxel_t* array, int x) {
			return new ChunkStorageArray1D_t {
				_arr = array,
				_x = x
			};
		}

		public Voxel_t this[int i] {
			get {
				BoundsCheckAndThrow(i, 0, _x);
				return _arr[i];
			}
			set {
				BoundsCheckAndThrow(i, 0, _x);
				_arr[i] = value;
			}
		}

		public void Broadcast(Voxel_t v) {
			for (int i = 0; i < _x; ++i) {
				_arr[i] = v;
			}
		}

		public int length => _x;
	};

	public unsafe struct DecorationStorageArray1D_t {
		[NativeDisableUnsafePtrRestriction]
		Decoration_t* _arr;
		int _x;

		public static DecorationStorageArray1D_t New(Decoration_t* array, int x) {
			return new DecorationStorageArray1D_t {
				_arr = array,
				_x = x
			};
		}

		public Decoration_t this[int i] {
			get {
				BoundsCheckAndThrow(i, 0, _x);
				return _arr[i];
			}
			set {
				BoundsCheckAndThrow(i, 0, _x);
				_arr[i] = value;
			}
		}
	};

	static void BoundsCheckAndThrow(int i, int min, int max) {
#if BOUNDS_CHECK
		if ((i < min) || (i >= max)) {
			throw new IndexOutOfRangeException();
		}
#endif
	}

	public static EVoxelBlockContents GetBlockContents(EVoxelBlockType type) {
		return ChunkMeshGen.tableStorage.blockContents[(int)type];
	}

	public static partial class ChunkMeshGen {
		const int MAX_OUTPUT_VERTICES = (VOXEL_CHUNK_SIZE_XZ+1) * (VOXEL_CHUNK_SIZE_XZ+1) * (VOXEL_CHUNK_SIZE_Y+1);
		const int BANK_SIZE = 24;
		const int MAX_MATERIALS_PER_VERTEX = 16;

		const int BORDER_SIZE = 2;
		const int NUM_VOXELS_XZ = VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE*2;
		const int NUM_VOXELS_Y = VOXEL_CHUNK_SIZE_Y + BORDER_SIZE*2;
		const int MAX_VIS_VOXELS = NUM_VOXELS_XZ * NUM_VOXELS_XZ * NUM_VOXELS_Y;

		public struct ChunkData_t {
			public ChunkStorageArray1D_t pinnedBlockData;
			public DecorationStorageArray1D_t pinnedDecorationData;
			public unsafe int* pinnedDecorationCount;
			public unsafe ChunkTimingData_t* pinnedTimingData;
			public unsafe EChunkFlags* pinnedFlags;
			public Voxel_t[] voxeldata;
			public EChunkFlags[] flags;
			public Decoration_t[] decorations;
			public int[] decorationCount;
			public ChunkTimingData_t[] timing;

			GCHandle _pinnedBlockData;
			GCHandle _pinnedDecorationData;
			GCHandle _pinnedDecorationCount;
			GCHandle _pinnedFlags;
			GCHandle _pinnedTiming;

			int _pinCount;

			public static ChunkData_t New() {
				return new ChunkData_t {
					voxeldata = new Voxel_t[VOXELS_PER_CHUNK],
					decorations = new Decoration_t[Decoration_t.MAX_DECORATIONS_PER_CHUNK],
					decorationCount = new int[1],
					flags = new EChunkFlags[1],
					timing = new ChunkTimingData_t[1]
				};
			}

			public void Pin() {
				++_pinCount;

				if (_pinCount == 1) {
					Assert(!_pinnedBlockData.IsAllocated);
					_pinnedBlockData = GCHandle.Alloc(voxeldata, GCHandleType.Pinned);
					_pinnedDecorationData = GCHandle.Alloc(decorations, GCHandleType.Pinned);
					_pinnedDecorationCount = GCHandle.Alloc(decorationCount, GCHandleType.Pinned);
					_pinnedFlags = GCHandle.Alloc(flags, GCHandleType.Pinned);
					_pinnedTiming = GCHandle.Alloc(timing, GCHandleType.Pinned);
					unsafe {
						pinnedBlockData = ChunkStorageArray1D_t.New((Voxel_t*)_pinnedBlockData.AddrOfPinnedObject().ToPointer(), voxeldata.Length);
						pinnedDecorationData = DecorationStorageArray1D_t.New((Decoration_t*)_pinnedDecorationData.AddrOfPinnedObject().ToPointer(), decorations.Length);
						pinnedDecorationCount = (int*)_pinnedDecorationCount.AddrOfPinnedObject().ToPointer();
						pinnedFlags = (EChunkFlags*)_pinnedFlags.AddrOfPinnedObject().ToPointer();
						pinnedTimingData = (ChunkTimingData_t*)_pinnedTiming.AddrOfPinnedObject().ToPointer();
					}
				}
			}

			public void Unpin() {
				Assert(_pinCount > 0);

				--_pinCount;

				if (_pinCount == 0) {
					Assert(_pinnedBlockData.IsAllocated);
					_pinnedBlockData.Free();
					_pinnedDecorationData.Free();
					_pinnedDecorationCount.Free();
					_pinnedFlags.Free();
					_pinnedTiming.Free();

					pinnedBlockData = default(ChunkStorageArray1D_t);
					pinnedDecorationData = default(DecorationStorageArray1D_t);

					unsafe {
						pinnedDecorationCount = null;
						pinnedFlags = null;
						pinnedTimingData = null;
					}
				}
			}
		};

		public struct CompiledChunkData : System.IDisposable {
			public SmoothingVertsOut_t smoothVerts;
			public VoxelStorage_t voxelStorage;
			public FinalMeshVerts_t outputVerts;
			public NativeArray<PinnedChunkData_t> neighbors;

#if DEBUG_VOXEL_MESH
			public SmoothingVertsOutDebug_t smoothVertsDebug;
			public FinalMeshVertsDebug_t outputVertsDebug;
#endif

			public static CompiledChunkData New() {
				return new CompiledChunkData() {
#if DEBUG_VOXEL_MESH
					smoothVertsDebug = SmoothingVertsOutDebug_t.New(),
					outputVertsDebug = FinalMeshVertsDebug_t.New(),
#endif
					smoothVerts = SmoothingVertsOut_t.New(),
					voxelStorage = VoxelStorage_t.New(),
					outputVerts = FinalMeshVerts_t.New(),
					neighbors = AllocatePersistentNoInit<PinnedChunkData_t>(27)
				};
			}

			public void Dispose() {
#if DEBUG_VOXEL_MESH
				smoothVertsDebug.Dispose();
				outputVertsDebug.Dispose();
#endif
				smoothVerts.Dispose();
				outputVerts.Dispose();
				neighbors.Dispose();
			}
		};

		public static PinnedChunkData_t NewPinnedChunkData_t(ChunkData_t chunk) {
			unsafe {
				Assert(chunk.pinnedFlags != null);
				return new PinnedChunkData_t {
					voxeldata = chunk.pinnedBlockData,
					decorations = chunk.pinnedDecorationData,
					pinnedDecorationCount = chunk.pinnedDecorationCount,
					pinnedFlags = chunk.pinnedFlags,
					pinnedTiming = chunk.pinnedTimingData,
					decorationCount = chunk.decorationCount[0],
					flags = chunk.flags[0],
					timing = chunk.timing[0],
					valid = 1
				};
			}
		}

		static NativeArray<T> AllocatePersistentNoInit<T>(int size) where T : struct {
			return new NativeArray<T>(size, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
		}

		static unsafe void Memset(void* p, int size) {
			var ub = (byte*)p;
			for (int i = 0; i < size; ++i) {
				ub[i] = 0;
			}
		}

		static unsafe void ZeroMem(void* p, int sizeofP, int count) {
			Memset(p, count*sizeofP);
		}

		static unsafe void ZeroInts(int* p, int count) {
			for (int i = 0; i < count; ++i) {
				p[i] = 0;
			}
		}

		public struct ConstIntArrayRowEnumerator_t : System.Collections.Generic.IEnumerator<int> {
			int _i;
			ConstIntArrayRow_t _row;

			public static ConstIntArrayRowEnumerator_t New(ConstIntArrayRow_t row) {
				return new ConstIntArrayRowEnumerator_t {
					_i = -1,
					_row = row
				};
			}

			public int Current {
				get {
					return _row[_i];
				}
			}

			object IEnumerator.Current {
				get {
					throw new NotImplementedException();
				}
			}

			public void Dispose() { }

			public bool MoveNext() {
				++_i;
				return _i < _row.length;
			}

			public void Reset() {
				_i = -1;
			}
		};

		public unsafe struct ConstIntArrayRow_t : System.Collections.Generic.IEnumerable<int> {
			[NativeDisableUnsafePtrRestriction]
			int* _arr;
			int _size;

			public static ConstIntArrayRow_t New(int* array, int size) {
				return new ConstIntArrayRow_t {
					_arr = array,
					_size = size
				};
			}

			public int this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _size);
					return _arr[i];
				}
			}

			public int length {
				get {
					return _size;
				}
			}

			public IEnumerator<int> GetEnumerator() {
				return ConstIntArrayRowEnumerator_t.New(this);
			}

			IEnumerator IEnumerable.GetEnumerator() {
				throw new NotImplementedException();
			}
		};

		public struct ConstIntArray2DEnumerator_t : System.Collections.Generic.IEnumerator<ConstIntArrayRow_t> {
			int _i;
			ConstIntArray2D_t _arr;

			public static ConstIntArray2DEnumerator_t New(ConstIntArray2D_t arr) {
				return new ConstIntArray2DEnumerator_t {
					_i = -1,
					_arr = arr
				};
			}

			public ConstIntArrayRow_t Current {
				get {
					return _arr[_i];
				}
			}

			object IEnumerator.Current {
				get {
					throw new NotImplementedException();
				}
			}

			public void Dispose() { }

			public bool MoveNext() {
				++_i;
				return _i < _arr.length;
			}

			public void Reset() {
				_i = -1;
			}
		};

		public unsafe struct ConstIntArray2D_t : System.Collections.Generic.IEnumerable<ConstIntArrayRow_t> {
			[NativeDisableUnsafePtrRestriction]
			int* _arr;
			int _x;
			int _y;

			public static ConstIntArray2D_t New(int* array, int x, int y) {
				return new ConstIntArray2D_t {
					_arr = array,
					_x = x,
					_y = y
				};
			}

			public ConstIntArrayRow_t this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return ConstIntArrayRow_t.New(_arr + (i*_y), _y);
				}
			}

			public int length {
				get {
					return _x;
				}
			}

			public IEnumerator<ConstIntArrayRow_t> GetEnumerator() {
				return ConstIntArray2DEnumerator_t.New(this);
			}

			IEnumerator IEnumerable.GetEnumerator() {
				throw new NotImplementedException();
			}
		};

		public unsafe struct ConstVoxelBlockContentsArray1D_t {
			[NativeDisableUnsafePtrRestriction]
			EVoxelBlockContents* _arr;
			int _x;
			
			public static ConstVoxelBlockContentsArray1D_t New(EVoxelBlockContents* array, int x) {
				return new ConstVoxelBlockContentsArray1D_t {
					_arr = array,
					_x = x
				};
			}

			public EVoxelBlockContents this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return _arr[i];
				}
			}

			public int length {
				get {
					return _x;
				}
			}
		};

		public unsafe struct ConstIntArray1D_t {
			[NativeDisableUnsafePtrRestriction]
			int* _arr;
			int _x;

			public static ConstIntArray1D_t New(int* array, int x) {
				return new ConstIntArray1D_t {
					_arr = array,
					_x = x
				};
			}

			public int this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return _arr[i];
				}
			}

			public int length {
				get {
					return _x;
				}
			}
		};

		public unsafe struct ConstUIntArray1D_t {
			[NativeDisableUnsafePtrRestriction]
			uint* _arr;
			int _x;

			public static ConstUIntArray1D_t New(uint* array, int x) {
				return new ConstUIntArray1D_t {
					_arr = array,
					_x = x
				};
			}

			public uint this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return _arr[i];
				}
			}

			public int length {
				get {
					return _x;
				}
			}
		};

		public unsafe struct ConstFloatArray1D_t {
			[NativeDisableUnsafePtrRestriction]
			float* _arr;
			int _x;

			public static ConstFloatArray1D_t New(float* array, int x) {
				return new ConstFloatArray1D_t {
					_arr = array,
					_x = x
				};
			}

			public float this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return _arr[i];
				}
			}

			public int length {
				get {
					return _x;
				}
			}
		};

		public unsafe struct ConstColor32Array1D_t {
			[NativeDisableUnsafePtrRestriction]
			Color32* _arr;
			int _x;

			public static ConstColor32Array1D_t New(Color32* array, int x) {
				return new ConstColor32Array1D_t {
					_arr = array,
					_x = x
				};
			}

			public Color32 this[int i] {
				get {
					BoundsCheckAndThrow(i, 0, _x);
					return _arr[i];
				}
			}
		};

		public class TableStorage : System.IDisposable {
			public ConstIntArray1D_t cubeEdges;
			public ConstIntArray1D_t edgeTable;
			public ConstIntArray2D_t voxelVerts;
			public ConstIntArray2D_t voxelFaces;
			public ConstIntArray2D_t voxelFaceNormal;
			public ConstIntArray2D_t collapseMap;
			public ConstIntArray2D_t spanningAxis;
			public ConstUIntArray1D_t blockSmoothingGroups;
			public ConstColor32Array1D_t blockColors;
			public ConstFloatArray1D_t blockSmoothingFactors;
			public ConstVoxelBlockContentsArray1D_t blockContents;

			int[] _cubeEdges;
			int[] _edgeTable;
			int[,] _voxelVerts;
			int[,] _voxelFaces;
			int[,] _voxelFaceNormal;
			int[,] _collapseMap;
			int[,] _spanningAxis;
			uint[] _blockSmoothingGroups;
			Color32[] _blockColors;
			float[] _blockSmoothingFactors;
			EVoxelBlockContents[] _blockContents;

			GCHandle _pinnedVoxelVerts;
			GCHandle _pinnedVoxelFaces;
			GCHandle _pinnedVoxelFaceNormal;
			GCHandle _pinnedCollapseMap;
			GCHandle _pinnedSpanningAxis;
			GCHandle _pinnedBlockSmoothingGroups;
			GCHandle _pinnedBlockColors;
			GCHandle _pinnedBlockSmoothingFactors;
			GCHandle _pinnedBlockContents;

			public static TableStorage New() {
				var t = new TableStorage();
				t.Init();
				return t;
			}

			public Color32[] blockColorsArray => _blockColors;

			void Init() {

				_cubeEdges = new int[24];
				_edgeTable = new int[256];

				int k = 0;
				for (int i = 0; i < 8; ++i) {
					for (int j = 1; j <= 4; j <<= 1) {
						var spanVert = i^j;
						if (i <= spanVert) {
							_cubeEdges[k++] = i;
							_cubeEdges[k++] = spanVert;
						}
					}
				}

				_voxelVerts = new int[8, 3] {
					{ 0,0,0 },
					{ 1,0,0 },
					{ 0,1,0 },
					{ 1,1,0 },
					{ 0,0,1 },
					{ 1,0,1 },
					{ 0,1,1 },
					{ 1,1,1 }
				};

				_pinnedVoxelVerts = GCHandle.Alloc(_voxelVerts, GCHandleType.Pinned);

				unsafe {
					voxelVerts = ConstIntArray2D_t.New((int*)_pinnedVoxelVerts.AddrOfPinnedObject().ToPointer(), 8, 3);
				}

				_voxelFaces = new int[6, 4] {
					// +x
					{ 1,3,7,5 },
					// -x
					{ 2,0,4,6 },
					// +y
					{ 3,2,6,7 },
					// -y
					{ 0,1,5,4 },
					// +z
					{ 4,5,7,6 },
					// -z
					{ 2,3,1,0 }
				};

				_pinnedVoxelFaces = GCHandle.Alloc(_voxelFaces, GCHandleType.Pinned);

				unsafe {
					voxelFaces = ConstIntArray2D_t.New((int*)_pinnedVoxelFaces.AddrOfPinnedObject().ToPointer(), 6, 4);
				}

				_voxelFaceNormal = new int[6, 3] {
					//+x
					{ 1,0,0 },
					//-x
					{ -1,0,0 },
					//+y
					{ 0,1,0 },
					//-y
					{ 0,-1,0 },
					//+z
					{ 0,0,1 },
					//-z
					{ 0,0,-1 }
				};

				_pinnedVoxelFaceNormal = GCHandle.Alloc(_voxelFaceNormal, GCHandleType.Pinned);

				unsafe {
					voxelFaceNormal = ConstIntArray2D_t.New((int*)_pinnedVoxelFaceNormal.AddrOfPinnedObject().ToPointer(), 6, 3);
				}

				_collapseMap = new int[8, 3] {
					{ 1, 2, 4 },
					{ 0, 3, 5 },
					{ 3, 0, 6 },
					{ 2, 1, 7 },
					{ 5, 6, 0 },
					{ 4, 7, 1 },
					{ 7, 4, 2 },
					{ 6, 5, 3 }
				};

				_pinnedCollapseMap = GCHandle.Alloc(_collapseMap, GCHandleType.Pinned);

				unsafe {
					collapseMap = ConstIntArray2D_t.New((int*)_pinnedCollapseMap.AddrOfPinnedObject().ToPointer(), 8, 3);
				}

				_spanningAxis = new int[3, 2] {
					// x -> y/z
					{ 1, 2 },
					// y -> x/z
					{ 0, 2 },
					// z -> x/y
					{ 0, 1 }
				};

				_pinnedSpanningAxis = GCHandle.Alloc(_spanningAxis, GCHandleType.Pinned);

				unsafe {
					spanningAxis = ConstIntArray2D_t.New((int*)_pinnedSpanningAxis.AddrOfPinnedObject().ToPointer(), 3, 2);
				}

				// Block contents

				_blockContents = new EVoxelBlockContents[(int)EVoxelBlockType.NumBlockTypes] {
					EVoxelBlockContents.None,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Water,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid,
					EVoxelBlockContents.Solid
				};

				_pinnedBlockContents = GCHandle.Alloc(_blockContents, GCHandleType.Pinned);

				unsafe {
					blockContents = ConstVoxelBlockContentsArray1D_t.New((EVoxelBlockContents*)_pinnedBlockContents.AddrOfPinnedObject().ToPointer(), (int)EVoxelBlockType.NumBlockTypes);
				}

				// Block colors

				_blockColors = new Color32[(int)EVoxelBlockType.NumBlockTypes - 1] {
					new Color32(153, 102, 51, 255),
					new Color32(20, 163, 61, 255),
					new Color32(0, 50, 255, 255),
					new Color32(200, 190, 100, 255),
					new Color32(240, 240, 255, 255),
					new Color32(80, 100, 120, 255),
					new Color32(200, 255, 255, 255),
					new Color32(150, 100, 25, 255),
					new Color32(60, 200, 20, 255),
					new Color32(10, 100, 60, 255),
					new Color32(250, 250, 50, 255),
					new Color32(200, 10, 0, 255),
					new Color32(150, 0, 200, 255),
					new Color32(50, 0, 200, 255)
				};

				_pinnedBlockColors = GCHandle.Alloc(_blockColors, GCHandleType.Pinned);

				unsafe {
					blockColors = ConstColor32Array1D_t.New((Color32*)_pinnedBlockColors.AddrOfPinnedObject().ToPointer(), (int)EVoxelBlockType.NumBlockTypes - 1);
				}

				// Defines the blending

				_blockSmoothingGroups = new uint[(int)EVoxelBlockType.NumBlockTypes - 1] {
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_DIRT -> blends with rock and grass
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_GRASS
					BLOCK_SMG_WATER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_WATER
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_SAND
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_SNOW
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_ROCK
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_ICE
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_WOOD
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_LEAVES
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_NEEDLES
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_FLOWERS1
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_FLOWERS2
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_FLOWERS3
					BLOCK_SMG_OTHER | BLOCK_BLEND_COLORS // BLOCK_TYPE_FLOWERS4
				};

				_pinnedBlockSmoothingGroups = GCHandle.Alloc(_blockSmoothingGroups, GCHandleType.Pinned);

				unsafe {
					blockSmoothingGroups = ConstUIntArray1D_t.New((uint*)_pinnedBlockSmoothingGroups.AddrOfPinnedObject().ToPointer(), (int)EVoxelBlockType.NumBlockTypes - 1);
				}

				// 1f = most smooth, 0 = very faceted

				_blockSmoothingFactors = new float[(int)EVoxelBlockType.NumBlockTypes - 1] {
					0.85f, // BLOCK_TYPE_DIRT
					0.85f, // BLOCK_TYPE_GRASS
					0.85f, // BLOCK_TYPE_WATER
					0.85f, // BLOCK_TYPE_SAND
					0.85f, // BLOCK_TYPE_SNOW
					0.85f, // BLOCK_TYPE_ROCK
					0.85f, // BLOCK_TYPE_ICE
					0.85f, // BLOCK_TYPE_WOOD
					0.85f, // BLOCK_TYPE_LEAVES
					0.85f, // BLOCK_TYPE_NEEDLES
					0.85f, // BLOCK_TYPE_FLOWERS1
					0.85f, // BLOCK_TYPE_FLOWERS2
					0.85f, // BLOCK_TYPE_FLOWERS3
					0.85f  // BLOCK_TYPE_FLOWERS4
				};

				_pinnedBlockSmoothingFactors = GCHandle.Alloc(_blockSmoothingFactors, GCHandleType.Pinned);

				unsafe {
					blockSmoothingFactors = ConstFloatArray1D_t.New((float*)_pinnedBlockSmoothingFactors.AddrOfPinnedObject().ToPointer(), (int)EVoxelBlockType.NumBlockTypes - 1);
				}
			}

			public void Dispose() {
				_pinnedVoxelVerts.Free();
				_pinnedVoxelFaces.Free();
				_pinnedVoxelFaceNormal.Free();
				_pinnedCollapseMap.Free();
				_pinnedSpanningAxis.Free();
				_pinnedBlockColors.Free();
				_pinnedBlockSmoothingGroups.Free();
				_pinnedBlockSmoothingFactors.Free();
			}
		};

		struct Tables {
			public ConstIntArray2D_t voxelVerts;
			public ConstIntArray2D_t voxelFaces;
			public ConstIntArray2D_t voxelFaceNormal;
			public ConstIntArray2D_t collapseMap;
			public ConstIntArray2D_t spanningAxis;
			public ConstColor32Array1D_t blockColors;
			public ConstUIntArray1D_t blockSmoothingGroups;
			public ConstFloatArray1D_t blockSmoothingFactors;
			public ConstVoxelBlockContentsArray1D_t blockContents;

			public static Tables New(TableStorage storage) {
				return new Tables {
					voxelVerts = storage.voxelVerts,
					voxelFaces = storage.voxelFaces,
					voxelFaceNormal = storage.voxelFaceNormal,
					collapseMap = storage.collapseMap,
					spanningAxis = storage.spanningAxis,
					blockColors = storage.blockColors,
					blockSmoothingGroups = storage.blockSmoothingGroups,
					blockSmoothingFactors = storage.blockSmoothingFactors,
					blockContents = storage.blockContents
				};
			}
		};

		public struct TexBlend_t {
			public int x, y, z, w;
			public int count;
		};

		public struct FinalMeshVerts_t : System.IDisposable {

			[WriteOnly]
			public NativeArray<Vector3> positions;
			public NativeArray<Vector3> normals;
			public NativeArray<Color32> colors;
			public NativeArray<Vector4> textureBlending;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts; // [vertCount][indexCount][submeshCount]
			[WriteOnly]
			public NativeArray<int> submeshes;
			[WriteOnly]
			public NativeArray<TexBlend_t> submeshTextures;

			NativeArray<int> _vtoi;
			NativeArray<int> _vtoiCounts;

			int _vertCount;
			int _indexCount;
			int _layerVertOfs;
			int _layerIndexOfs;
			int _layer;

			public static FinalMeshVerts_t New() {
				var verts = new FinalMeshVerts_t {
					positions = AllocatePersistentNoInit<Vector3>(ushort.MaxValue),
					normals = AllocatePersistentNoInit<Vector3>(ushort.MaxValue),
					colors = AllocatePersistentNoInit<Color32>(ushort.MaxValue),
					textureBlending = AllocatePersistentNoInit<Vector4>(ushort.MaxValue),
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(3*MAX_CHUNK_LAYERS),
					submeshes = AllocatePersistentNoInit<int>(MAX_CHUNK_SUBMESHES*MAX_CHUNK_LAYERS),
					submeshTextures = AllocatePersistentNoInit<TexBlend_t>(MAX_CHUNK_SUBMESHES*MAX_CHUNK_LAYERS),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES*BANK_SIZE),
					_vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				normals.Dispose();
				colors.Dispose();
				textureBlending.Dispose();
				indices.Dispose();
				counts.Dispose();
				submeshes.Dispose();
				submeshTextures.Dispose();
				_vtoi.Dispose();
				_vtoiCounts.Dispose();
			}

			public void Init() {
				_vertCount = 0;
				_indexCount = 0;

				for (int i = 0; i < counts.Length; ++i) {
					counts[i] = 0;
				}
			}

			public void BeginLayer(int layer) {
				_layer = layer;
				_layerVertOfs = _vertCount;
				_layerIndexOfs = _indexCount;

				for (int i = 0; i < _vtoiCounts.Length; ++i) {
					_vtoiCounts[i] = 0;
				}
			}

			public void FinishLayer(int maxSubmesh) {
				counts[(_layer*3)+0] = _vertCount - _layerVertOfs;
				counts[(_layer*3)+1] = _indexCount - _layerIndexOfs;
				counts[(_layer*3)+2] = maxSubmesh;
			}

			static bool ColorEqual(Color32 a, Color32 b) {
				return (a.r == b.r) &&
				(a.g == b.g) &&
				(a.b == b.b) &&
				(a.a == b.a);
			}

			public void EmitVert(int x, int y, int z, Vector3 normal, Color32 color, Vector4 textureBlending) {
				int INDEX = (y*(VOXEL_CHUNK_SIZE_XZ+1)*(VOXEL_CHUNK_SIZE_XZ+1)) + (z*(VOXEL_CHUNK_SIZE_XZ+1)) + x;

				// existing vertex?
				int vtoiCount = _vtoiCounts[INDEX];
				for (int i = 0; i < vtoiCount; ++i) {
					int idx = _vtoi[(INDEX*BANK_SIZE)+i];
					if (ColorEqual(colors[idx], color) && (Vector4.Equals(this.textureBlending[idx], textureBlending)) && (Vector3.Equals(normals[idx], normal))) {
						Assert((_indexCount-_layerIndexOfs) < ushort.MaxValue);
						indices[_indexCount++] = idx - _layerVertOfs;
						return;
					}
				}

				Assert((_vertCount-_layerVertOfs) < ushort.MaxValue);
				Assert((_indexCount-_layerIndexOfs) < ushort.MaxValue);
				Assert(vtoiCount < BANK_SIZE);

				positions[_vertCount] = new Vector3(x, y, z);
				normals[_vertCount] = normal;
				colors[_vertCount] = color;
				this.textureBlending[_vertCount] = textureBlending;

				_vtoi[(INDEX*BANK_SIZE)+vtoiCount] = _vertCount;
				_vtoiCounts[INDEX] = vtoiCount + 1;

				indices[_indexCount++] = _vertCount - _layerVertOfs;
				_vertCount++;
			}
		};

		public struct Int3_t {
			public int x, y, z;
		};

		struct SmoothingVertsIn_t {
			[ReadOnly]
			public NativeArray<Int3_t> positions;
			[ReadOnly]
			public NativeArray<Vector3> normals;
			[ReadOnly]
			public NativeArray<Color32> colors;
			[ReadOnly]
			public NativeArray<float> smoothFactor;
			[ReadOnly]
			public NativeArray<uint> smgs;
			[ReadOnly]
			public NativeArray<int> layers;
			[ReadOnly]
			public NativeArray<int> indices;
			[ReadOnly]
			public NativeArray<int> counts;
			[ReadOnly]
			public NativeArray<int> vtoiCounts;
			[ReadOnly]
			public NativeArray<int> vertMaterials;
			[ReadOnly]
			public NativeArray<int> vertMaterialCount;

			public static SmoothingVertsIn_t New(SmoothingVertsOut_t smv) {
				return new SmoothingVertsIn_t {
					positions = smv.positions,
					normals = smv.normals,
					colors = smv.colors,
					smoothFactor = smv.smoothFactor,
					smgs = smv.smgs,
					layers = smv.layers,
					indices = smv.indices,
					counts = smv.counts,
					vtoiCounts = smv.vtoiCounts,
					vertMaterials = smv.vertMaterials,
					vertMaterialCount = smv.vertMaterialCount
				};
			}
		};

		public struct SmoothingVertsOut_t : System.IDisposable {
			[WriteOnly]
			public NativeArray<Int3_t> positions;
			[WriteOnly]
			public NativeArray<Vector3> normals;
			[WriteOnly]
			public NativeArray<Color32> colors;
			[WriteOnly]
			public NativeArray<float> smoothFactor;
			[WriteOnly]
			public NativeArray<uint> smgs;
			[WriteOnly]
			public NativeArray<int> layers;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts; // [numIndices][maxLayers]
			public NativeArray<int> vtoiCounts;
			
			public NativeArray<int> vertMaterials;
			public NativeArray<int> vertMaterialCount;
						
			int _maxLayer;

			NativeArray<int> _vtoi;

			int _vertCount;
			int _indexCount;

			public static SmoothingVertsOut_t New() {
				var verts = new SmoothingVertsOut_t {
					positions = AllocatePersistentNoInit<Int3_t>(ushort.MaxValue),
					normals = AllocatePersistentNoInit<Vector3>(ushort.MaxValue*BANK_SIZE),
					colors = AllocatePersistentNoInit<Color32>(ushort.MaxValue*BANK_SIZE),
					smoothFactor = AllocatePersistentNoInit<float>(ushort.MaxValue*BANK_SIZE),
					smgs = AllocatePersistentNoInit<uint>(ushort.MaxValue*BANK_SIZE),
					layers = AllocatePersistentNoInit<int>(ushort.MaxValue*BANK_SIZE),
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(2),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES),
					vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES),
					vertMaterials = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES*MAX_MATERIALS_PER_VERTEX),
					vertMaterialCount = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				normals.Dispose();
				colors.Dispose();
				smoothFactor.Dispose();
				smgs.Dispose();
				layers.Dispose();
				indices.Dispose();
				counts.Dispose();
				_vtoi.Dispose();
				vtoiCounts.Dispose();
				vertMaterials.Dispose();
				vertMaterialCount.Dispose();
			}

			public void Init() {
				_vertCount = 0;
				_indexCount = 0;
				_maxLayer = 0;
				for (int i = 0; i < _vtoi.Length; ++i) {
					_vtoi[i] = 0;
				}
				for (int i = 0; i < vertMaterialCount.Length; ++i) {
					vertMaterialCount[i] = 0;
				}
			}

			public void Finish() {
				counts[0] = _indexCount;
				counts[1] = _maxLayer;
			}

			int EmitVert(int x, int y, int z, uint smg, float smoothingFactor, Color32 color, Vector3 normal, int layer) {
				int INDEX = (y*(VOXEL_CHUNK_SIZE_XZ + 1)*(VOXEL_CHUNK_SIZE_XZ + 1)) + (z*(VOXEL_CHUNK_SIZE_XZ + 1)) + x;

				var idx = _vtoi[INDEX] - 1;
				if (idx < 0) {
					Assert(_vertCount < ushort.MaxValue);
					idx = _vertCount++;
					vtoiCounts[idx] = 0;
					_vtoi[INDEX] = idx+1;
					positions[idx] = new Int3_t {
						x = x,
						y = y,
						z = z
					};
				}

				var count = vtoiCounts[idx];
				Assert(count < BANK_SIZE);

				normals[(idx*BANK_SIZE) + count] = normal;
				colors[(idx*BANK_SIZE) + count] = color;
				smoothFactor[(idx*BANK_SIZE) + count] = smoothingFactor;
				smgs[(idx*BANK_SIZE) + count] = smg;
				layers[(idx*BANK_SIZE) + count] = layer;

				vtoiCounts[idx] = count + 1;
				_maxLayer = (layer > _maxLayer) ? layer : _maxLayer;

				return idx | (count << 24);
			}

			void AddVertexMaterial(int v0, int layer, int material) {
				var count = vertMaterialCount[v0];
				var code = material | (layer << 16);

				var OFS = v0 * MAX_MATERIALS_PER_VERTEX;
				for (int i = 0; i < count; ++i) {
					if (vertMaterials[OFS+i] == code) {
						return;
					}
				}

				if (count < MAX_MATERIALS_PER_VERTEX) {
					vertMaterials[OFS+count] = code;
					vertMaterialCount[v0] = count + 1;
				}
			}
					
			public void EmitTri(int x0, int y0, int z0, int x1, int y1, int z1, int x2, int y2, int z2, uint smg, float smoothFactor, Color32 color, int layer, int material, bool isBorderVoxel) {
				var n = GetNormalAndAngles((float)x0, (float)y0, (float)z0, (float)x1, (float)y1, (float)z1, (float)x2, (float)y2, (float)z2);

				int v0 = -1;
				int v1 = -1;
				int v2 = -1;

				if (isBorderVoxel) {
					if ((x0 >= 0) && (x0 <= VOXEL_CHUNK_SIZE_XZ) && (y0 >= 0) && (y0 <= VOXEL_CHUNK_SIZE_Y) && (z0 >= 0) && (z0 <= VOXEL_CHUNK_SIZE_XZ)) {
						v0 = EmitVert(x0, y0, z0, smg, smoothFactor, color, n, layer);
					}
					if ((x1 >= 0) && (x1 <= VOXEL_CHUNK_SIZE_XZ) && (y1 >= 0) && (y1 <= VOXEL_CHUNK_SIZE_Y) && (z1 >= 0) && (z1 <= VOXEL_CHUNK_SIZE_XZ)) {
						v1 = EmitVert(x1, y1, z1, smg, smoothFactor, color, n, layer);
					}
					if ((x2 >= 0) && (x2 <= VOXEL_CHUNK_SIZE_XZ) && (y2 >= 0) && (y2 <= VOXEL_CHUNK_SIZE_Y) && (z2 >= 0) && (z2 <= VOXEL_CHUNK_SIZE_XZ)) {
						v2 = EmitVert(x2, y2, z2, smg, smoothFactor, color, n, layer);
					}
				} else {
					indices[_indexCount++] = v0 = EmitVert(x0, y0, z0, smg, smoothFactor, color, n, layer);
					indices[_indexCount++] = v1 = EmitVert(x1, y1, z1, smg, smoothFactor, color, n, layer);
					indices[_indexCount++] = v2 = EmitVert(x2, y2, z2, smg, smoothFactor, color, n, layer);
				}

				if (v0 != -1) {
					AddVertexMaterial(v0 & 0x00ffffff, layer, material);
				}
				if (v1 != -1) {
					AddVertexMaterial(v1 & 0x00ffffff, layer, material);
				}
				if (v2 != -1) {
					AddVertexMaterial(v2 & 0x00ffffff, layer, material);
				}
			}

			static Vector3 GetNormalAndAngles(float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2) {
				var a = new Vector3(x0, y0, z0);
				var b = new Vector3(x1, y1, z1);
				var c = new Vector3(x2, y2, z2);
				var u = (b - a).normalized;
				var v = (c - a).normalized;

				return Vector3.Cross(u, v).normalized;
			}
		};

		struct GenerateFinalVertices_t : Unity.Jobs.IJob {
			SmoothingVertsIn_t _smoothVerts;
			FinalMeshVerts_t _finalVerts;
			[NativeDisableUnsafePtrRestriction]
			unsafe ChunkTimingData_t* _timing;
			[NativeDisableContainerSafetyRestriction]
			NativeArray<int> _materials;
			[NativeDisableContainerSafetyRestriction]
			NativeArray<int> _matrefcounts;

			public unsafe static GenerateFinalVertices_t New(SmoothingVertsIn_t inVerts, FinalMeshVerts_t outVerts, ChunkTimingData_t* timing) {
				return new GenerateFinalVertices_t {
					_smoothVerts = inVerts,
					_finalVerts = outVerts,
					_timing = timing
				};
			}

			public void Execute() {
				var start = Utils.ReadTimestamp();
				Run();
				unsafe {
					_timing->verts2 = Utils.ReadTimestamp() - start;
				}
			}

			static bool AddSubmeshTexture(ref TexBlend_t blend, int material, int max) {
				if (max > 0) {
					if (blend.count < 1) {
						blend.x = material;
						blend.count = 1;
						return true;
					}
					if (blend.x == material) {
						return true;
					};

					if (max > 1) {
						if (blend.count < 2) {
							blend.y = material;
							blend.count = 2;
							return true;
						}
						if (blend.y == material) {
							return true;
						}

						if (max > 2) {
							if (blend.count < 3) {
								blend.z = material;
								blend.count = 3;
								return true;
							}
							if (blend.z == material) {
								return true;
							}

							if (max > 3) {
								if (blend.count < 4) {
									blend.w = material;
									blend.count = 4;
									return true;
								}
								if (blend.w == material) {
									return true;
								}
							}
						}
					}
				}

				return false;
			}

			Vector4 SetMaterialBlendFactor(Vector4 blendFactor, TexBlend_t blend, int m, float frac) {
				if (blend.x == m) {
					blendFactor.x = frac;
					return blendFactor;
				}

				if (blend.y == m) {
					blendFactor.y = frac;
					return blendFactor;
				}

				if (blend.z == m) {
					blendFactor.z = frac;
					return blendFactor;
				}

				blendFactor.w = frac;
				return blendFactor;
			}

			Vector4 GetTriVertTexBlendFactor(TexBlend_t blend, int layer, int v0) {
				var num = 0;

				AddVertexMaterials(layer, v0, ref num);

				float total = 0f;
				for (int i = 0; i < num; ++i) {
					total += _matrefcounts[i];
				}

				var blendFactor = Vector4.zero;

				for (int i = 0; i < num; ++i) {
					var frac = _matrefcounts[i] / total;
					blendFactor = SetMaterialBlendFactor(blendFactor, blend, _materials[i], frac);
				}

				return blendFactor;
			}

			void AddVertexMaterials(int layer, int v0, ref int num) {
				var OFS = v0 * MAX_MATERIALS_PER_VERTEX;
				var count = _smoothVerts.vertMaterialCount[v0];

				for (int i = 0; i < count; ++i) {
					var code = _smoothVerts.vertMaterials[OFS+i];

					var materialLayer = code >> 16;
					if (layer == materialLayer) {
						var m = code & 0xffff;
						AddMaterial(m, ref num);
					}
				}
			}

			void AddMaterial(int m, ref int num) {
				for (int i = 0; i < num; ++i) {
					if (_materials[i] == m) {
						++_matrefcounts[i];
						return;
					}
				}

				if (num < MAX_MATERIALS_PER_SUBMESH) {
					_materials[num] = m;
					_matrefcounts[num] = 1;
					++num;
				}
			}

			int AddVertexMaterials(int layer, int v0, int v1, int v2) {
				int num = 0;

				AddVertexMaterials(layer, v0, ref num);
				AddVertexMaterials(layer, v1, ref num);
				AddVertexMaterials(layer, v2, ref num);

				return num;
			}

			bool AddSubmeshMaterials(ref TexBlend_t texBlend, int num, int max) {
				var blend = texBlend;
				for (int i = 0; i < num; ++i) {
					if (!AddSubmeshTexture(ref blend, _materials[i], max)) {
						return false;
					}
				}
				texBlend = blend;
				return true;
			}

			void Run() {
				_finalVerts.Init();

				var numIndices = _smoothVerts.counts[0];
				var maxLayer = _smoothVerts.counts[1];

				_materials = new NativeArray<int>(MAX_MATERIALS_PER_SUBMESH, Allocator.Temp, NativeArrayOptions.ClearMemory);
				_matrefcounts = new NativeArray<int>(MAX_MATERIALS_PER_SUBMESH, Allocator.Temp, NativeArrayOptions.ClearMemory);

				var emitFlags = new NativeArray<int>(numIndices / 3, Allocator.Temp, NativeArrayOptions.ClearMemory);

				for (int layer = 0; layer <= maxLayer; ++layer) {
					int maxLayerSubmesh = -1;
					int numEmitted = 0;
					
					_finalVerts.BeginLayer(layer);
					emitFlags.Broadcast(0);

					while (numEmitted < numIndices) {
						for (int maxMats = 1; maxMats <= MAX_MATERIALS_PER_SUBMESH; ++maxMats) {
							if (numEmitted >= numIndices) {
								break;
							}
							var texBlend = default(TexBlend_t);

							int numTris = 0;
							int firstIndex = 0;

							// pack submesh edges textures

							for (int k = 0; k < numIndices; k += 3) {
								if (emitFlags[k/3] == 0) {
									int packedIndex0 = _smoothVerts.indices[k];
									int vertNum0 = packedIndex0 & 0x00ffffff;
									int vertOfs0 = packedIndex0 >> 24;
									int bankedIndex0 = (vertNum0*BANK_SIZE)+vertOfs0;

									if (_smoothVerts.layers[bankedIndex0] == layer) {

										int packedIndex1 = _smoothVerts.indices[k+1];
										int vertNum1 = packedIndex1 & 0x00ffffff;
										int packedIndex2 = _smoothVerts.indices[k+2];
										int vertNum2 = packedIndex2 & 0x00ffffff;

										var numMats = AddVertexMaterials(layer, vertNum0, vertNum1, vertNum2);
										if ((numMats > 0) && (numMats <= maxMats)) {
											if (AddSubmeshMaterials(ref texBlend, numMats, maxMats)) {
												if (numTris == 0) {
													firstIndex = k;
												}

												++numTris;
											}
										} /*else {
										emitFlags[k/3] = 1;
										numEmitted = 3;
									}*/
									} else {
										emitFlags[k/3] = 1;
										numEmitted += 3;
									}
								}
							}

							if (numTris > 0) {
								// we've packed as many triangles as we can into a TexBlend_t
								// write out the packed submesh.
								if (maxLayerSubmesh == MAX_CHUNK_SUBMESHES) {
									throw new Exception("MAX_CHUNK_SUBMESHES");
								}

								++maxLayerSubmesh;
								int numSubmeshVerts = 0;
								var curBlend = default(TexBlend_t);

								for (int k = firstIndex; k < numIndices; k += 3) {
									if (emitFlags[k/3] == 0) {
										int packedIndex0 = _smoothVerts.indices[k];
										int vertNum0 = packedIndex0 & 0x00ffffff;
										int vertOfs0 = packedIndex0 >> 24;
										int bankedIndex0 = (vertNum0*BANK_SIZE)+vertOfs0;

										int packedIndex1 = _smoothVerts.indices[k+1];
										int vertNum1 = packedIndex1 & 0x00ffffff;
										int vertOfs1 = packedIndex1 >> 24;
										int bankedIndex1 = (vertNum1*BANK_SIZE)+vertOfs1;

										int packedIndex2 = _smoothVerts.indices[k+2];
										int vertNum2 = packedIndex2 & 0x00ffffff;
										int vertOfs2 = packedIndex2 >> 24;
										int bankedIndex2 = (vertNum2*BANK_SIZE)+vertOfs2;

										var numMats = AddVertexMaterials(layer, vertNum0, vertNum1, vertNum2);
										if ((numMats > 0) && (numMats <= maxMats)) {
											if (AddSubmeshMaterials(ref curBlend, numMats, maxMats)) {
												Int3_t p;
												Vector3 n;
												Color32 c;
												Vector4 blendFactor;

												BlendVertex(vertNum0, vertOfs0, bankedIndex0, out p, out n, out c);
												blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum0);
												_finalVerts.EmitVert(p.x, p.y, p.z, n, c, blendFactor);

												BlendVertex(vertNum1, vertOfs1, bankedIndex1, out p, out n, out c);
												blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum1);
												_finalVerts.EmitVert(p.x, p.y, p.z, n, c, blendFactor);

												BlendVertex(vertNum2, vertOfs2, bankedIndex2, out p, out n, out c);
												blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum2);
												_finalVerts.EmitVert(p.x, p.y, p.z, n, c, blendFactor);

												numSubmeshVerts += 3;

												emitFlags[k/3] = 1;
												numEmitted += 3;
											}
										}
									}
								}

								_finalVerts.submeshTextures[(layer*MAX_CHUNK_SUBMESHES)+maxLayerSubmesh] = texBlend;
								_finalVerts.submeshes[(layer*MAX_CHUNK_SUBMESHES)+maxLayerSubmesh] = numSubmeshVerts;
							}
						}
					}

					_finalVerts.FinishLayer(maxLayerSubmesh);
				}

				_materials.Dispose();
				_matrefcounts.Dispose();
				emitFlags.Dispose();
			}

			void BlendVertex(int index, int ofs, int bankedIndex, out Int3_t outPos, out Vector3 outNormal, out Color32 outColor) {
				
				var originalNormal = _smoothVerts.normals[bankedIndex];
				var summedNormal = originalNormal;

				Vector4 c = (Color)_smoothVerts.colors[bankedIndex];

				var smg = _smoothVerts.smgs[bankedIndex];

				float factor = 1f - _smoothVerts.smoothFactor[bankedIndex];

				float w = 1f;

				if (smg != 0) {
					var num = _smoothVerts.vtoiCounts[index];
					for (int i = 0; i < num; ++i) {
						if (i != ofs) {
							if ((_smoothVerts.smgs[(index*BANK_SIZE) + i] & smg & ~BLOCK_BLEND_COLORS) != 0) {
								if ((_smoothVerts.smgs[(index*BANK_SIZE) + i] & smg & BLOCK_BLEND_COLORS) != 0) {
									c += (Vector4)(Color)_smoothVerts.colors[(index*BANK_SIZE) + i];
									w += 1f;
								}

								var blendNormal = _smoothVerts.normals[(index*BANK_SIZE) + i];
								var dot = Vector3.Dot(blendNormal, originalNormal);
								if (dot >= factor) {
									var checkNormal = summedNormal + blendNormal;
									var nml = checkNormal.normalized;
									dot = Vector3.Dot(nml, originalNormal);

									if (dot >= 0.33f) {
										summedNormal = checkNormal;
									}
								}
							}
						}
					}

					c /= w;
				}

				outPos = _smoothVerts.positions[index];
				outNormal = summedNormal.normalized;
				outColor = (Color)c;
			}
		};
		
		public static TableStorage tableStorage;		
	}
};

