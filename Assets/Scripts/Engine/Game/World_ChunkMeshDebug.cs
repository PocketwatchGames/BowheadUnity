// Copyright (c) 2018 Pocketwatch Games LLC.

//#define BOUNDS_CHECK
//#define NO_SMOOTHING

using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static UnityEngine.Debug;

public partial class World {
	public static partial class ChunkMeshGen {
		
		public struct FinalMeshVertsDebug_t : IDisposable {

			[WriteOnly]
			public NativeArray<Vector3> positions;
			public NativeArray<Color32> colors;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts; // [vertCount][indexCount]

			NativeArray<int> _vtoi;
			NativeArray<int> _vtoiCounts;

			int _vertCount;
			int _indexCount;

			public static FinalMeshVertsDebug_t New() {
				var verts = new FinalMeshVertsDebug_t {
					positions = AllocatePersistentNoInit<Vector3>(ushort.MaxValue),
					colors = AllocatePersistentNoInit<Color32>(ushort.MaxValue),
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(2),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES*BANK_SIZE),
					_vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				colors.Dispose();
				indices.Dispose();
				counts.Dispose();
				_vtoi.Dispose();
				_vtoiCounts.Dispose();
			}

			public void Init() {
				_vertCount = 0;
				_indexCount = 0;

				for (int i = 0; i < counts.Length; ++i) {
					counts[i] = 0;
				}

				for (int i = 0; i < _vtoiCounts.Length; ++i) {
					_vtoiCounts[i] = 0;
				}
			}

			public void Finish() {
				counts[0] = _vertCount;
				counts[1] = _indexCount;
			}

			static bool ColorEqual(Color32 a, Color32 b) {
				return (a.r == b.r) &&
				(a.g == b.g) &&
				(a.b == b.b) &&
				(a.a == b.a);
			}

			public void EmitVert(int x, int y, int z, Color32 color) {
				int INDEX = (y*(VOXEL_CHUNK_SIZE_XZ+1)*(VOXEL_CHUNK_SIZE_XZ+1)) + (z*(VOXEL_CHUNK_SIZE_XZ+1)) + x;

				// existing vertex?
				int vtoiCount = _vtoiCounts[INDEX];
				for (int i = 0; i < vtoiCount; ++i) {
					int idx = _vtoi[(INDEX*BANK_SIZE)+i];
					if (ColorEqual(colors[idx], color)) {
						Assert((_indexCount) < ushort.MaxValue);
						indices[_indexCount++] = idx;
						return;
					}
				}

				Assert((_vertCount) < ushort.MaxValue);
				Assert((_indexCount) < ushort.MaxValue);
				Assert(vtoiCount < BANK_SIZE);

				positions[_vertCount] = new Vector3(x, y, z);
				colors[_vertCount] = color;
				
				_vtoi[(INDEX*BANK_SIZE)+vtoiCount] = _vertCount;
				_vtoiCounts[INDEX] = vtoiCount + 1;

				indices[_indexCount++] = _vertCount;
				_vertCount++;
			}
		};

		struct SmoothingVertsInDebug_t {
			[ReadOnly]
			public NativeArray<Int3_t> positions;
			[ReadOnly]
			public NativeArray<Color32> colors;
			[ReadOnly]
			public NativeArray<int> indices;
			[ReadOnly]
			public NativeArray<int> counts;
			[ReadOnly]
			public NativeArray<int> vtoiCounts;
			
			public static SmoothingVertsInDebug_t New(SmoothingVertsOutDebug_t smv) {
				return new SmoothingVertsInDebug_t {
					positions = smv.positions,
					colors = smv.colors,
					indices = smv.indices,
					counts = smv.counts,
					vtoiCounts = smv.vtoiCounts
				};
			}
		};

		public struct SmoothingVertsOutDebug_t : IDisposable {
			[WriteOnly]
			public NativeArray<Int3_t> positions;
			[WriteOnly]
			public NativeArray<Color32> colors;
			[WriteOnly]
			public NativeArray<int> indices;
			[WriteOnly]
			public NativeArray<int> counts; // [numIndices]
			public NativeArray<int> vtoiCounts;
			
			NativeArray<int> _vtoi;

			int _vertCount;
			int _indexCount;

