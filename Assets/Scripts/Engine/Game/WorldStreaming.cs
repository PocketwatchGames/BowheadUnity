using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public partial class World {
	public sealed class Streaming : IDisposable {
#if UNITY_EDITOR
		const int MAX_STREAMING_CHUNKS = 4;
#else
		const int MAX_STREAMING_CHUNKS = 8;
#endif
		const uint CHUNK_HASH_SIZE_XZ = VOXEL_CHUNK_SIZE_XZ;
		const uint CHUNK_HASH_SIZE_Y = VOXEL_CHUNK_SIZE_Y;
		const uint CHUNK_HASH_SIZE = CHUNK_HASH_SIZE_XZ * CHUNK_HASH_SIZE_XZ * CHUNK_HASH_SIZE_Y;
		const int MAX_FRAME_TIMEus = 4000;

		readonly ObjectPool<Chunk> _chunkPool = new ObjectPool<Chunk>(VOXEL_CHUNK_VIS_MAX_XZ*VOXEL_CHUNK_VIS_MAX_XZ*VOXEL_CHUNK_VIS_MAX_Y, 0);
		readonly Chunk[] _chunkHash = new Chunk[CHUNK_HASH_SIZE];
		readonly List<VolumeData> _streamingVolumes = new List<VolumeData>();
		readonly Chunk[] _neighbors = new Chunk[27];
		readonly World_ChunkComponent _chunkPrefab;
		GameObject _terrainRoot;

		ChunkJobData _freeJobData;
		ChunkJobData _usedJobData;
		bool _loading;

		public static void StaticInit() {
			FastNoise_t.New(); // construct this to init readonly tables.
			ChunkMeshGen.tableStorage = ChunkMeshGen.TableStorage.New();
		}

		public static void StaticShutdown() {
			ChunkMeshGen.tableStorage.Dispose();
		}
		
		public Streaming(World_ChunkComponent chunkPrefab) {
			_chunkPrefab = chunkPrefab;

			for (int i = 0; i < MAX_STREAMING_CHUNKS; ++i) {
				var chunkData = new ChunkJobData {
					next = _freeJobData
				};
				_freeJobData = chunkData;
			}
		}

		public Volume NewStreamingVolume(int xzSize, int ySize) {
			var zPitch = MaxVoxelChunkLine(xzSize);
			var yPitch = zPitch * zPitch;
			var yDim = MaxVoxelChunkLine(ySize);

			var numChunks = yPitch * yDim;

			var data = new VolumeData() {
				curPos = new WorldChunkPos_t(int.MaxValue, int.MaxValue, int.MaxValue),
				xzSize = xzSize,
				ySize = ySize,
				chunks = new VolumeData.ChunkRef_t[numChunks],
				tempChunks = new VolumeData.ChunkRef_t[numChunks],
				streaming = this
			};
			_streamingVolumes.Add(data);
			return data;
		}

		public void Tick() {

			CompleteJobs(MAX_FRAME_TIMEus);

			var anyLoading = false;

			foreach (var volume in _streamingVolumes) {
				volume.Move();
				anyLoading = anyLoading || volume.loading;
			}

			if (anyLoading) {
				bool didQueue = false;
				bool anyQueued;
				do {
					anyQueued = false;
					foreach (var volume in _streamingVolumes) {
						if (volume.loading) {
							if (QueueNextChunk(volume)) {
								anyQueued = true;
								didQueue = true;
							}

							anyLoading = anyLoading || volume.loading;
						}
					}
				} while (anyQueued);

				if (didQueue) {
					JobHandle.ScheduleBatchedJobs();
				}
			}

			_loading = anyLoading;
		}

		public void Flush() {
			while (_usedJobData != null) {
				var job = _usedJobData;
				job.jobHandle.Complete();
				CompleteJob(job);
				_usedJobData = job.next;
				job.next = _freeJobData;
				_freeJobData = job;
			}
		}

		public void BeginTravel() {
		}

		public void FinishTravel() {
			_terrainRoot = new GameObject("TerrainRoot");
		}

		bool QueueNextChunk(VolumeData volume) {
			for (int i = volume.loadNext; i < volume.count; ++i) {
				var chunk = volume.chunks[i].chunk;

				if ((chunk == null) || chunk.hasTrisData) {
					if (i == volume.loadNext) {
						++volume.loadNext;
						volume.loading = volume.loadNext < volume.count;
					}
				} else {
					// check if chunk is not already queued for generating tris
					if ((chunk.jobData == null) || ((chunk.jobData.flags & EJobFlags.TRIS) == 0)) {
						var canGenerateTris = true;

						{
							int k = 0;
							for (int y = -1; y <= 1; ++y) {
								for (int z = -1; z <= 1; ++z) {
									for (int x = -1; x <= 1; ++x) {
										var neighbor = FindChunk(chunk.pos, x, y, z);
										if (neighbor != null) {
											if (neighbor != chunk) {
												if (!neighbor.hasVoxelData) {
													canGenerateTris = false;
													if (neighbor.jobData == null) {
														return ScheduleGenVoxelsJob(neighbor);
													}
												}
											}
										}
										_neighbors[k++] = neighbor;
									}
								}
							}
						}

						if (canGenerateTris) {
							return ScheduleGenTrisJob(chunk);
						} else if (!chunk.hasVoxelData && (chunk.jobData == null)) {
							return ScheduleGenVoxelsJob(chunk);
						}
					}
				}
			}
			return false;
		}

		ChunkJobData GetFreeJobData() {
			if (_freeJobData == null) {
				return null;
			}
			var jobData = _freeJobData;
			_freeJobData = _freeJobData.next;
			return jobData;
		}

		void QueueJobData(ChunkJobData jobData) {
			jobData.next = _usedJobData;
			_usedJobData = jobData;
		}

		bool ScheduleGenVoxelsJob(Chunk chunk) {
			var jobData = GetFreeJobData();
			if (jobData == null) {
				return false;
			}

			AddRef(chunk);

			jobData.flags = EJobFlags.VOXELS;
			jobData.chunk = chunk;
			chunk.jobData = jobData;
			chunk.chunkData.Pin();

			jobData.jobHandle = ChunkMeshGen.ScheduleGenVoxelsJob(chunk.pos, chunk.chunkData);
			QueueJobData(jobData);
			return true;
		}

		bool ScheduleGenTrisJob(Chunk chunk) {
			bool existingJob = chunk.jobData != null;

			if (!existingJob) {
				chunk.jobData = GetFreeJobData();
				if (chunk.jobData == null) {
					return false;
				}
				chunk.jobData.chunk = chunk;
			}

			chunk.jobData.flags = EJobFlags.VOXELS|EJobFlags.TRIS;
			
			if (!(existingJob || chunk.hasVoxelData)) {
				AddRef(chunk);
				chunk.chunkData.Pin();
				chunk.jobData.jobHandle = ChunkMeshGen.ScheduleGenVoxelsJob(chunk.pos, chunk.chunkData);
			}

			chunk.jobData.jobData.voxelStorage.Pin();

			var dependancies = new NativeArray<JobHandle>(_neighbors.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
			
			for (int i = 0; i < _neighbors.Length; ++i) {
				var neighbor = _neighbors[i];
				chunk.jobData.neighbors[i] = neighbor;

				if (neighbor != null) {
					AddRef(neighbor);
					neighbor.chunkData.Pin();
					chunk.jobData.jobData.neighbors[i] = ChunkMeshGen.PinnedChunkData_t.New(neighbor.chunkData);
					dependancies[i] = neighbor.jobData != null ? neighbor.jobData.jobHandle : default(JobHandle);
				} else {
					chunk.jobData.jobData.neighbors[i] = default(ChunkMeshGen.PinnedChunkData_t);
					dependancies[i] = default(JobHandle);
				}
			}

			chunk.jobData.jobHandle = ChunkMeshGen.ScheduleGenTrisJob(ref chunk.jobData.jobData, JobHandle.CombineDependencies(dependancies));
			dependancies.Dispose();

			if (!existingJob) {
				QueueJobData(chunk.jobData);
			}

			return true;
		}

		void CompleteJobs(int maxTimeMicroseconds) {
			ChunkJobData prev = null;
			ChunkJobData next;

			var endTime = Utils.ReadMicroseconds() + maxTimeMicroseconds;

			for (var job = _usedJobData; job != null; job = next) {
				next = job.next;

				if (job.jobHandle.IsCompleted) {
					job.jobHandle.Complete();

					CompleteJob(job);

					if (prev != null) {
						prev.next = job.next;
					} else {
						_usedJobData = job.next;
					}
					
					job.next = _freeJobData;
					_freeJobData = job;

					var timestamp = Utils.ReadMicroseconds();
					if (timestamp >= endTime) {
						break;
					}
				} else {
					prev = job;
				}
			}
		}

		void CompleteJob(ChunkJobData job) {
			var chunk = job.chunk;

			chunk.jobData = null;
			chunk.hasVoxelData = true;

			if ((job.flags & EJobFlags.TRIS) != 0) {
				job.jobData.voxelStorage.Unpin();
				chunk.hasTrisData = true;
				for (int i = 0; i < job.neighbors.Length; ++i) {
					var neighbor = job.neighbors[i];
					if (neighbor != null) {
						neighbor.chunkData.Unpin();
						Release(neighbor);
					}
				}
			} else {
				chunk.chunkData.Unpin();
				Release(chunk);
			}

			job.chunk = null;
			job.jobHandle = default(JobHandle);
			
			if (chunk.refCount > 0) {
				if (chunk.hasTrisData) {
					CopyToMesh(job, chunk);
				}
			}
		}

		void CopyToMesh(ChunkJobData jobData, Chunk chunk) {
			if ((_terrainRoot != null) && (jobData.jobData.outputVerts.counts[0] > 0)) {
				if (chunk.goChunk == null) {
					chunk.goChunk = GameObject.Instantiate(_chunkPrefab, WorldToVec3(ChunkToWorld(chunk.pos)), Quaternion.identity, _terrainRoot.transform);
				}

				ChunkMeshGen.CopyToMesh(ref jobData.jobData, chunk.goChunk.meshFilter.mesh);
				chunk.goChunk.meshCollider.sharedMesh = chunk.goChunk.meshFilter.mesh;
			}
		}

		public void Dispose() {
			while (_streamingVolumes.Count > 0) {
				_streamingVolumes[0].Dispose();
			}

			Flush();
			
			for (var genData = _freeJobData; genData != null; genData = genData.next) {
				genData.Dispose();
			}

			if (_terrainRoot != null) {
				Utils.DestroyGameObject(_terrainRoot);
			}
		}

		void AddRef(Chunk chunk) {
			++chunk.refCount;
		}

		void Release(Chunk chunk) {
			--chunk.refCount;
			if (chunk.refCount == 0) {
				DestroyChunk(chunk);
			}
		}

		public bool loading {
			get {
				return _loading;
			}
		}

		class Chunk : HChunk {
			public Chunk hashNext;
			public Chunk hashPrev;
			public WorldChunkPos_t pos;
			public int refCount;
			public uint hash;
			public bool hasVoxelData;
			public bool hasTrisData;
			public ChunkMeshGen.ChunkData_t chunkData;
			public ChunkJobData jobData;
			public World_ChunkComponent goChunk;

			public bool GetVoxelAt(LocalVoxelPos_t pos, out EVoxelBlockType blocktype) {
				if (hasVoxelData) {
					var idx = pos.vx + (pos.vz * VOXEL_CHUNK_SIZE_XZ) + (pos.vy * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
					blocktype = chunkData.blocktypes[idx];
					return true;
				}
				blocktype = EVoxelBlockType.AIR;
				return false;
			}
		};

		[Flags]
		enum EJobFlags {
			VOXELS = 0x1,
			TRIS = 0x2
		};

		class ChunkJobData : IDisposable {
			public ChunkJobData next;
			public Chunk chunk;
			public Chunk[] neighbors = new Chunk[27];
			public ChunkMeshGen.JobInputData jobData = ChunkMeshGen.JobInputData.New();
			public EJobFlags flags;
			public JobHandle jobHandle;

			public void Dispose() {
				jobData.Dispose();
			}
		};

		class VolumeData : Volume {
			public struct ChunkRef_t {
				public Chunk chunk;
				public uint sort;
			};

			public ChunkRef_t[] chunks;
			public ChunkRef_t[] tempChunks;
			public int loadNext;
			public int count;
			public bool loading;
			public int xzSize;
			public int ySize;
			public WorldChunkPos_t curPos;
			public WorldChunkPos_t nextPos;
			public Streaming streaming;

			int Volume.xzSize {
				get {
					return xzSize;
				}
			}

			int Volume.ySize {
				get {
					return ySize;
				}
			}

			WorldChunkPos_t Volume.position {
				get {
					return nextPos;
				}
				set {
					nextPos = value;
				}
			}

			public void Move() {
				if (nextPos == curPos) {
					return;
				}

				loading = true;
				curPos = nextPos;
				loadNext = 0;

				Array.Copy(chunks, tempChunks, count);
				var prevCount = count;
				count = 0;

				var zPitch = MaxVoxelChunkLine(xzSize);
				var yDim = MaxVoxelChunkLine(ySize);

				var xorg = curPos.cx - xzSize;
				var yorg = curPos.cy - ySize;
				var zorg = curPos.cz - xzSize;
								
				for (int y = 0; y < yDim; ++y) {

					var yc = yorg + y;
					uint dy = (uint)Mathf.Abs(yc - curPos.cy);
					
					for (int z = 0; z < zPitch; ++z) {

						var zc = zorg + z;
						uint dz = (uint)Mathf.Abs(zc - curPos.cz);
						
						for (int x = 0; x < zPitch; ++x) {

							var xc = xorg + x;
							uint dx = (uint)Mathf.Abs(xc - curPos.cx);
							
							var sort = dx * dx + dy * dy + dz + dz;

							WorldChunkPos_t chunkPos = new WorldChunkPos_t(xc, yc, zc);

							var chunk = streaming.FindOrCreateChunk(chunkPos);
							
							if (chunk != null) {
								chunks[count++] = new ChunkRef_t {
									chunk = chunk,
									sort = sort
								};
							}
						}
					}
				}

				Array.Sort(chunks, (x, y) => x.sort.CompareTo(y.sort));

				for (int i = 0; i < prevCount; ++i) {
					streaming.Release(tempChunks[i].chunk);
				}
			}

			public void Dispose() {
				for (int i = 0; i < chunks.Length; ++i) {
					var chunkRef = chunks[i];
					if (chunkRef.chunk != null) {
						streaming.Release(chunkRef.chunk);
					}
				}
				streaming._streamingVolumes.Remove(this);
			}
		};

		public interface Volume : IDisposable {
			WorldChunkPos_t position { get; set; }
			int xzSize { get; }
			int ySize { get; }
		};

		public interface HChunk {
			bool GetVoxelAt(LocalVoxelPos_t pos, out EVoxelBlockType blocktype);
		};

		public struct ChunkRef_t : IDisposable {
			public HChunk chunk;
			public Streaming streaming;

			public void Dispose() {
				streaming.Release((Chunk)chunk);
				streaming = null;
				chunk = null;
			}
		};

		public ChunkRef_t GetChunkRef(WorldChunkPos_t pos) {
			var chunk = FindChunk(pos);
			if (chunk != null) {
				AddRef(chunk);
			}
			return new ChunkRef_t() {
				chunk = chunk,
				streaming = (chunk != null) ? this : null
			};
		}
		
		public bool GetVoxelAt(WorldVoxelPos_t pos, out EVoxelBlockType blocktype) {
			var wpos = WorldToChunk(pos);

			var chunk = FindChunk(wpos);
			if (chunk != null) {
				return chunk.GetVoxelAt(WorldToLocalVoxel(pos), out blocktype);
			}

			blocktype = EVoxelBlockType.AIR;
			return false;
		}
		
		static uint GetChunkPosHash(uint cx, uint cy, uint cz) {
			var hx = cx & (CHUNK_HASH_SIZE_XZ - 1);
			var hy = cy & (CHUNK_HASH_SIZE_Y - 1);
			var hz = cx & (CHUNK_HASH_SIZE_XZ - 1);
			return hx + (hz* CHUNK_HASH_SIZE_XZ) + (hy* CHUNK_HASH_SIZE_XZ * CHUNK_HASH_SIZE_XZ);
		}

		static uint GetChunkPosHash(WorldChunkPos_t pos) {
			const uint H = uint.MaxValue / 2;

			uint cx = (pos.cx < 0) ? (H - ((uint)(-pos.cx))) : (uint)(H + pos.cx);
			uint cy = (pos.cy < 0) ? (H - ((uint)(-pos.cy))) : (uint)(H + pos.cy);
			uint cz = (pos.cz < 0) ? (H - ((uint)(-pos.cz))) : (uint)(H + pos.cz);

			return GetChunkPosHash(cx, cy, cz);
		}

		Chunk FindChunk(WorldChunkPos_t pos) {
			var hash = GetChunkPosHash(pos);

			for (var chunk = _chunkHash[hash]; chunk != null; chunk = chunk.hashNext) {
				if (chunk.pos == pos) {
					return chunk;
				}
			}

			return null;
		}

		Chunk FindChunk(WorldChunkPos_t pos, int cx, int cy, int cz) {
			return FindChunk(new WorldChunkPos_t(pos.cx + cx, pos.cy + cy, pos.cz + cz));
		}

		void RemoveChunkFromHash(Chunk chunk) {
			if (chunk.hashPrev != null) {
				chunk.hashPrev.hashNext = chunk.hashNext;
			} else {
				_chunkHash[chunk.hash] = chunk.hashNext;
			}

			if (chunk.hashNext != null) {
				chunk.hashNext.hashPrev = chunk.hashPrev;
			}
		}

		void AddChunkToHash(Chunk chunk) {
			var next = _chunkHash[chunk.hash];

			chunk.hashPrev = null;
			chunk.hashNext = next;

			if (next != null) {
				next.hashPrev = chunk;
			}

			_chunkHash[chunk.hash] = chunk;
		}

		Chunk FindOrCreateChunk(WorldChunkPos_t pos) {
			var chunk = FindChunk(pos);
			if (chunk != null) {
				AddRef(chunk);
				return chunk;
			}

			chunk = _chunkPool.GetObject();
			chunk.pos = pos;
			chunk.hash = GetChunkPosHash(pos);
			chunk.hasTrisData = false;
			chunk.hasVoxelData = false;

			if (chunk.chunkData.blocktypes == null) {
				chunk.chunkData = ChunkMeshGen.ChunkData_t.New();
			}

			AddChunkToHash(chunk);
			chunk.refCount = 1;
			return chunk;
		}

		void DestroyChunk(Chunk chunk) {
			RemoveChunkFromHash(chunk);
			if (chunk.goChunk != null) {
				Utils.DestroyGameObject(chunk.goChunk.gameObject);
				chunk.goChunk = null;
			}
			_chunkPool.ReturnObject(chunk);
		}
	}
}
