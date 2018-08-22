// Copyright (c) 2018 Pocketwatch Games LLC.

// Surface Nets

//#define BOUNDS_CHECK
//#define NO_SMOOTHING

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
			public VoxelArray1D voxelsDebug;
			BlendedVoxel_t[] _voxelsDebug;
			GCHandle _pinnedVoxelsDebug;
#endif
				public static VoxelStorage_t New() {
					return new VoxelStorage_t {
#if DEBUG_VOXEL_MESH
					_voxelsDebug = new BlendedVoxel_t[NUM_VOXELS],
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
					voxelsDebug = VoxelArray1D.New((BlendedVoxel_t*)_pinnedVoxelsDebug.AddrOfPinnedObject().ToPointer(), _voxels.Length);
				}
#endif
				}

				public void Unpin() {
					Assert(_pinnedVoxels.IsAllocated);
					voxels = new VoxelArray1D();
					_pinnedVoxels.Free();

#if DEBUG_VOXEL_MESH
				voxelsDebug = new VoxelArray1D();
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

				void EmitVoxelFaces(int index, int x, int y, int z, EVoxelBlockType blocktype, bool isBorderVoxel) {
					var voxel = _voxels[index];
					var contents = _tables.blockContents[(int)blocktype];

					// emit cube tris that are not degenerate from collapse
					for (int i = 0; i < 6; ++i) {
						if (_vnc[i] < contents) {
							Color32 color;
							uint smg;
							float factor;
							int layer;
							int material = _blockMaterials[(int)blocktype - 1];

							GetBlockColorAndSmoothing(blocktype, out color, out smg, out factor, out layer);

							var v0 = GetVoxelVert(voxel, _tables.voxelFaces[i][0]);

							for (int k = 1; k <= 2; ++k) {
								var v1 = GetVoxelVert(voxel, _tables.voxelFaces[i][k]);
								var v2 = GetVoxelVert(voxel, _tables.voxelFaces[i][k + 1]);

								if ((v0 != v1) && (v0 != v2) && (v1 != v2)) {
									_smoothVerts.EmitTri(
										x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
										x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
										x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
										smg, factor, color, layer, material, isBorderVoxel
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
										int layer;
										int material = _blockMaterials[(int)_vn[i] - 1];

										GetBlockColorAndSmoothing(_vn[i], out color, out smg, out factor, out layer);

										_smoothVerts.EmitTri(
											x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
											x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
											x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
											smg, factor, color, layer, material, isBorderVoxel
										);

										// emit backface?
										if (_vnc[i] > contents) {
											_smoothVerts.EmitTri(
												x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
												x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
												x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
												smg, factor, color, layer, material, isBorderVoxel
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

#if SURFACE_NETS
			public unsafe static JobHandle NewGenTrisJob(ref CompiledChunkData jobData, ChunkTimingData_t* timing, NativeArray<int> blockMaterials, JobHandle dependsOn = default(JobHandle)) {
				var genChunkVerts = GenerateChunkVerts_t.New(jobData.smoothVerts, jobData.voxelStorage.voxels, jobData.neighbors, tableStorage, blockMaterials).Schedule(dependsOn);
				return GenerateFinalVertices_t.New(SmoothingVertsIn_t.New(jobData.smoothVerts), jobData.outputVerts, timing).Schedule(genChunkVerts);
			}
#endif
		}
	}
};

