// Copyright (c) 2018 Pocketwatch Games LLC.

// Surface Nets

//#define BOUNDS_CHECK
//#define NO_SMOOTHING

// Based on:
// https://github.com/mikolalysenko/mikolalysenko.github.com/blob/master/Isosurface/js/surfacenets.js

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

			const int MAX_OUTPUT_VERTICES = (VOXEL_CHUNK_SIZE_XZ+1) * (VOXEL_CHUNK_SIZE_XZ+1) * (VOXEL_CHUNK_SIZE_Y+1);
			const int BANK_SIZE = 24;
			const int MAX_MATERIALS_PER_VERTEX = 4*2; // max cube crossing edges * 2

			const int BORDER_SIZE = 1;
			const int NUM_VOXELS_XZ = VOXEL_CHUNK_SIZE_XZ + BORDER_SIZE;
			const int NUM_VOXELS_Y = VOXEL_CHUNK_SIZE_Y + BORDER_SIZE;
			const int MAX_VIS_VOXELS = NUM_VOXELS_XZ * NUM_VOXELS_XZ * NUM_VOXELS_Y;

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

#if DEBUG_VOXEL_MESH
				public Debug.VoxelArray1D voxelsDebug;
				Debug.BlendedVoxel_t[] _voxelsDebug;
				GCHandle _pinnedVoxelsDebug;
