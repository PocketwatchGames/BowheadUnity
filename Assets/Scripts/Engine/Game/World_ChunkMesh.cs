﻿// Copyright (c) 2018 Pocketwatch Games LLC.

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

public partial class World {
	public struct PinnedChunkData_t {
		public ChunkStorageArray1D_t voxeldata;
		public DecorationStorageArray1D_t decorations;
		[NativeDisableUnsafePtrRestriction]
		public unsafe EChunkFlags* pinnedFlags;
		[NativeDisableUnsafePtrRestriction]
		public unsafe int* pinnedDecorationCount;
		public EChunkFlags flags;
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
		const int BANK_SIZE = 16;
		
		const int BORDER_SIZE = 2;
		const int NUM_VOXELS_XZ = VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE*2;
		const int NUM_VOXELS_Y = VOXEL_CHUNK_SIZE_Y + BORDER_SIZE*2;
		const int MAX_VIS_VOXELS = NUM_VOXELS_XZ * NUM_VOXELS_XZ * NUM_VOXELS_Y;

		public struct ChunkData_t {
			public ChunkStorageArray1D_t pinnedBlockData;
			public DecorationStorageArray1D_t pinnedDecorationData;
			public unsafe int* pinnedDecorationCount;
			public unsafe EChunkFlags* pinnedFlags;
			public Voxel_t[] voxeldata;
			public EChunkFlags[] flags;
			public Decoration_t[] decorations;
			public int[] decorationCount;

			GCHandle _pinnedBlockData;
			GCHandle _pinnedDecorationData;
			GCHandle _pinnedDecorationCount;
			GCHandle _pinnedFlags;
			int _pinCount;