			public static SmoothingVertsOutDebug_t New() {
				var verts = new SmoothingVertsOutDebug_t {
					positions = AllocatePersistentNoInit<Int3_t>(ushort.MaxValue),
					colors = AllocatePersistentNoInit<Color32>(ushort.MaxValue*BANK_SIZE),
					indices = AllocatePersistentNoInit<int>(ushort.MaxValue),
					counts = AllocatePersistentNoInit<int>(1),
					_vtoi = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES),
					vtoiCounts = AllocatePersistentNoInit<int>(MAX_OUTPUT_VERTICES)
				};
				return verts;
			}

			public void Dispose() {
				positions.Dispose();
				colors.Dispose();
				indices.Dispose();
				counts.Dispose();
				_vtoi.Dispose();
				vtoiCounts.Dispose();
			}

			public void Init() {
				_vertCount = 0;
				_indexCount = 0;
				for (int i = 0; i < _vtoi.Length; ++i) {
					_vtoi[i] = 0;
				}
			}

			public void Finish() {
				counts[0] = _indexCount;
			}

			int EmitVert(int x, int y, int z, Color32 color) {
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

				colors[(idx*BANK_SIZE) + count] = color;

				vtoiCounts[idx] = count + 1;

				return idx | (count << 24);
			}

			public void EmitTri(int x0, int y0, int z0, int x1, int y1, int z1, int x2, int y2, int z2, Color32 color, bool isBorderVoxel) {
				if (isBorderVoxel) {
					if ((x0 >= 0) && (x0 <= VOXEL_CHUNK_SIZE_XZ) && (y0 >= 0) && (y0 <= VOXEL_CHUNK_SIZE_Y) && (z0 >= 0) && (z0 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x0, y0, z0, color);
					}
					if ((x1 >= 0) && (x1 <= VOXEL_CHUNK_SIZE_XZ) && (y1 >= 0) && (y1 <= VOXEL_CHUNK_SIZE_Y) && (z1 >= 0) && (z1 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x1, y1, z1, color);
					}
					if ((x2 >= 0) && (x2 <= VOXEL_CHUNK_SIZE_XZ) && (y2 >= 0) && (y2 <= VOXEL_CHUNK_SIZE_Y) && (z2 >= 0) && (z2 <= VOXEL_CHUNK_SIZE_XZ)) {
						EmitVert(x2, y2, z2, color);
					}
				} else {
					indices[_indexCount++] = EmitVert(x0, y0, z0, color);
					indices[_indexCount++] = EmitVert(x1, y1, z1, color);
					indices[_indexCount++] = EmitVert(x2, y2, z2, color);
				}
			}
		};

		struct GenerateFinalVerticesDebug_t : IJob {
			SmoothingVertsInDebug_t _smoothVerts;
			FinalMeshVertsDebug_t _finalVerts;
			
			public unsafe static GenerateFinalVerticesDebug_t New(SmoothingVertsInDebug_t inVerts, FinalMeshVertsDebug_t outVerts) {
				return new GenerateFinalVerticesDebug_t {
					_smoothVerts = inVerts,
					_finalVerts = outVerts
				};
			}

			public void Execute() {
				Run();
			}

			void Run() {
				_finalVerts.Init();

				var numIndices = _smoothVerts.counts[0];

				for (int i = 0; i < numIndices; ++i) {
					var vi = _smoothVerts.indices[i];

					Color32 c;
					Int3_t p;
					BlendVertex(vi, out p, out c);
					_finalVerts.EmitVert(p.x, p.y, p.z, c);
				}
				
				_finalVerts.Finish();
			}

			void BlendVertex(int index, out Int3_t outPos, out Color32 outColor) {
				var ofs = index >> 24;
				index = index & 0x00ffffff;

				var bankedIndex = (index*BANK_SIZE)+ofs;
				
				outPos = _smoothVerts.positions[index];
				outColor = _smoothVerts.colors[bankedIndex];
			}
		};

		unsafe struct GenerateChunkVertsDebug_t : IJob {
			SmoothingVertsOutDebug_t _smoothVerts;
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