#endif
				public static VoxelStorage_t New() {
					return new VoxelStorage_t {
#if DEBUG_VOXEL_MESH
						_voxelsDebug = new Debug.BlendedVoxel_t[Debug.MAX_VIS_VOXELS],
#endif
					};
				}

				public void Pin() {
#if DEBUG_VOXEL_MESH
					_pinnedVoxelsDebug = GCHandle.Alloc(_voxelsDebug, GCHandleType.Pinned);
					unsafe {
						voxelsDebug = Debug.VoxelArray1D.New((Debug.BlendedVoxel_t*)_pinnedVoxelsDebug.AddrOfPinnedObject().ToPointer(), _voxelsDebug.Length);
					}
#endif
				}

				public void Unpin() {
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

				public void EmitVert(Vector3 position, Vector3 normal, Color32 color, Vector4 textureBlending) {
					Assert((_vertCount-_layerVertOfs) < ushort.MaxValue);

					// this is slow as fuck
					for (int i = _layerVertOfs; i < _vertCount; ++i) {
						if (Vector3.Equals(positions[i], position) &&
							Vector3.Equals(normals[i], normal) &&
							Vector4.Equals(this.textureBlending[i], textureBlending)) {
							EmitIndex(i);
							return;
						}
					}

					positions[_vertCount] = position;
					normals[_vertCount] = normal;
					colors[_vertCount] = color;
					this.textureBlending[_vertCount] = textureBlending;

					EmitIndex(_vertCount);
					_vertCount++;
				}

				void EmitIndex(int i) {
					Assert((_indexCount-_layerIndexOfs) < ushort.MaxValue);
					indices[_indexCount++] = i - _layerVertOfs;
				}
			};

			struct SmoothingVertsIn_t {
				[ReadOnly]
				public NativeArray<Vector3> positions;
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
				public NativeArray<Vector3> positions;
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
				int _vertCount;
				int _indexCount;

				public struct Vertex_t {
					int v0;
					uint smg;
					float smoothing;
					Color32 color;
					int layer;
				};

				public static SmoothingVertsOut_t New() {
					var verts = new SmoothingVertsOut_t {
						positions = AllocatePersistentNoInit<Vector3>(MAX_OUTPUT_VERTICES),
						normals = AllocatePersistentNoInit<Vector3>(MAX_OUTPUT_VERTICES*BANK_SIZE),
						colors = AllocatePersistentNoInit<Color32>(MAX_OUTPUT_VERTICES*BANK_SIZE),
						smoothFactor = AllocatePersistentNoInit<float>(MAX_OUTPUT_VERTICES*BANK_SIZE),
						smgs = AllocatePersistentNoInit<uint>(MAX_OUTPUT_VERTICES*BANK_SIZE),
						layers = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES*BANK_SIZE),
						indices = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES),
						counts = AllocatePersistentNoInit<int>(2),
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
					vtoiCounts.Dispose();
					vertMaterials.Dispose();
					vertMaterialCount.Dispose();
				}

				public void Init() {
					_vertCount = 0;
					_indexCount = 0;
					_maxLayer = 0;
					for (int i = 0; i < vertMaterialCount.Length; ++i) {
						vertMaterialCount[i] = 0;
					}
				}

				public void Finish() {
					counts[0] = _indexCount;
					counts[1] = _maxLayer;
				}

				public void WriteVert(int idx, Vector3 position) {
					positions[idx] = position;
				}

				public int EmitVert() {
					var idx = _vertCount++;
					vtoiCounts[idx] = 0;
					return idx;
				}

				int EmitVert(int v0, uint smg, float smoothingFactor, Vector3 normal, Color32 color, int layer) {
					var count = vtoiCounts[v0];
					BoundsCheckAndThrow(count, 0, BANK_SIZE);

					normals[(v0*BANK_SIZE) + count] = normal;
					colors[(v0*BANK_SIZE) + count] = color;
					smoothFactor[(v0*BANK_SIZE) + count] = smoothingFactor;
					smgs[(v0*BANK_SIZE) + count] = smg;
					layers[(v0*BANK_SIZE) + count] = layer;

					vtoiCounts[v0] = count + 1;
					_maxLayer = (layer > _maxLayer) ? layer : _maxLayer;
					
					return v0 | (count << 24);
				}

				public void EmitFace(int v0, int v1, int v2, int v3, uint smg, float smoothingFactor, Color32 color, int layer, int material, bool isBorder) {
					EmitTri(v0, v1, v2, smg, smoothingFactor, color, layer, material, isBorder);
					EmitTri(v0, v2, v3, smg, smoothingFactor, color, layer, material, isBorder);
				}
				
				void EmitTri(int v0, int v1, int v2, uint smg, float smoothingFactor, Color32 color, int layer, int material, bool isBorder) {
					Vector3 normal;
					if (GetNormal(v0, v1, v2, out normal)) {
						if (isBorder) {
							EmitVert(v0, smg, smoothingFactor, normal, color, layer);
							EmitVert(v1, smg, smoothingFactor, normal, color, layer);
							EmitVert(v2, smg, smoothingFactor, normal, color, layer);
						} else {
							indices[_indexCount++] = EmitVert(v0, smg, smoothingFactor, normal, color, layer);
							indices[_indexCount++] = EmitVert(v1, smg, smoothingFactor, normal, color, layer);
							indices[_indexCount++] = EmitVert(v2, smg, smoothingFactor, normal, color, layer);
						}

						AddVertexMaterial(v0, layer, material);
						AddVertexMaterial(v1, layer, material);
						AddVertexMaterial(v2, layer, material);
					}
				}

				public void AddVertexMaterial(int v0, int layer, int material) {
					var count = vertMaterialCount[v0];
					var code = material | (layer << 16);
					if (count < MAX_MATERIALS_PER_VERTEX) {
						var OFS = v0 * MAX_MATERIALS_PER_VERTEX;
						vertMaterials[OFS+count] = code;
						vertMaterialCount[v0] = count + 1;
					}
				}

				bool GetNormal(int v0, int v1, int v2, out Vector3 n) {
					n = default(Vector3);

					var a = positions[v0];
					var b = positions[v1];
					var c = positions[v2];

					var u = (b - a);
					if (u.sqrMagnitude < 1e-4f) {
						return false;
					}
					u.Normalize();
					
					var v = (c - a);
					if (v.sqrMagnitude < 1e-4f) {
						return false;
					}

					v.Normalize();

					if (Mathf.Abs(Vector3.Dot(u, v)) >= 0.9999f) {
						return false;
					}

					n = Vector3.Cross(u, v);
					if (n.sqrMagnitude < 1e-4f) {
						return false;
					}
					n.Normalize();

					return true;
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
													Vector3 p;
													Vector3 n;
													Color32 c;
													Vector4 blendFactor;

													BlendVertex(vertNum0, vertOfs0, bankedIndex0, out p, out n, out c);
													blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum0);
													_finalVerts.EmitVert(p, n, c, blendFactor);

													BlendVertex(vertNum1, vertOfs1, bankedIndex1, out p, out n, out c);
													blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum1);
													_finalVerts.EmitVert(p, n, c, blendFactor);

													BlendVertex(vertNum2, vertOfs2, bankedIndex2, out p, out n, out c);
													blendFactor = GetTriVertTexBlendFactor(texBlend, layer, vertNum2);
													_finalVerts.EmitVert(p, n, c, blendFactor);

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

				void BlendVertex(int index, int ofs, int bankedIndex, out Vector3 outPos, out Vector3 outNormal, out Color32 outColor) {

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
				Tables _tables;
				[ReadOnly]
				NativeArray<PinnedChunkData_t> _area;
				[ReadOnly]
				NativeArray<int> _blockMaterials;

				VoxelNeighbors_t _vn;
				VoxelNeighborContents_t _vnc;

				public static GenerateChunkVerts_t New(SmoothingVertsOut_t smoothVerts, NativeArray<PinnedChunkData_t> area, TableStorage tableStorage, NativeArray<int> blockMaterials) {
					return new GenerateChunkVerts_t {
						_smoothVerts = smoothVerts,
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

				void GetBlockColorAndSmoothing(EVoxelBlockType blocktype, out Color32 color, out uint smg, out float smoothing, out int layer) {
					color = _tables.blockColors[(int)blocktype - 1];
					smg = _tables.blockSmoothingGroups[(int)blocktype - 1];
					smoothing = _tables.blockSmoothingFactors[(int)blocktype - 1];

					if (blocktype == EVoxelBlockType.Water) {
						layer = EChunkLayers.Water.ToIndex();
					} else if ((blocktype == EVoxelBlockType.Leaves) || (blocktype == EVoxelBlockType.Needles) || (blocktype == EVoxelBlockType.Wood)) {
						layer = EChunkLayers.Trees.ToIndex();
					} else {
						layer = EChunkLayers.Terrain.ToIndex();
					}
				}

				const int BUFFER_SIZE = ((VOXEL_CHUNK_SIZE_XZ+5)*(VOXEL_CHUNK_SIZE_XZ+5))*2;

				void Run(PinnedChunkData_t chunk) {
					_smoothVerts.Init();

					if ((chunk.flags & EChunkFlags.SOLID) == 0) {
						// no solid blocks in this chunk it can't have any visible faces.
						_smoothVerts.Finish();
						return;
					}

					byte* grid = stackalloc byte[8];
					float* density = stackalloc float[8];
					int* x = stackalloc int[3];
					int* R = stackalloc int[3];
					int* buffer = stackalloc int[BUFFER_SIZE];
					float* s = stackalloc float[3];

					R[0] = 1;
					R[1] = VOXEL_CHUNK_SIZE_XZ+5;
					R[2] = (VOXEL_CHUNK_SIZE_XZ+5)*(VOXEL_CHUNK_SIZE_XZ+5);

					int bidx = 1;

					for (x[2] = -2; x[2] < VOXEL_CHUNK_SIZE_Y+1; ++x[2], bidx ^= 1, R[2] = -R[2]) {

						var m = 1 + (VOXEL_CHUNK_SIZE_XZ+5) * (1 + bidx * (VOXEL_CHUNK_SIZE_XZ+5));

						for (x[1] = -2; x[1] < VOXEL_CHUNK_SIZE_XZ+1; ++x[1], m += 2) {
							for (x[0] = -2; x[0] < VOXEL_CHUNK_SIZE_XZ+1; ++x[0], ++m) {

								// read voxels around this vertex
								// note the mask, and grid verts for the cubes are X/Y/Z, but unity
								// is Y up so we have to swap Z/Y

								bool isBorder = (x[0] < 0) || (x[0] >= VOXEL_CHUNK_SIZE_XZ) || (x[1] < 0) || (x[1] >= VOXEL_CHUNK_SIZE_XZ) || (x[2] < 0) || (x[2] >= VOXEL_CHUNK_SIZE_Y);

								int g = 0;
								int mask = 0;

								for (int zz = 0; zz < 2; ++zz) {
									
									var iz = x[2] + zz;
									var zwrap = Wrap(iz, VOXEL_CHUNK_SIZE_Y);
									var zofs = VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ*zwrap; // zofs is really yofs in voxel data

									var cz = (iz < 0) ? 0 : (iz <VOXEL_CHUNK_SIZE_Y) ? 1 : 2;

									for (int yy = 0; yy < 2; ++yy) {
										var iy = x[1] + yy;
										var ywrap = Wrap(iy, VOXEL_CHUNK_SIZE_XZ);
										var yofs = VOXEL_CHUNK_SIZE_XZ * ywrap; // yofs is really zofs in voxel data

										var cy = (iy < 0) ? 0 : (iy < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

										for (int xx = 0; xx < 2; ++xx, ++g) {
											var ix = x[0] + xx;

											var xwrap = Wrap(ix, VOXEL_CHUNK_SIZE_XZ);
											var xofs = xwrap;

											var cx = (ix < 0) ? 0 : (ix < VOXEL_CHUNK_SIZE_XZ) ? 1 : 2;

											var chunkIndex = cx + (cz*Y_PITCH) + (cy*Z_PITCH);
											var POS_X = chunkIndex + 1;
											var NEG_X = chunkIndex - 1;
											var POS_Y = chunkIndex + Y_PITCH;
											var NEG_Y = chunkIndex - Y_PITCH;
											var POS_Z = chunkIndex + Z_PITCH;
											var NEG_Z = chunkIndex - Z_PITCH;

											var neighbor = _area[chunkIndex];
											var voxel = neighbor.valid != 0 ? neighbor.voxeldata[xofs + yofs + zofs] : EVoxelBlockType.Air;
											grid[g] = voxel.raw;
											density[g] = (voxel.density / 255f) * 2f - 1f;
											if (_tables.blockContents[(int)voxel.type] == EVoxelBlockContents.None) {
												mask |= 1 << g;
											}
										}
									}
								}

								// multiple contents

								//for (int i = 0; i < 12; ++i) {
								//	var v0 = _tables.sn_cubeEdges[i*2+0];
								//	var v1 = _tables.sn_cubeEdges[i*2+1];
								//	BoundsCheckAndThrow(v0, 0, 8);
								//	BoundsCheckAndThrow(v1, 0, 8);
								//	var v0v = new Voxel_t(grid[v0]);
								//	var v1v = new Voxel_t(grid[v1]);
								//	var v0c = _tables.blockContents[(int)v0v.type];
								//	var v1c = _tables.blockContents[(int)v1v.type];

								//	if (v0c < v1c) {
								//		mask |= 1 << v0;
								//	} else if (v1c < v0c) {
								//		mask |= 1 << v1;
								//	}
								//}

								if ((mask == 0) || (mask == 255)) {
									// no contents change
									continue;
								}

								var vertIdx = _smoothVerts.EmitVert();
								BoundsCheckAndThrow(m, 0, BUFFER_SIZE);
								buffer[m] = vertIdx;
								
								var edgeMask = _tables.sn_edgeTable[mask];
								s[0] = 0; s[1] = 0; s[2] = 0;
								var edgeCount = 0;

								for (int i = 0; i < 12; ++i) {
									if ((edgeMask & (1<<i)) == 0) {
										continue;
									}

									var v0 = _tables.sn_cubeEdges[i*2+0];
									var v1 = _tables.sn_cubeEdges[i*2+1];
									BoundsCheckAndThrow(v0, 0, 8);
									BoundsCheckAndThrow(v1, 0, 8);

									//var v0v = new Voxel_t(grid[v0]);
									//var v1v = new Voxel_t(grid[v1]);
									//var v0c = _tables.blockContents[(int)v0v.type];
									//var v1c = _tables.blockContents[(int)v1v.type];

									var t = density[v0] - density[v1];
									t = density[v0] / t;
									
									for (int j = 0, k = 1; j < 3; ++j, k <<= 1) {
										var a = v0 & k;
										var b = v1 & k;

										BoundsCheckAndThrow(j, 0, 3);

										if (a != b) {
											s[j] += (a != 0) ? 1.0f - t : t;
										} else {
											s[j] += (a != 0) ? 1.0f : 0;
										}
									}
									
									++edgeCount;
								}

								{
									var avg = 1f / edgeCount;
									// NOTE: swapped Z/Y for Y up
									Vector3 v = new Vector3(x[0] + s[0]*avg + 0.5f, x[2] + s[2]*avg + 0.5f, x[1] + s[1]*avg + 0.5f);
									_smoothVerts.WriteVert(vertIdx, v);
								}
													
								// if we have 0-level edges on the root vertex, then we can emit a quad containing this vertex
								// and the previous 3 verts
								if (((edgeMask&7) != 0)) {
									for (int i = 0; i < 3; ++i) {
										if ((edgeMask&(1<<i)) == 0) {
											continue;
										}
										
										// ortho axis
										var iu = (i+1)%3;
										var iv = (i+2)%3;
										BoundsCheckAndThrow(iu, 0, 3);
										BoundsCheckAndThrow(iv, 0, 3);

										if ((x[iu] == -2) || (x[iv] == -2)) {
											continue;
										}

										var v0 = _tables.sn_cubeEdges[i*2+0];
										var v1 = _tables.sn_cubeEdges[i*2+1];

										BoundsCheckAndThrow(v0, 0, 8);
										BoundsCheckAndThrow(v1, 0, 8);

										// figure out the material face, which comes from the crossing edge
										int mat, layer;
										uint smg;
										Color32 color;
										float smoothing;

										var v0v = new Voxel_t(grid[v0]);
										var v1v = new Voxel_t(grid[v1]);
										var v0c = _tables.blockContents[(int)v0v.type];
										var v1c = _tables.blockContents[(int)v1v.type];

										if (v0c > v1c) {
											GetBlockColorAndSmoothing(v0v.type, out color, out smg, out smoothing, out layer);
											mat = _blockMaterials[(int)v0v.type - 1];
										} else {
											GetBlockColorAndSmoothing(v1v.type, out color, out smg, out smoothing, out layer);
											mat = _blockMaterials[(int)v1v.type - 1];
										}

										var du = R[iu];
										var dv = R[iv];

										BoundsCheckAndThrow(m-du, 0, BUFFER_SIZE);
										BoundsCheckAndThrow(m-dv, 0, BUFFER_SIZE);
										BoundsCheckAndThrow(m-du-dv, 0, BUFFER_SIZE);

										if ((mask&1) != 0) {
											_smoothVerts.EmitFace(vertIdx, buffer[m-du], buffer[m-du-dv], buffer[m-dv], smg, smoothing, color, layer, mat, isBorder);
										} else {
											_smoothVerts.EmitFace(vertIdx, buffer[m-dv], buffer[m-du-dv], buffer[m-du], smg, smoothing, color, layer, mat, isBorder);
										}
									}
								}
							}
						}
					}

					_smoothVerts.Finish();
				}
			};

#if SURFACE_NETS
			public unsafe static JobHandle NewGenTrisJob(ref CompiledChunkData jobData, ChunkTimingData_t* timing, NativeArray<int> blockMaterials, JobHandle dependsOn = default(JobHandle)) {
				var genChunkVerts = GenerateChunkVerts_t.New(jobData.smoothVerts, jobData.neighbors, tableStorage, blockMaterials).Schedule(dependsOn);
				return GenerateFinalVertices_t.New(SmoothingVertsIn_t.New(jobData.smoothVerts), jobData.outputVerts, timing).Schedule(genChunkVerts);
			}
#endif
		}
	}
};