			public static ChunkData_t New() {
				return new ChunkData_t {
					voxeldata = new Voxel_t[VOXELS_PER_CHUNK],
					decorations = new Decoration_t[Decoration_t.MAX_DECORATIONS_PER_CHUNK],
					decorationCount = new int[1],
					flags = new EChunkFlags[1]
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
					unsafe {
						pinnedBlockData = ChunkStorageArray1D_t.New((Voxel_t*)_pinnedBlockData.AddrOfPinnedObject().ToPointer(), voxeldata.Length);
						pinnedDecorationData = DecorationStorageArray1D_t.New((Decoration_t*)_pinnedDecorationData.AddrOfPinnedObject().ToPointer(), decorations.Length);
						pinnedDecorationCount = (int*)_pinnedDecorationCount.AddrOfPinnedObject().ToPointer();
						pinnedFlags = (EChunkFlags*)_pinnedFlags.AddrOfPinnedObject().ToPointer();
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

					pinnedBlockData = default(ChunkStorageArray1D_t);
					pinnedDecorationData = default(DecorationStorageArray1D_t);

					unsafe {
						pinnedDecorationCount = null;
						pinnedFlags = null;
					}
				}
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
					decorationCount = chunk.decorationCount[0],
					flags = chunk.flags[0],
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

		public struct ConstIntArrayRowEnumerator_t : IEnumerator<int> {
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

		public unsafe struct ConstIntArrayRow_t : IEnumerable<int> {
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

		public struct ConstIntArray2DEnumerator_t : IEnumerator<ConstIntArrayRow_t> {
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

		public unsafe struct ConstIntArray2D_t : IEnumerable<ConstIntArrayRow_t> {
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

		public class TableStorage : IDisposable {
			public ConstIntArray2D_t voxelVerts;
			public ConstIntArray2D_t voxelFaces;
			public ConstIntArray2D_t voxelFaceNormal;
			public ConstIntArray2D_t collapseMap;
			public ConstIntArray2D_t spanningAxis;
			public ConstUIntArray1D_t blockSmoothingGroups;
			public ConstColor32Array1D_t blockColors;
			public ConstFloatArray1D_t blockSmoothingFactors;
			public ConstVoxelBlockContentsArray1D_t blockContents;

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
					0, // BLOCK_TYPE_DIRT -> blends with rock and grass
					0, // BLOCK_TYPE_GRASS
					BLOCK_SMG_WATER | BLOCK_BLEND_COLORS, // BLOCK_TYPE_WATER
					0, // BLOCK_TYPE_SAND
					0, // BLOCK_TYPE_SNOW
					0, // BLOCK_TYPE_ROCK
					0, // BLOCK_TYPE_ICE
					0, // BLOCK_TYPE_WOOD
					0, // BLOCK_TYPE_LEAVES
					0, // BLOCK_TYPE_NEEDLES
					0, // BLOCK_TYPE_FLOWERS1
					0, // BLOCK_TYPE_FLOWERS2
					0, // BLOCK_TYPE_FLOWERS3
					0 // BLOCK_TYPE_FLOWERS4
				};

				_pinnedBlockSmoothingGroups = GCHandle.Alloc(_blockSmoothingGroups, GCHandleType.Pinned);

				unsafe {
					blockSmoothingGroups = ConstUIntArray1D_t.New((uint*)_pinnedBlockSmoothingGroups.AddrOfPinnedObject().ToPointer(), (int)EVoxelBlockType.NumBlockTypes - 1);
				}

				// 1f = most smooth, 0 = very faceted

				_blockSmoothingFactors = new float[(int)EVoxelBlockType.NumBlockTypes - 1] {
					0.8f, // BLOCK_TYPE_DIRT
					0.8f, // BLOCK_TYPE_GRASS
					0.8f, // BLOCK_TYPE_WATER
					0.8f, // BLOCK_TYPE_SAND
					0.8f, // BLOCK_TYPE_SNOW
					0.35f, // BLOCK_TYPE_ROCK
					0.35f, // BLOCK_TYPE_ICE
					0.5f, // BLOCK_TYPE_WOOD
					0.8f, // BLOCK_TYPE_LEAVES
					0.8f, // BLOCK_TYPE_NEEDLES
					0.8f, // BLOCK_TYPE_FLOWERS1
					0.8f, // BLOCK_TYPE_FLOWERS2
					0.8f, // BLOCK_TYPE_FLOWERS3
					0.8f  // BLOCK_TYPE_FLOWERS4
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

		public struct FinalMeshVerts_t : IDisposable {

			[WriteOnly]
			public NativeArray<Vector3> positions;
			public NativeArray<Vector3> normals;
			public NativeArray<Color32> colors;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts;
			[WriteOnly]
			public NativeArray<int> submeshes;
			
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
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(3*MAX_CHUNK_LAYERS),
					submeshes = AllocatePersistentNoInit<int>(MAX_CHUNK_SUBMESHES*MAX_CHUNK_LAYERS),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES*BANK_SIZE),
					_vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				normals.Dispose();
				colors.Dispose();
				indices.Dispose();
				counts.Dispose();
				submeshes.Dispose();
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

			public void EmitVert(int x, int y, int z, Vector3 normal, Color32 color) {
				int INDEX = (y*(VOXEL_CHUNK_SIZE_XZ+1)*(VOXEL_CHUNK_SIZE_XZ+1)) + (z*(VOXEL_CHUNK_SIZE_XZ+1)) + x;

				// existing vertex?
				int vtoiCount = _vtoiCounts[INDEX];
				for (int i = 0; i < vtoiCount; ++i) {
					int idx = _vtoi[(INDEX*BANK_SIZE)+i];
					if (ColorEqual(colors[idx], color) && (normals[idx] == normal)) {
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
			public NativeArray<int> submeshes;
			[ReadOnly]
			public NativeArray<int> layers;
			[ReadOnly]
			public NativeArray<int> indices;
			[ReadOnly]
			public NativeArray<int> counts;
			[ReadOnly]
			public NativeArray<int> vtoiCounts;

			public static SmoothingVertsIn_t New(SmoothingVertsOut_t smv) {
				return new SmoothingVertsIn_t {
					positions = smv.positions,
					normals = smv.normals,
					colors = smv.colors,
					smoothFactor = smv.smoothFactor,
					smgs = smv.smgs,
					submeshes = smv.submeshes,
					layers = smv.layers,
					indices = smv.indices,
					counts = smv.counts,
					vtoiCounts = smv.vtoiCounts
				};
			}
		};

		public struct SmoothingVertsOut_t : IDisposable {
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
			public NativeArray<int> submeshes;
			[WriteOnly]
			public NativeArray<int> layers;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts;
			public NativeArray<int> vtoiCounts;

			int _maxSubmesh;
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
					submeshes = AllocatePersistentNoInit<int>(ushort.MaxValue*BANK_SIZE),
					layers = AllocatePersistentNoInit<int>(ushort.MaxValue*BANK_SIZE),
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(3),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES),
					vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				normals.Dispose();
				colors.Dispose();
				smoothFactor.Dispose();
				smgs.Dispose();
				submeshes.Dispose();
				layers.Dispose();
				indices.Dispose();
				counts.Dispose();
				_vtoi.Dispose();
				vtoiCounts.Dispose();
			}

			public void Init() {
				_vertCount = 0;
				_indexCount = 0;
				_maxSubmesh = 0;
				_maxLayer = 0;
				for (int i = 0; i < _vtoi.Length; ++i) {
					_vtoi[i] = 0;
				}
			}

			public void Finish() {
				counts[0] = _indexCount;
				counts[1] = _maxLayer;
				counts[2] = _maxSubmesh;
			}

			int EmitVert(int x, int y, int z, uint smg, float smoothingFactor, Color32 color, Vector3 normal, int submesh, int layer) {
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
				submeshes[(idx*BANK_SIZE) + count] = submesh;
				layers[(idx*BANK_SIZE) + count] = layer;

				vtoiCounts[idx] = count + 1;
				_maxSubmesh = (submesh > _maxSubmesh) ? submesh : _maxSubmesh;
				_maxLayer = (layer > _maxLayer) ? layer : _maxLayer;

				return idx | (count << 24);
			}

			public void EmitTri(int x0, int y0, int z0, int x1, int y1, int z1, int x2, int y2, int z2, uint smg, float smoothFactor, Color32 color, int submesh, int layer, bool isBorderVoxel) {
				var n = GetNormalAndAngles((float)x0, (float)y0, (float)z0, (float)x1, (float)y1, (float)z1, (float)x2, (float)y2, (float)z2);
				if (isBorderVoxel) {
					if ((x0 >= 0) && (x0 <= VOXEL_CHUNK_SIZE_XZ) && (y0 >= 0) && (y0 <= VOXEL_CHUNK_SIZE_Y) && (z0 >= 0) && (z0 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x0, y0, z0, smg, smoothFactor, color, n, submesh, layer);
					}
					if ((x1 >= 0) && (x1 <= VOXEL_CHUNK_SIZE_XZ) && (y1 >= 0) && (y1 <= VOXEL_CHUNK_SIZE_Y) && (z1 >= 0) && (z1 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x1, y1, z1, smg, smoothFactor, color, n, submesh, layer);
					}
					if ((x2 >= 0) && (x2 <= VOXEL_CHUNK_SIZE_XZ) && (y2 >= 0) && (y2 <= VOXEL_CHUNK_SIZE_Y) && (z2 >= 0) && (z2 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x2, y2, z2, smg, smoothFactor, color, n, submesh, layer);
					}
				} else {
					indices[_indexCount++] = EmitVert(x0, y0, z0, smg, smoothFactor, color, n, submesh, layer);
					indices[_indexCount++] = EmitVert(x1, y1, z1, smg, smoothFactor, color, n, submesh, layer);
					indices[_indexCount++] = EmitVert(x2, y2, z2, smg, smoothFactor, color, n, submesh, layer);
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

		struct GenerateFinalVertices_t : IJob {
			SmoothingVertsIn_t smoothVerts;
			FinalMeshVerts_t finalVerts;

			public static GenerateFinalVertices_t New(SmoothingVertsIn_t inVerts, FinalMeshVerts_t outVerts) {
				return new GenerateFinalVertices_t {
					smoothVerts = inVerts,
					finalVerts = outVerts
				};
			}

			public void Execute() {
				finalVerts.Init();

				var numIndices = smoothVerts.counts[0];
				var maxLayer = smoothVerts.counts[1];
				var maxSubmesh = smoothVerts.counts[2];

				for (int layer = 0; layer <= maxLayer; ++layer) {
					int maxLayerSubmesh = 0;
					finalVerts.BeginLayer(layer);

					for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
						var numSubmeshVerts = 0;

						for (int i = 0; i < numIndices; ++i) {
							Int3_t p;
							Vector3 n;
							Color32 c;

							if (BlendVertex(layer, submesh, smoothVerts.indices[i], out p, out n, out c)) {
								finalVerts.EmitVert(p.x, p.y, p.z, n, c);
								++numSubmeshVerts;
								maxLayerSubmesh = (submesh > maxLayerSubmesh) ? submesh : maxLayerSubmesh;
							}
						}

						finalVerts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh] = numSubmeshVerts;
					}

					finalVerts.FinishLayer(maxLayerSubmesh);
				}
			}

			bool BlendVertex(int layer, int submesh, int packedIndex, out Int3_t outPos, out Vector3 outNormal, out Color32 outColor) {
				int index = packedIndex & (0x00ffffff);
				int ofs = packedIndex >> 24;

				if (smoothVerts.layers[(index*BANK_SIZE) + ofs] != layer) {
					outPos = default(Int3_t);
					outNormal = default(Vector3);
					outColor = default(Color32);
					return false;
				}

				if (smoothVerts.submeshes[(index*BANK_SIZE) + ofs] != submesh) {
					outPos = default(Int3_t);
					outNormal = default(Vector3);
					outColor = default(Color32);
					return false;
				}

				var originalNormal = smoothVerts.normals[(index*BANK_SIZE) + ofs];
				var summedNormal = originalNormal;

				Vector4 c = (Color)smoothVerts.colors[(index*BANK_SIZE) + ofs];
				var smg = smoothVerts.smgs[(index*BANK_SIZE) + ofs];

				float factor = 1.1f - smoothVerts.smoothFactor[(index*BANK_SIZE)+ofs];

				float w = 1f;

				if (smg != 0) {
					var num = smoothVerts.vtoiCounts[index];
					for (int i = 0; i < num; ++i) {
						if (i != ofs) {
							if ((smoothVerts.smgs[(index*BANK_SIZE) + i] & smg & ~BLOCK_BLEND_COLORS) != 0) {
								if ((smoothVerts.smgs[(index*BANK_SIZE) + i] & smg & BLOCK_BLEND_COLORS) != 0) {
									c += (Vector4)(Color)smoothVerts.colors[(index*BANK_SIZE) + i];
									w += 1f;
								}

								var checkNormal = summedNormal + smoothVerts.normals[(index*BANK_SIZE) + i];
								var nml = checkNormal.normalized;
								var dot = Vector3.Dot(nml, originalNormal);

								if (dot >= factor) {
									summedNormal = checkNormal;
								}
							}
						}
					}

					c /= w;
				}

				outPos = smoothVerts.positions[index];
				outNormal = summedNormal.normalized;
				outColor = (Color)c;
				return true;
			}
		};

		public unsafe struct BlendedVoxel_t {
			public fixed int vertexFlags[8];
			public fixed int neighbors[6];
			public bool touched;
		};

		public unsafe struct VoxelArray1D {
			[NativeDisableUnsafePtrRestriction]
			BlendedVoxel_t* _arr;
			int _x;

			public static VoxelArray1D New(BlendedVoxel_t* array, int x) {
				return new VoxelArray1D {
					_arr = array,
					_x = x
				};
			}

			public BlendedVoxel_t* this[int i] {
				get {
					Assert(i < _x);
					return &_arr[i];
				}
			}

			public int length {
				get {
					return _x;
				}
			}
		};

		public struct VoxelStorage_t {
			public const int NUM_VOXELS = MAX_VIS_VOXELS;

			public VoxelArray1D voxels;

			BlendedVoxel_t[] _voxels;
			GCHandle _pinnedVoxels;

			public static VoxelStorage_t New() {
				return new VoxelStorage_t {
					_voxels = new BlendedVoxel_t[NUM_VOXELS]
				};
			}

			public void Pin() {
				Assert(!_pinnedVoxels.IsAllocated);
				_pinnedVoxels = GCHandle.Alloc(_voxels, GCHandleType.Pinned);
				unsafe {
					voxels = VoxelArray1D.New((BlendedVoxel_t*)_pinnedVoxels.AddrOfPinnedObject().ToPointer(), _voxels.Length);
				}
			}

			public void Unpin() {
				Assert(_pinnedVoxels.IsAllocated);
				voxels = new VoxelArray1D();
				_pinnedVoxels.Free();
			}
		};

		unsafe struct VoxelNeighbors_t {
			fixed byte _arr[6];

			public EVoxelBlockType this[int i] {
				get {
					Assert((i >= 0) && (i < 6));
					fixed (byte* p = _arr) {
						return (EVoxelBlockType)p[i];
					}
				}
				set {
					Assert((i >= 0) && (i < 6));
					fixed (byte* p = _arr) {
						p[i] = (byte)value;
					}
				}
			}
		};

		unsafe struct VoxelNeighborContents_t {
			fixed byte _arr[6];

			public EVoxelBlockContents this[int i] {
				get {
					Assert((i >= 0) && (i < 6));
					fixed (byte* p = _arr) {
						return (EVoxelBlockContents)p[i];
					}
				}
				set {
					Assert((i >= 0) && (i < 6));
					fixed (byte* p = _arr) {
						p[i] = (byte)value;
					}
				}
			}
		};

		unsafe struct GenerateChunkVerts_t : IJob {
			SmoothingVertsOut_t _smoothVerts;
			VoxelArray1D _voxels;
			Tables _tables;
			NativeArray<PinnedChunkData_t> _area;

			VoxelNeighbors_t _vn;
			VoxelNeighborContents_t _vnc;

			int _numVoxels;
			int _numTouched;

			public static GenerateChunkVerts_t New(SmoothingVertsOut_t smoothVerts, VoxelArray1D voxels, NativeArray<PinnedChunkData_t> area, TableStorage tableStorage) {
				return new GenerateChunkVerts_t {
					_smoothVerts = smoothVerts,
					_voxels = voxels,
					_tables = Tables.New(tableStorage),
					_area = area,
					_vn = new VoxelNeighbors_t()
				};
			}

			/*
			=======================================
			AddVoxel

			A normal voxel has 0-6 visible outward faces. Faces are visible if the neighboring block is an air block. Any visible faces is
			a candidate for collapsing along the two orthogonal facing directions. Since it's ambiguous which axis to collapse, they are ordered
			by priority: Z,Y,X. The direction along the collapse axis is also ambiguous so we decide to collapse from (+) -> (-) first. Because
			a face may have vertices that collapse in both directions we only collapse from (-) -> (+) if the opposite vertex doesn't also collapse
			(otherwise this would cause a crossing fold).

			Takes each visible face and mark all vertices on the faces as collapsable along the two orthogonal axes of the face if the face on the orthogonal 
			axis opposite the collapse direction is visible.
			=======================================
			*/

			void AddVoxel(int x, int y, int z, Voxel_t voxel) {
				var blendVoxel = _voxels[_numVoxels++];

				ZeroInts(blendVoxel->vertexFlags, 8);
				ZeroInts(blendVoxel->neighbors, 6);
				
				blendVoxel->touched = true;

				if ((voxel.flags & EVoxelBlockFlags.FullVoxel) != 0) {
					return;
				}

				var faceCounts = stackalloc int[8];
				ZeroInts(faceCounts, 8);

				var contents = _tables.blockContents[(int)voxel.type];

				for (int i = 0; i < 6; ++i) {
					if (_vnc[i] == EVoxelBlockContents.None) {
						foreach (var vi in _tables.voxelFaces[i]) {
							++faceCounts[vi];
						}
					}
				}

				for (int i = 0; i < 6; ++i) {
					if ((_vnc[i] == EVoxelBlockContents.None) && (_vnc[i ^ 1] >= contents)) { // we can only collapse faces whose opposite face is hidden.

						int numOver2 = 0;

						foreach (var vi in _tables.voxelFaces[i]) {
							if (faceCounts[vi] > 2) {
								++numOver2;
							}
						}

						if (numOver2 < 4) {
							// don't collapse a face that is exposed by air on all sides.

							// this face can potentially be collapsed, assuming it won't expose
							// a non-visible face on the voxel.
							// NOTE: the collapse may expose a non-visible face on a neighboring voxel
							// but that case is handled by the voxel emitting the faces.

							int axis = i / 2;
							var spanning = _tables.spanningAxis[axis];

							foreach (var vi in _tables.voxelFaces[i]) {
								foreach (var spaxis in spanning) {
									var signbit = 1 << spaxis;
									var side = (vi & signbit) != 0 ? 1 : 0;
									// we can collapse in this direction if the face on the vertex-side of the spanning axis is visible.
									if (_vnc[(spaxis * 2) + (side ^ 1)] == EVoxelBlockContents.None) {
										blendVoxel->vertexFlags[vi] |= signbit;
									}
								}
							}
						}
					}
				}
			}

			int GetVoxelVert(BlendedVoxel_t* voxel, int vi) {
				for (int i = 0; i < 3; ++i) {
					var axis = (i == 0) ? 1 : (i == 1) ? 2 : 0;
					var signbit = 1 << axis;
					var flags = voxel->vertexFlags[vi];

					if ((flags&signbit) != 0) {
						// we want to collapse on this axis
						// check predicence: we go from (-)->(+), the only way to go from (+)->(-)
						// is if there is no (-)->(+)
						var other = vi ^ signbit;
						if (vi > other) {
							if ((voxel->vertexFlags[other] & signbit) != 0) {
								continue; // other side will fold onto us.
							}
						}

						vi = _tables.collapseMap[vi][axis];
					}
				}

				return vi;
			}

			/*
			=======================================
			MatchNeighbors

			Neighboring blocks with shifted vertices may expose new faces which in turn may have sort of a "third wheel" vertex which sticks out.
			If possible we should try and adjust to our neighbors blending.
			=======================================
			*/

			void MatchNeighbors(int index, int x, int y, int z, Voxel_t voxel) {
				var smoothVoxel = _voxels[index];

				if (!smoothVoxel->touched) {
					return;
				}

				smoothVoxel->touched = false;
				var contents = _tables.blockContents[(int)voxel.type];

				for (int i = 0; i < 6; ++i) {
					if (_vnc[i] != EVoxelBlockContents.None) {
						// flag any neighboring voxels if they have vertex shifts.

						var axis = i / 2;
						var axisbit = 1 << axis;

						var nx = x + _tables.voxelFaceNormal[i][0];
						var ny = y + _tables.voxelFaceNormal[i][1];
						var nz = z + _tables.voxelFaceNormal[i][2];

						var ni = nx + (nz*NUM_VOXELS_XZ) + (ny*NUM_VOXELS_XZ*NUM_VOXELS_XZ);
						var neighborVoxel = _voxels[ni];

						for (int n = 0; n < 4; ++n) {
							var vi = _tables.voxelFaces[i][n];
							var mirrorVert = vi ^ axisbit;

							if (neighborVoxel->vertexFlags[mirrorVert] != 0) {
								smoothVoxel->neighbors[i] = 1;
								break;
							}
						}
					} else {
						smoothVoxel->neighbors[i] = -1;
					}
				}

				if ((voxel.flags & EVoxelBlockFlags.FullVoxel) != 0) {
					return;
				}

				// flag vertices uncovered by neighbor vertex shifts to be collapsed as well if possible.
				var faceCounts = stackalloc int[8];
				ZeroInts(faceCounts, 8);

				for (int i = 0; i < 6; ++i) {
					if ((_vnc[i] == EVoxelBlockContents.None) || (smoothVoxel->neighbors[i] != 0)) {
						foreach (var vi in _tables.voxelFaces[i]) {
							++faceCounts[vi];
						}
					}
				}

				var vflags = stackalloc int[8];
				ZeroInts(vflags, 8);
				var vmask = stackalloc int[8];
				ZeroInts(vmask, 8);

				for (int i = 0; i < 6; ++i) {
					if (smoothVoxel->neighbors[i] == 1) {

						var axis = i / 2;
						var axisbit = 1 << axis;

						var nx = x + _tables.voxelFaceNormal[i][0];
						var ny = y + _tables.voxelFaceNormal[i][1];
						var nz = z + _tables.voxelFaceNormal[i][2];

						var ni = nx + (nz*NUM_VOXELS_XZ) + (ny*NUM_VOXELS_XZ*NUM_VOXELS_XZ);
						var neighborVoxel = _voxels[ni];

						var spanning = _tables.spanningAxis[axis];

						foreach (var vi in _tables.voxelFaces[i]) {
							if (faceCounts[vi] > 2) {

								var mirrorVert = vi ^ axisbit;

								foreach (var spaxis in spanning) {
									var signbit = 1 << spaxis;
									var side = (vi & signbit) != 0 ? 1 : 0;
									// we can collapse in this direction if the face on the vertex-side of the spanning axis is visible.
									bool set = false;
									bool exposed = true;

									if (_vnc[(spaxis * 2) + (side ^ 1)] != EVoxelBlockContents.None) {
										exposed = false;

										// a vertex collapse may have exposed this vertex...

										var spanningNeighborFace = (spaxis * 2) + (side ^ 1);
										var snx = x + _tables.voxelFaceNormal[spanningNeighborFace][0];
										var sny = y + _tables.voxelFaceNormal[spanningNeighborFace][1];
										var snz = z + _tables.voxelFaceNormal[spanningNeighborFace][2];
										var sni = snx + (snz*NUM_VOXELS_XZ) + (sny*NUM_VOXELS_XZ*NUM_VOXELS_XZ);
										var spanningNeighborVoxel = _voxels[sni];

										var boundMirrorVert = vi ^ signbit;

										for (int checkAxisNum = 0; checkAxisNum < 3; ++checkAxisNum) {
											var axisCheck = (checkAxisNum == 0) ? 1 : (checkAxisNum == 1) ? 2 : 0;
											if (axisCheck != spaxis) {
												var checkAxisBit = 1 << axisCheck;
												var boundMirrorAxisVert = boundMirrorVert ^ checkAxisBit;

												// check the (-)->(+) collapse order to get the right vertex
												if (boundMirrorAxisVert < boundMirrorVert) {
													if ((spanningNeighborVoxel->vertexFlags[boundMirrorAxisVert] & checkAxisBit) != 0) {
														continue;
													}
												}

												if ((spanningNeighborVoxel->vertexFlags[boundMirrorVert] & checkAxisBit) != 0) {
													exposed = true;
													break;
												}
											}
										}
									}

									if (exposed) {
										for (int checkAxisNum = 0; checkAxisNum < 3; ++checkAxisNum) {
											var checkAxis = (checkAxisNum == 0) ? 1 : (checkAxisNum == 1) ? 2 : 0;
											if (checkAxis != axis) {
												var checkAxisBit = 1 << checkAxis;
												var mirrorAxisVert = mirrorVert ^ checkAxisBit;

												// check the (-)->(+) collapse order to get the right vertex
												if (mirrorAxisVert < mirrorVert) {
													if ((neighborVoxel->vertexFlags[mirrorAxisVert] & checkAxisBit) != 0) {
														continue;
													}
												}

												if ((neighborVoxel->vertexFlags[mirrorVert] & checkAxisBit) != 0) {
													// this vertex is uncovered.
													vflags[vi] |= signbit;
													set = true;
													break;
												}
											}
										}
									}

									if (!set) {
										vmask[vi] |= signbit;
									}
								}
							}
						}
					}
				}

				for (int i = 0; i < 8; ++i) {
					// mark neighboring voxels as dirty.
					var changed = ~smoothVoxel->vertexFlags[i] & (vflags[i] & ~vmask[i]);

					if (changed != 0) {
						for (int axis = 0; axis < 3; ++axis) {
							var axisbit = 1 << axis;
							if ((changed & axisbit) != 0) {
								var side = (i & axisbit) != 0 ? 1 : 0;
								var spanningNeighborFace = (axis * 2) + (side ^ 1);
								var snx = x + _tables.voxelFaceNormal[spanningNeighborFace][0];
								var sny = y + _tables.voxelFaceNormal[spanningNeighborFace][1];
								var snz = z + _tables.voxelFaceNormal[spanningNeighborFace][2];
								var sni = snx + (snz*NUM_VOXELS_XZ) + (sny*NUM_VOXELS_XZ*NUM_VOXELS_XZ);
								var spanningNeighborVoxel = _voxels[sni];
								spanningNeighborVoxel->touched = true;
								++_numTouched;
							}
						}
					}

					smoothVoxel->vertexFlags[i] |= vflags[i] & ~vmask[i];
				}
			}

			bool CheckVertexUncovered(int v0, int v1, int v2, BlendedVoxel_t* neighbor) {

				bool v0vis = true;

				for (int axiscount = 0; axiscount < 3; ++axiscount) {
					var axis = (axiscount == 0) ? 1 : (axiscount == 1) ? 2 : 0;

					// a covering face must be reflected about the plane of the face.

					var axisbit = 1 << axis;

					var v0side = v0 & axisbit;
					var v1side = v1 & axisbit;
					var v2side = v2 & axisbit;

					// winding is on this plane?
					if ((v0side != v1side) || (v0side != v2side) || (v1side != v2side)) {
						continue; // can't reflect about this axis.
					}

					var side = v0side == 0;

					for (int i = 0; i< 6; ++i) {
						var nv0 = GetVoxelVert(neighbor, _tables.voxelFaces[i][0]);
						for (int z = 1; z <= 2; ++z) {
							var nv1 = GetVoxelVert(neighbor, _tables.voxelFaces[i][z]);
							var nv2 = GetVoxelVert(neighbor, _tables.voxelFaces[i][z + 1]);

							if ((nv0 != nv1) && (nv0 != nv2) && (nv1 != nv2)) {
								var nv0side = nv0 & axisbit;
								var nv1side = nv1 & axisbit;
								var nv2side = nv2 & axisbit;

								// winding is on this plane?
								if ((nv0side != nv1side) || (nv0side != nv2side) || (nv1side != nv2side)) {
									continue; // can't reflect about this axis.
								}

								// is this winding plane reflected?
								if ((nv0side == 0) == side) {
									continue;
								}

								var mv0 = nv0 ^ axisbit;
								var mv1 = nv1 ^ axisbit;
								var mv2 = nv2 ^ axisbit;

								// if any verts don't match then it's not covered.
								if ((v0 != mv0) && (v0 != mv1) && (v0 != mv2)) {
									continue;
								}

								v0vis = false;

								if ((v1 != mv0) && (v1 != mv1) && (v1 != mv2)) {
									continue;
								}
								if ((v2 != mv0) && (v2 != mv1) && (v2 != mv2)) {
									continue;
								}

								return false;
							}
						}
					}
				}

				return v0vis;
			}

			bool CheckFaceUncovered(int v0, int v1, int v2, BlendedVoxel_t* neighbor) {

				for (int axiscount = 0; axiscount < 3; ++axiscount) {
					var axis = (axiscount == 0) ? 1 : (axiscount == 1) ? 2 : 0;

					var axisbit = 1 << axis;

					var v0side = v0 & axisbit;
					var v1side = v1 & axisbit;
					var v2side = v2 & axisbit;

					// winding is on this plane?
					if ((v0side != v1side) || (v0side != v2side) || (v1side != v2side)) {
						continue; // can't reflect about this axis.
					}

					var side = v0side == 0;

					for (int i = 0; i < 6; ++i) {
						var nv0 = GetVoxelVert(neighbor, _tables.voxelFaces[i][0]);
						for (int z = 1; z <= 2; ++z) {
							var nv1 = GetVoxelVert(neighbor, _tables.voxelFaces[i][z]);
							var nv2 = GetVoxelVert(neighbor, _tables.voxelFaces[i][z + 1]);

							if ((nv0 != nv1) && (nv0 != nv2) && (nv1 != nv2)) {
								var nv0side = nv0 & axisbit;
								var nv1side = nv1 & axisbit;
								var nv2side = nv2 & axisbit;

								// winding is on this plane?
								if ((nv0side != nv1side) || (nv0side != nv2side) || (nv1side != nv2side)) {
									continue; // can't reflect about this axis.
								}

								// is this winding plane reflected?
								if ((nv0side == 0) == side) {
									continue;
								}

								var mv0 = nv0 ^ axisbit;
								var mv1 = nv1 ^ axisbit;
								var mv2 = nv2 ^ axisbit;

								// if any verts don't match then it's not covered.
								if ((v0 != mv0) && (v0 != mv1) && (v0 != mv2)) {
									continue;
								}
								if ((v1 != mv0) && (v1 != mv1) && (v1 != mv2)) {
									continue;
								}
								if ((v2 != mv0) && (v2 != mv1) && (v2 != mv2)) {
									continue;
								}

								return false;
							}
						}
					}
				}

				return true;
			}

			static int WrapVert(int i) {
				if (i < 0) {
					return 4 + i;
				}
				if (i > 3) {
					return i - 4;
				}
				return i;
			}

			void GetBlockColorAndSmoothing(EVoxelBlockType blocktype, out Color32 color, out uint smg, out float smoothing, out int submesh, out int layer) {
				color = _tables.blockColors[(int)blocktype - 1];
				smg = _tables.blockSmoothingGroups[(int)blocktype - 1];
				smoothing = _tables.blockSmoothingFactors[(int)blocktype - 1];
				submesh = 0;

				if (blocktype == EVoxelBlockType.Water) {
					layer = EChunkLayers.Water.ToIndex();
				} else if ((blocktype == EVoxelBlockType.Leaves) || (blocktype == EVoxelBlockType.Needles) || (blocktype == EVoxelBlockType.Wood)) {
					layer = EChunkLayers.Trees.ToIndex();
				} else {
					layer = EChunkLayers.Terrain.ToIndex();
				}
			}

			void EmitVoxelFaces(int index, int x, int y, int z, EVoxelBlockType blocktype, bool isBorderVoxel) {
				var voxel = _voxels[index];
				var contents = _tables.blockContents[(int)blocktype];

				// emit cube tris that are not degenerate from collapse
				for (int i = 0; i < 6; ++i) {
					if (_vnc[i] < contents) {
						Color32 color;
						uint smg;
						float factor;
						int submesh;
						int layer;

						GetBlockColorAndSmoothing(blocktype, out color, out smg, out factor, out submesh, out layer);

						var v0 = GetVoxelVert(voxel, _tables.voxelFaces[i][0]);

						for (int k = 1; k <= 2; ++k) {
							var v1 = GetVoxelVert(voxel, _tables.voxelFaces[i][k]);
							var v2 = GetVoxelVert(voxel, _tables.voxelFaces[i][k + 1]);

							if ((v0 != v1) && (v0 != v2) && (v1 != v2)) {
								_smoothVerts.EmitTri(
									x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
									x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
									x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
									smg, factor, color, submesh, layer, isBorderVoxel
								);
							}

						}
					} else if (voxel->neighbors[i] == 1) {
						// hole-filling:
						// a neighboring voxel on this side may have collapsed a vertex exposing a triangle on this face.

						var axis = i / 2;
						var axisbit = 1 << axis;

						var nx = x + _tables.voxelFaceNormal[i][0];
						var ny = y + _tables.voxelFaceNormal[i][1];
						var nz = z + _tables.voxelFaceNormal[i][2];

						var ni = nx + (nz*NUM_VOXELS_XZ) + (ny*NUM_VOXELS_XZ*NUM_VOXELS_XZ);
						var neighborVoxel = _voxels[ni];

						// find the correct starting vertex.
						int v0base = 0;

						for (int s = 0; s < 4; ++s) {
							var v0 = GetVoxelVert(voxel, _tables.voxelFaces[i][s]);

							for (int k = 1; k <= 2; ++k) {
								var v1 = GetVoxelVert(voxel, _tables.voxelFaces[i][(k + s) % 4]);
								var v2 = GetVoxelVert(voxel, _tables.voxelFaces[i][(k + s + 1) % 4]);

								if ((v0 != v1) && (v0 != v2) && (v1 != v2)) {
									if (CheckVertexUncovered(v0, v1, v2, neighborVoxel)) {
										v0base = s;
										// an uncovered shifted vertex is the best to start.
										if ((i & 1) != 0) {
											if ((v0&axisbit) != 0) {
												goto found_start_vert;
											}
										} else {
											if ((v0&axisbit) == 0) {
												goto found_start_vert;
											}
										}
									}
								}
							}
						}

						found_start_vert:

						for (int k = 0; k < 2; ++k) {
							var v0 = GetVoxelVert(voxel, _tables.voxelFaces[i][WrapVert(v0base + (k * 2) - 1)]);
							var v1 = GetVoxelVert(voxel, _tables.voxelFaces[i][WrapVert(v0base + (k * 2))]);
							var v2 = GetVoxelVert(voxel, _tables.voxelFaces[i][WrapVert(v0base + (k * 2) + 1)]);

							if ((v0 != v1) && (v0 != v2) && (v1 != v2)) {
								if (CheckFaceUncovered(v0, v1, v2, neighborVoxel)) {
									Color32 color;
									uint smg;
									float factor;
									int submesh;
									int layer;

									GetBlockColorAndSmoothing(_vn[i], out color, out smg, out factor, out submesh, out layer);

									_smoothVerts.EmitTri(
										x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
										x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
										x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
										smg, factor, color, submesh, layer, isBorderVoxel
									);

									// emit backface?
									if (_vnc[i] > contents) {
										_smoothVerts.EmitTri(
											x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
											x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
											x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
											smg, factor, color, submesh, layer, isBorderVoxel
										);
									}
								}
							}
						}
					}
				}
			}

			public const int Z_PITCH = 3;
			public const int Y_PITCH = Z_PITCH * Z_PITCH;

			static int Wrap(int w, int max) {
				if (w < 0) {
					return max + w;
				}
				if (w >= max) {
					return w - max;
				}
				return w;
			}

			public void Execute() {

				_smoothVerts.Init();

				_numVoxels = 0;

				var chunk = _area[1 + Y_PITCH + Z_PITCH];
				chunk.flags = chunk.pinnedFlags[0];

				if ((chunk.flags & EChunkFlags.SOLID) == 0) {
					// no solid blocks in this chunk it can't have any visible faces.
					_smoothVerts.Finish();
					return;
				}

				for (int y = -BORDER_SIZE; y < VOXEL_CHUNK_SIZE_Y + BORDER_SIZE; ++y) {
					var ywrap = Wrap(y, VOXEL_CHUNK_SIZE_Y);
					var ymin = (ywrap == 0);
					var ymax = (ywrap == (VOXEL_CHUNK_SIZE_Y - 1));
					var yofs = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ*ywrap;

					var cy = (y < 0) ? 0 : (y <VOXEL_CHUNK_SIZE_Y) ? 1 : 2;

					for (int z = -BORDER_SIZE; z < VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE; ++z) {
						var zwrap = Wrap(z, VOXEL_CHUNK_SIZE_XZ);
						var zmin = (zwrap == 0);
						var zmax = (zwrap == (VOXEL_CHUNK_SIZE_XZ - 1));
						var zofs = VOXEL_CHUNK_SIZE_XZ * zwrap;

						var cz = (z < 0) ? 0 : (z < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

						for (int x = -BORDER_SIZE; x < VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE; ++x) {
							var xwrap = Wrap(x, VOXEL_CHUNK_SIZE_XZ);
							var xmin = (xwrap == 0);
							var xmax = (xwrap == (VOXEL_CHUNK_SIZE_XZ - 1));
							var xofs = xwrap;

							var cx = (x < 0) ? 0 : (x < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

							var chunkIndex = cx + (cy*Y_PITCH) + (cz*Z_PITCH);
							var POS_X = chunkIndex + 1;
							var NEG_X = chunkIndex - 1;
							var POS_Y = chunkIndex + Y_PITCH;
							var NEG_Y = chunkIndex - Y_PITCH;
							var POS_Z = chunkIndex + Z_PITCH;
							var NEG_Z = chunkIndex - Z_PITCH;

							var neighbor = _area[chunkIndex];

							if (neighbor.valid != 0) {
								var voxel = neighbor.voxeldata[zofs + yofs + xofs];
								if (_tables.blockContents[(int)voxel.type] == EVoxelBlockContents.None) {
									++_numVoxels;
									continue;
								}
#if NO_SMOOTHING
								voxel.flags |= EVoxelBlockFlags.FullVoxel;
#endif

								var blocktype = voxel.type;

								// avoid contents-change with neighbor blocks in unloaded-space
								_vn[0] = blocktype;
								_vn[1] = blocktype;
								_vn[2] = blocktype;
								_vn[3] = blocktype;
								_vn[4] = blocktype;
								_vn[5] = blocktype;

								if (xmin) {
									_vn[0] = neighbor.voxeldata[zofs + yofs + xofs + 1].type;
									if (_area[NEG_X].valid != 0) {
										_vn[1] = _area[NEG_X].voxeldata[zofs + yofs + VOXEL_CHUNK_SIZE_XZ - 1].type;
									}
								} else if (xmax) {
									if (_area[POS_X].valid != 0) {
										_vn[0] = _area[POS_X].voxeldata[zofs + yofs].type;
									}
									_vn[1] = neighbor.voxeldata[zofs + yofs + xofs - 1].type;
								} else {
									_vn[0] = neighbor.voxeldata[zofs + yofs + xofs + 1].type;
									_vn[1] = neighbor.voxeldata[zofs + yofs + xofs - 1].type;
								}

								if (ymin) {
									_vn[2] = neighbor.voxeldata[yofs+(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
									if (_area[NEG_Y].valid != 0) {
										_vn[3] = _area[NEG_Y].voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_Y - 1)) + zofs + xofs].type;
									}
								} else if (ymax) {
									if (_area[POS_Y].valid != 0) {
										_vn[2] = _area[POS_Y].voxeldata[zofs + xofs].type;
									}
									_vn[3] = neighbor.voxeldata[yofs-(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
								} else {
									_vn[2] = neighbor.voxeldata[yofs+(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
									_vn[3] = neighbor.voxeldata[yofs-(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
								}

								if (zmin) {
									_vn[4] = neighbor.voxeldata[yofs + (zofs + VOXEL_CHUNK_SIZE_XZ) + xofs].type;
									if (_area[NEG_Z].valid != 0) {
										_vn[5] = _area[NEG_Z].voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_XZ - 1)) + xofs].type;
									}
								} else if (zmax) {
									if (_area[POS_Z].valid != 0) {
										_vn[4] = _area[POS_Z].voxeldata[yofs + xofs].type;
									}
									_vn[5] = neighbor.voxeldata[yofs + (zofs-VOXEL_CHUNK_SIZE_XZ) + xofs].type;
								} else {
									_vn[4] = neighbor.voxeldata[yofs + (zofs+VOXEL_CHUNK_SIZE_XZ) + xofs].type;
									_vn[5] = neighbor.voxeldata[yofs + (zofs-VOXEL_CHUNK_SIZE_XZ) + xofs].type;
								}

								for (int i = 0; i < 6; ++i) {
									_vnc[i] = _tables.blockContents[(int)_vn[i]];
								}

								AddVoxel(x+BORDER_SIZE, y+BORDER_SIZE, z+BORDER_SIZE, voxel);
							} else {
								++_numVoxels;
							}
						}
					}
				}

				do {
					const int CENTER = 1 + Y_PITCH + Z_PITCH;
					const int POS_X = CENTER + 1;
					const int NEG_X = CENTER - 1;
					const int POS_Y = CENTER + Y_PITCH;
					const int NEG_Y = CENTER - Y_PITCH;
					const int POS_Z = CENTER + Z_PITCH;
					const int NEG_Z = CENTER - Z_PITCH;

					_numTouched = 0;

					for (int y = 0; y < VOXEL_CHUNK_SIZE_Y; ++y) {
						var ymin = (y == 0);
						var ymax = (y == (VOXEL_CHUNK_SIZE_Y - 1));
						var yofs = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ*y;

						for (int z = 0; z < VOXEL_CHUNK_SIZE_XZ; ++z) {
							var zmin = (z == 0);
							var zmax = (z == (VOXEL_CHUNK_SIZE_XZ - 1));
							var zofs = VOXEL_CHUNK_SIZE_XZ * z;

							for (int x = 0; x < VOXEL_CHUNK_SIZE_XZ; ++x) {
								var xmin = (x == 0);
								var xmax = (x == (VOXEL_CHUNK_SIZE_XZ - 1));

								var voxel = chunk.voxeldata[zofs + yofs + x];
								if (_tables.blockContents[(int)voxel.type] == EVoxelBlockContents.None) {
									continue;
								}
#if NO_SMOOTHING
								voxel.flags |= EVoxelBlockFlags.FullVoxel;
#endif

								var blocktype = voxel.type;

								// avoid contents-change with neighbor blocks in unloaded-space
								_vn[0] = blocktype;
								_vn[1] = blocktype;
								_vn[2] = blocktype;
								_vn[3] = blocktype;
								_vn[4] = blocktype;
								_vn[5] = blocktype;

								if (xmin) {
									_vn[0] = chunk.voxeldata[zofs + yofs + x + 1].type;
									if (_area[NEG_X].valid != 0) {
										_vn[1] = _area[NEG_X].voxeldata[zofs + yofs + VOXEL_CHUNK_SIZE_XZ - 1].type;
									}
								} else if (xmax) {
									if (_area[POS_X].valid != 0) {
										_vn[0] = _area[POS_X].voxeldata[zofs + yofs].type;
									}
									_vn[1] = chunk.voxeldata[zofs + yofs + x - 1].type;
								} else {
									_vn[0] = chunk.voxeldata[zofs + yofs + x + 1].type;
									_vn[1] = chunk.voxeldata[zofs + yofs + x - 1].type;
								}

								if (ymin) {
									_vn[2] = chunk.voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(y + 1)) + zofs + x].type;
									if (_area[NEG_Y].valid != 0) {
										_vn[3] = _area[NEG_Y].voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_Y - 1)) + zofs + x].type;
									}
								} else if (ymax) {
									if (_area[POS_Y].valid != 0) {
										_vn[2] = _area[POS_Y].voxeldata[zofs + x].type;
									}
									_vn[3] = chunk.voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(y - 1)) + zofs + x].type;
								} else {
									_vn[2] = chunk.voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(y + 1)) + zofs + x].type;
									_vn[3] = chunk.voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(y - 1)) + zofs + x].type;
								}

								if (zmin) {
									_vn[4] = chunk.voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(z + 1)) + x].type;
									if (_area[NEG_Z].valid != 0) {
										_vn[5] = _area[NEG_Z].voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_XZ - 1)) + x].type;
									}
								} else if (zmax) {
									if (_area[POS_Z].valid != 0) {
										_vn[4] = _area[POS_Z].voxeldata[yofs + x].type;
									}
									_vn[5] = chunk.voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(z - 1)) + x].type;
								} else {
									_vn[4] = chunk.voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(z + 1)) + x].type;
									_vn[5] = chunk.voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(z - 1)) + x].type;
								}

								for (int i = 0; i < 6; ++i) {
									_vnc[i] = _tables.blockContents[(int)_vn[i]];
								}

								MatchNeighbors((x + BORDER_SIZE) + ((z + BORDER_SIZE)*NUM_VOXELS_XZ) + ((y + BORDER_SIZE)*NUM_VOXELS_XZ*NUM_VOXELS_XZ), x + BORDER_SIZE, y + BORDER_SIZE, z + BORDER_SIZE, voxel);
							}
						}
					}
				} while (_numTouched > 0);
				

				for (int y = -BORDER_SIZE; y < VOXEL_CHUNK_SIZE_Y + BORDER_SIZE; ++y) {
					var ywrap = Wrap(y, VOXEL_CHUNK_SIZE_Y);
					var ymin = (ywrap == 0);
					var ymax = (ywrap == (VOXEL_CHUNK_SIZE_Y - 1));
					var yofs = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ*ywrap;

					var cy = (y < 0) ? 0 : (y <VOXEL_CHUNK_SIZE_Y) ? 1 : 2;

					for (int z = -BORDER_SIZE; z < VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE; ++z) {
						var zwrap = Wrap(z, VOXEL_CHUNK_SIZE_XZ);
						var zmin = (zwrap == 0);
						var zmax = (zwrap == (VOXEL_CHUNK_SIZE_XZ - 1));
						var zofs = VOXEL_CHUNK_SIZE_XZ * zwrap;

						var cz = (z < 0) ? 0 : (z < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

						for (int x = -BORDER_SIZE; x < VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE; ++x) {
							var xwrap = Wrap(x, VOXEL_CHUNK_SIZE_XZ);
							var xmin = (xwrap == 0);
							var xmax = (xwrap == (VOXEL_CHUNK_SIZE_XZ - 1));
							var xofs = xwrap;

							var cx = (x < 0) ? 0 : (x < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

							var chunkIndex = cx + (cy*Y_PITCH) + (cz*Z_PITCH);
							var POS_X = chunkIndex + 1;
							var NEG_X = chunkIndex - 1;
							var POS_Y = chunkIndex + Y_PITCH;
							var NEG_Y = chunkIndex - Y_PITCH;
							var POS_Z = chunkIndex + Z_PITCH;
							var NEG_Z = chunkIndex - Z_PITCH;

							var neighbor = _area[chunkIndex];

							if (neighbor.valid != 0) {
								var voxel = neighbor.voxeldata[zofs + yofs + xofs];
								if (_tables.blockContents[(int)voxel.type] == EVoxelBlockContents.None) {
									continue;
								}
#if NO_SMOOTHING
								voxel.flags |= EVoxelBlockFlags.FullVoxel;
#endif

								var blocktype = voxel.type;

								// avoid contents-change with neighbor blocks in unloaded-space
								_vn[0] = blocktype;
								_vn[1] = blocktype;
								_vn[2] = blocktype;
								_vn[3] = blocktype;
								_vn[4] = blocktype;
								_vn[5] = blocktype;

								if (xmin) {
									_vn[0] = neighbor.voxeldata[zofs + yofs + xofs + 1].type;
									if (_area[NEG_X].valid != 0) {
										_vn[1] = _area[NEG_X].voxeldata[zofs + yofs + VOXEL_CHUNK_SIZE_XZ - 1].type;
									}
								} else if (xmax) {
									if (_area[POS_X].valid != 0) {
										_vn[0] = _area[POS_X].voxeldata[zofs + yofs].type;
									}
									_vn[1] = neighbor.voxeldata[zofs + yofs + xofs - 1].type;
								} else {
									_vn[0] = neighbor.voxeldata[zofs + yofs + xofs + 1].type;
									_vn[1] = neighbor.voxeldata[zofs + yofs + xofs - 1].type;
								}

								if (ymin) {
									_vn[2] = neighbor.voxeldata[yofs+(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
									if (_area[NEG_Y].valid != 0) {
										_vn[3] = _area[NEG_Y].voxeldata[(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_Y - 1)) + zofs + xofs].type;
									}
								} else if (ymax) {
									if (_area[POS_Y].valid != 0) {
										_vn[2] = _area[POS_Y].voxeldata[zofs + xofs].type;
									}
									_vn[3] = neighbor.voxeldata[yofs-(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
								} else {
									_vn[2] = neighbor.voxeldata[yofs+(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
									_vn[3] = neighbor.voxeldata[yofs-(VOXEL_CHUNK_SIZE_XZ*VOXEL_CHUNK_SIZE_XZ) + zofs + xofs].type;
								}

								if (zmin) {
									_vn[4] = neighbor.voxeldata[yofs + (zofs + VOXEL_CHUNK_SIZE_XZ) + xofs].type;
									if (_area[NEG_Z].valid != 0) {
										_vn[5] = _area[NEG_Z].voxeldata[yofs + (VOXEL_CHUNK_SIZE_XZ*(VOXEL_CHUNK_SIZE_XZ - 1)) + xofs].type;
									}
								} else if (zmax) {
									if (_area[POS_Z].valid != 0) {
										_vn[4] = _area[POS_Z].voxeldata[yofs + xofs].type;
									}
									_vn[5] = neighbor.voxeldata[yofs + (zofs-VOXEL_CHUNK_SIZE_XZ) + xofs].type;
								} else {
									_vn[4] = neighbor.voxeldata[yofs + (zofs+VOXEL_CHUNK_SIZE_XZ) + xofs].type;
									_vn[5] = neighbor.voxeldata[yofs + (zofs-VOXEL_CHUNK_SIZE_XZ) + xofs].type;
								}

								for (int i = 0; i < 6; ++i) {
									_vnc[i] = _tables.blockContents[(int)_vn[i]];
								}

								var isBorderVoxel = (x < 0) || (x >= VOXEL_CHUNK_SIZE_XZ) || (y < 0) || (y >= VOXEL_CHUNK_SIZE_Y) || (z < 0) || (z >= VOXEL_CHUNK_SIZE_XZ);

								EmitVoxelFaces((x + BORDER_SIZE) + ((z + BORDER_SIZE)*NUM_VOXELS_XZ) + ((y + BORDER_SIZE)*NUM_VOXELS_XZ*NUM_VOXELS_XZ), x + BORDER_SIZE, y + BORDER_SIZE, z + BORDER_SIZE, blocktype, isBorderVoxel);
							}
						}
					}
				}

				_smoothVerts.Finish();
			}
		};

		public static TableStorage tableStorage;

		public struct CompiledChunkData : IDisposable {
			public SmoothingVertsOut_t smoothVerts;
			public VoxelStorage_t voxelStorage;
			public FinalMeshVerts_t outputVerts;
			public NativeArray<PinnedChunkData_t> neighbors;

			public static CompiledChunkData New() {
				return new CompiledChunkData() {
					smoothVerts = SmoothingVertsOut_t.New(),
					voxelStorage = VoxelStorage_t.New(),
					outputVerts = FinalMeshVerts_t.New(),
					neighbors = AllocatePersistentNoInit<PinnedChunkData_t>(27)
				};
			}

			public void Dispose() {
				smoothVerts.Dispose();
				outputVerts.Dispose();
				neighbors.Dispose();
			}
		};

		public static JobHandle ScheduleGenVoxelsJob(WorldChunkPos_t pos, ChunkData_t chunkData, Streaming.CreateGenVoxelsJobDelegate createGenVoxelsJob) {
			return createGenVoxelsJob(pos, NewPinnedChunkData_t(chunkData));
		}

		public static JobHandle ScheduleGenTrisJob(ref CompiledChunkData jobData, JobHandle dependsOn = default(JobHandle)) {
			var genChunkVerts = GenerateChunkVerts_t.New(jobData.smoothVerts, jobData.voxelStorage.voxels, jobData.neighbors, tableStorage).Schedule(dependsOn);
			return GenerateFinalVertices_t.New(SmoothingVertsIn_t.New(jobData.smoothVerts), jobData.outputVerts).Schedule(genChunkVerts);
		}

		
	}
};