			public static GenerateChunkVertsDebug_t New(SmoothingVertsOutDebug_t smoothVerts, VoxelArray1D voxels, NativeArray<PinnedChunkData_t> area, TableStorage tableStorage, NativeArray<int> blockMaterials) {
				return new GenerateChunkVertsDebug_t {
					_smoothVerts = smoothVerts,
					_voxels = voxels,
					_tables = Tables.New(tableStorage),
					_blockMaterials = blockMaterials,
					_area = area,
					_vn = new VoxelNeighbors_t()
				};
			}

			void AddVoxel(int x, int y, int z, Voxel_t voxel) {
				var blendVoxel = _voxels[_numVoxels++];
				ZeroInts(blendVoxel->vertexFlags, 8);
				ZeroInts(blendVoxel->neighbors, 6);				
				blendVoxel->touched = true;
			}

			void GetBlockColor(EVoxelBlockType blocktype, int x, int y, int z, out Color32 color) {
				color = _tables.blockColors[(int)blocktype - 1];

				if ((x & 1) != 0) {
					Color cc = color;
					cc *= 0.9f;
					cc.a = 1f;
					color = cc;
				}
				if ((z & 1) != 0) {
					Color cc = color;
					cc *= 0.9f;
					cc.a = 1f;
					color = cc;
				}
				if ((y & 1) != 0) {
					Color cc = color;
					cc *= 0.9f;
					cc.a = 1f;
					color = cc;
				}
			}

			void EmitVoxelFaces(int index, int x, int y, int z, Voxel_t voxel, bool isBorderVoxel) {
				var contents = _tables.blockContents[(int)voxel.type];

				Color32 color;

				if ((voxel.flags & EVoxelBlockFlags.FullVoxel) != 0) {
					color = Color.red;
				} else {
					GetBlockColor(voxel.type, x, y, z, out color);
				}

				for (int i = 0; i < 6; ++i) {
					if (_vnc[i] < contents) {

						var v0 = _tables.voxelFaces[i][0];

						for (int k = 1; k <= 2; ++k) {
							var v1 =_tables.voxelFaces[i][k];
							var v2 = _tables.voxelFaces[i][k + 1];

							if ((v0 != v1) && (v0 != v2) && (v1 != v2)) {
								_smoothVerts.EmitTri(
									x + _tables.voxelVerts[v0][0] - BORDER_SIZE, y + _tables.voxelVerts[v0][1] - BORDER_SIZE, z + _tables.voxelVerts[v0][2] - BORDER_SIZE,
									x + _tables.voxelVerts[v1][0] - BORDER_SIZE, y + _tables.voxelVerts[v1][1] - BORDER_SIZE, z + _tables.voxelVerts[v1][2] - BORDER_SIZE,
									x + _tables.voxelVerts[v2][0] - BORDER_SIZE, y + _tables.voxelVerts[v2][1] - BORDER_SIZE, z + _tables.voxelVerts[v2][2] - BORDER_SIZE,
									color, isBorderVoxel
								);
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
				var chunk = _area[1 + Y_PITCH + Z_PITCH];
				chunk.flags = chunk.pinnedFlags[0];
				chunk.timing = chunk.pinnedTiming[0];

				Run(chunk);
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

								EmitVoxelFaces((x + BORDER_SIZE) + ((z + BORDER_SIZE)*NUM_VOXELS_XZ) + ((y + BORDER_SIZE)*NUM_VOXELS_XZ*NUM_VOXELS_XZ), x + BORDER_SIZE, y + BORDER_SIZE, z + BORDER_SIZE, voxel, isBorderVoxel);
							}
						}
					}
				}

				_smoothVerts.Finish();
			}
		};

#if DEBUG_VOXEL_MESH
		public unsafe static JobHandle ScheduleGenTrisDebugJob(ref CompiledChunkData jobData, NativeArray<int> blockMaterials, JobHandle dependsOn = default(JobHandle)) {
			var genChunkVerts = GenerateChunkVertsDebug_t.New(jobData.smoothVertsDebug, jobData.voxelStorage.voxelsDebug, jobData.neighbors, tableStorage, blockMaterials).Schedule(dependsOn);
			return GenerateFinalVerticesDebug_t.New(SmoothingVertsInDebug_t.New(jobData.smoothVertsDebug), jobData.outputVertsDebug).Schedule(genChunkVerts);
		}
#endif

	}
};

