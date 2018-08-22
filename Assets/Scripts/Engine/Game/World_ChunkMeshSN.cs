// Copyright (c) 2018 Pocketwatch Games LLC.

// Surface Nets

//#define BOUNDS_CHECK
//#define NO_SMOOTHING

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static UnityEngine.Debug;

public partial class World {
	public static partial class ChunkMeshGen {

		public static class SurfaceNets {

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

#if DEBUG_VOXEL_MESH
				public Debug.VoxelArray1D voxelsDebug;
				Debug.BlendedVoxel_t[] _voxelsDebug;
				GCHandle _pinnedVoxelsDebug;
#endif
				public static VoxelStorage_t New() {
					return new VoxelStorage_t {
#if DEBUG_VOXEL_MESH
						_voxelsDebug = new Debug.BlendedVoxel_t[NUM_VOXELS],
#endif
						_voxels = new BlendedVoxel_t[NUM_VOXELS]
					};
				}

				public void Pin() {
					Assert(!_pinnedVoxels.IsAllocated);
					_pinnedVoxels = GCHandle.Alloc(_voxels, GCHandleType.Pinned);
					unsafe {
						voxels = VoxelArray1D.New((BlendedVoxel_t*)_pinnedVoxels.AddrOfPinnedObject().ToPointer(), _voxels.Length);
					}
#if DEBUG_VOXEL_MESH
				_pinnedVoxelsDebug = GCHandle.Alloc(_voxelsDebug, GCHandleType.Pinned);
				unsafe {
					voxelsDebug = Debug.VoxelArray1D.New((Debug.BlendedVoxel_t*)_pinnedVoxelsDebug.AddrOfPinnedObject().ToPointer(), _voxels.Length);
				}
#endif
				}

				public void Unpin() {
					Assert(_pinnedVoxels.IsAllocated);
					voxels = new VoxelArray1D();
					_pinnedVoxels.Free();

#if DEBUG_VOXEL_MESH
				voxelsDebug = new Debug.VoxelArray1D();
				_pinnedVoxelsDebug.Free();
#endif
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

			unsafe struct GenerateChunkVerts_t : IJob {
				SmoothingVertsOut_t _smoothVerts;
				VoxelArray1D _voxels;
				Tables _tables;
				[ReadOnly]
				NativeArray<PinnedChunkData_t> _area;
				[ReadOnly]
				NativeArray<int> _blockMaterials;

				VoxelNeighbors_t _vn;
				VoxelNeighborContents_t _vnc;

				int _numVoxels;
				int _numTouched;

				public static GenerateChunkVerts_t New(SmoothingVertsOut_t smoothVerts, VoxelArray1D voxels, NativeArray<PinnedChunkData_t> area, TableStorage tableStorage, NativeArray<int> blockMaterials) {
					return new GenerateChunkVerts_t {
						_smoothVerts = smoothVerts,
						_voxels = voxels,
						_tables = Tables.New(tableStorage),
						_blockMaterials = blockMaterials,
						_area = area,
						_vn = new VoxelNeighbors_t()
					};
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
					var start = Utils.ReadTimestamp();

					var chunk = _area[1 + Y_PITCH + Z_PITCH];
					chunk.flags = chunk.pinnedFlags[0];
					chunk.timing = chunk.pinnedTiming[0];

					Run(chunk);

					chunk.timing.verts1 = Utils.ReadTimestamp() - start;
					chunk.pinnedTiming[0] = chunk.timing;
				}

				void Run(PinnedChunkData_t chunk) {
					_smoothVerts.Init();

					_numVoxels = 0;

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

									//AddVoxel(x+BORDER_SIZE, y+BORDER_SIZE, z+BORDER_SIZE, voxel);
								} else {
									++_numVoxels;
								}
							}
						}
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

									//EmitVoxelFaces((x + BORDER_SIZE) + ((z + BORDER_SIZE)*NUM_VOXELS_XZ) + ((y + BORDER_SIZE)*NUM_VOXELS_XZ*NUM_VOXELS_XZ), x + BORDER_SIZE, y + BORDER_SIZE, z + BORDER_SIZE, blocktype, isBorderVoxel);
								}
							}
						}
					}

					_smoothVerts.Finish();
				}
			};

#if SURFACE_NETS
			public unsafe static JobHandle NewGenTrisJob(ref CompiledChunkData jobData, ChunkTimingData_t* timing, NativeArray<int> blockMaterials, JobHandle dependsOn = default(JobHandle)) {
				var genChunkVerts = GenerateChunkVerts_t.New(jobData.smoothVerts, jobData.voxelStorage.voxels, jobData.neighbors, tableStorage, blockMaterials).Schedule(dependsOn);
				return GenerateFinalVertices_t.New(SmoothingVertsIn_t.New(jobData.smoothVerts), jobData.outputVerts, timing).Schedule(genChunkVerts);
			}
#endif
		}
	}
};

