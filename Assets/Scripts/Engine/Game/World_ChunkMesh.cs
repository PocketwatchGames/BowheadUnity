// Copyright (c) 2018 Pocketwatch Games LLC.

#define BOUNDS_CHECK
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

#if SURFACE_NETS
using static World.ChunkMeshGen.SurfaceNets;
#elif MARCHING_CUBES
using static World.ChunkMeshGen.MarchingCubes;
#else
using static World.ChunkMeshGen.EdgeCollapse;
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

	public static void BoundsCheckAndThrow(int i, int min, int max) {
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
		public struct Int3_t {
			public int x, y, z;
		};

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
			public ConstIntArray1D_t sn_cubeEdges;
			public ConstIntArray1D_t sn_edgeTable;
			public ConstIntArray2D_t voxelVerts;
			public ConstIntArray2D_t voxelFaces;
			public ConstIntArray2D_t voxelFaceNormal;
			public ConstIntArray2D_t collapseMap;
			public ConstIntArray2D_t spanningAxis;
			public ConstUIntArray1D_t blockSmoothingGroups;
			public ConstColor32Array1D_t blockColors;
			public ConstFloatArray1D_t blockSmoothingFactors;
			public ConstVoxelBlockContentsArray1D_t blockContents;

			int[] _sn_cubeEdges;
			int[] _sn_edgeTable;
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
			GCHandle _sn_pinnedCubeEdges;
			GCHandle _sn_pinnedEdgeTable;

			public static TableStorage New() {
				var t = new TableStorage();
				t.Init();
				return t;
			}

			public Color32[] blockColorsArray => _blockColors;

			void Init() {

				_sn_cubeEdges = new int[24];
				
				{
					int k = 0;
					for (int i = 0; i < 8; ++i) {
						for (int j = 1; j <= 4; j <<= 1) {
							var spanVert = i^j;
							if (i <= spanVert) {
								_sn_cubeEdges[k++] = i;
								_sn_cubeEdges[k++] = spanVert;
							}
						}
					}
				}

				_sn_pinnedCubeEdges = GCHandle.Alloc(_sn_cubeEdges, GCHandleType.Pinned);

				unsafe {
					sn_cubeEdges = ConstIntArray1D_t.New((int*)_sn_pinnedCubeEdges.AddrOfPinnedObject().ToPointer(), _sn_cubeEdges.Length);
				}

				// make an edge map, each entry is a bitfield containing, a bit set means the vertex is inside the manifold, otherwise outside.
				_sn_edgeTable = new int[256];
				for (int i = 0; i < 256; ++i) {
					var mask = 0;
					// i encodes the edge-bit field, 
					for (int k = 0; k < 24; k += 2) {
						var v0 = _sn_cubeEdges[k];
						var v1 = _sn_cubeEdges[k+1];
						var v0in = (i & (1 << v0)) != 0;
						var v1in = (i & (1 << v1)) != 0;
						if (v0in != v1in) {
							mask |= (1 << (k / 2));
						}
					}
					_sn_edgeTable[i] = mask;
				}

				_sn_pinnedEdgeTable = GCHandle.Alloc(_sn_edgeTable, GCHandleType.Pinned);

				unsafe {
					sn_edgeTable = ConstIntArray1D_t.New((int*)_sn_pinnedEdgeTable.AddrOfPinnedObject().ToPointer(), _sn_edgeTable.Length);
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
				_sn_pinnedCubeEdges.Free();
				_sn_pinnedEdgeTable.Free();
			}
		};

		struct Tables {
			public ConstIntArray1D_t sn_cubeEdges;
			public ConstIntArray1D_t sn_edgeTable;
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
					sn_cubeEdges = storage.sn_cubeEdges,
					sn_edgeTable = storage.sn_edgeTable,
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

		public static TableStorage tableStorage;		
	}
};

