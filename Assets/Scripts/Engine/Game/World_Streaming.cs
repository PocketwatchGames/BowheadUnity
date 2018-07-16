// Copyright (c) 2018 Pocketwatch Games LLC.

#if UNITY_EDITOR
#define DEBUG_DRAW
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;

public partial class World {
	public sealed class Streaming : IDisposable {

		public enum EAsyncChunkReadResult {
			Pending,
			Success,
			Error
		};

		public interface IAsyncChunkIO : IDisposable {
			EAsyncChunkReadResult result { get; }
			IChunkIO chunkIO { get; }
		};

#if UNITY_EDITOR
		const int MAX_STREAMING_CHUNKS = 4;
		static bool _debugDraw;
		static bool _loadedPrefs;

		[MenuItem("Bowhead/Options/Debug Chunk Display")]
		static void MenuToggle() {
			debugDraw = !debugDraw;
		}

		static bool debugDraw {
			get {
				if (!_loadedPrefs) {
					_loadedPrefs = true;
					_debugDraw = EditorPrefs.GetBool("Bowhead_DebugChunkDisplay", false);
					Menu.SetChecked("Bowhead/Options/Debug Chunk Display", _debugDraw);
				}
				return _debugDraw;
			}
			set {
				_debugDraw = value;
				_loadedPrefs = true;
				EditorPrefs.SetBool("Bowhead_DebugChunkDisplay", value);
				Menu.SetChecked("Bowhead/Options/Debug Chunk Display", value);
			}
		}
#else
		const int MAX_STREAMING_CHUNKS = 8;
#endif
		public delegate JobHandle CreateGenVoxelsJobDelegate(WorldChunkPos_t cpos, PinnedChunkData_t chunk);
		public delegate void ChunkGeneratedDelegate(IChunk chunk);
		public delegate IAsyncChunkIO AsyncChunkReadDelegate(IChunkIO chunk);
		public delegate void ChunkWriteDelegate(IChunkIO chunk);

		public event ChunkGeneratedDelegate onChunkVoxelsLoaded;
		public event ChunkGeneratedDelegate onChunkVoxelsUpdated;
		public event ChunkGeneratedDelegate onChunkTrisUpdated;
		public event ChunkGeneratedDelegate onChunkLoaded;
		public event ChunkGeneratedDelegate onChunkUnloaded;

		const uint CHUNK_HASH_SIZE_XZ = VOXEL_CHUNK_SIZE_XZ;
		const uint CHUNK_HASH_SIZE_Y = VOXEL_CHUNK_SIZE_Y;
		const uint CHUNK_HASH_SIZE = CHUNK_HASH_SIZE_XZ * CHUNK_HASH_SIZE_XZ * CHUNK_HASH_SIZE_Y;

		readonly ObjectPool<Chunk> _chunkPool = new ObjectPool<Chunk>(VOXEL_CHUNK_VIS_MAX_XZ*VOXEL_CHUNK_VIS_MAX_XZ*VOXEL_CHUNK_VIS_MAX_Y, 0);
		readonly Chunk[] _chunkHash = new Chunk[CHUNK_HASH_SIZE];
		readonly List<VolumeData> _streamingVolumes = new List<VolumeData>();
		readonly Chunk[] _neighbors = new Chunk[27];
		readonly World_ChunkComponent _chunkPrefab;
		GameObject _terrainRoot;

		ChunkJobData _freeJobData;
		ChunkJobData _usedJobData;
		CreateGenVoxelsJobDelegate _createGenVoxelsJob;
		AsyncChunkReadDelegate _chunkRead;
		ChunkWriteDelegate _chunkWrite;
		bool _loading;
		bool _flush;

		public static void StaticInit() {
			ChunkMeshGen.tableStorage = ChunkMeshGen.TableStorage.New();
			Utils.ReadMicroseconds(); // init statics
			Utils.ReadMilliseconds(); 
		}

		public static void StaticShutdown() {
			ChunkMeshGen.tableStorage.Dispose();
		}

		public static Color32[] blockColors => ChunkMeshGen.tableStorage.blockColorsArray;
		
		public Streaming(World_ChunkComponent chunkPrefab, CreateGenVoxelsJobDelegate createGenVoxelsJob, AsyncChunkReadDelegate chunkRead, ChunkWriteDelegate chunkWrite) {
			_chunkPrefab = chunkPrefab;
			_chunkRead = chunkRead;
			_chunkWrite = chunkWrite;
			_createGenVoxelsJob = createGenVoxelsJob;

			for (int i = 0; i < MAX_STREAMING_CHUNKS; ++i) {
				var chunkData = new ChunkJobData {
					next = _freeJobData
				};
				_freeJobData = chunkData;
			}
		}

		public IVolume NewStreamingVolume(int xzSize, int yUp, int yDown) {
			var zPitch = MaxVoxelChunkLine(xzSize);
			var yPitch = zPitch * zPitch;
			var yDim = MaxVoxelChunkLine(yUp+yDown);

			var numChunks = yPitch * yDim;

			var data = new VolumeData() {
				curPos = new WorldChunkPos_t(int.MaxValue, int.MaxValue, int.MaxValue),
				xzSize = xzSize,
				yUp = yUp,
				yDown = yDown,
				chunks = new VolumeData.ChunkRef_t[numChunks],
				tempChunks = new VolumeData.ChunkRef_t[numChunks],
				streaming = this
			};
			_streamingVolumes.Add(data);
			return data;
		}

		public void Tick() {

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

			CompleteJobs();

#if DEBUG_DRAW
			if (debugDraw) {
				foreach (var volume in _streamingVolumes) {
					for (int i = volume.loadNext; i < volume.count; ++i) {
						var chunk = volume.chunks[i].chunk;
						if ((chunk.jobData == null) && !chunk.hasTrisData) {
							chunk.DebugDrawState();
						}
					}
				}

				for (var job = _usedJobData; job != null; job = job.next) {
					job.chunk.DebugDrawState();
				}
			}
#endif
		}

		void Flush() {
			_flush = true;
			while (_usedJobData != null) {
				var job = _usedJobData;
				job.jobHandle.Complete();
				CompleteJob(job, true);
				_usedJobData = job.next;
				job.next = _freeJobData;
				_freeJobData = job;
			}
			_flush = false;
		}

		public void BeginTravel() {
			Flush();
		}

		public void FinishTravel() {
			_terrainRoot = new GameObject("TerrainRoot");
		}

		bool QueueNextChunk(VolumeData volume) {
			for (int i = volume.loadNext; i < volume.count; ++i) {
				var chunk = volume.chunks[i].chunk;

				if (chunk.hasTrisData) {
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
			jobData.hasSubJob = false;
			chunk.jobData = jobData;

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.GENERATING_VOXELS;
#endif

			jobData.ioRead = _chunkRead?.Invoke(chunk);
			
			if (jobData.ioRead == null) {
				chunk.chunkData.Pin();
				jobData.jobHandle = _createGenVoxelsJob(chunk.pos, ChunkMeshGen.NewPinnedChunkData_t(chunk.chunkData));
			}

			QueueJobData(jobData);
			return true;
		}

		bool ScheduleGenTrisJob(Chunk chunk) {
			bool existingJob = chunk.jobData != null;

			if (existingJob) {
				chunk.jobData.subJobHandle = chunk.jobData.jobHandle;
				chunk.jobData.hasSubJob = true;
			} else {
				chunk.jobData = GetFreeJobData();
				if (chunk.jobData == null) {
					return false;
				}
				chunk.jobData.chunk = chunk;
				chunk.jobData.hasSubJob = false;

				chunk.jobData.ioRead = _chunkRead?.Invoke(chunk);
			}

			if (chunk.jobData.ioRead != null) {
				chunk.jobData.flags = EJobFlags.TRIS;
				if (!chunk.hasVoxelData) {
					chunk.jobData.flags |= EJobFlags.VOXELS;
				}
			} else {
				chunk.jobData.flags = EJobFlags.TRIS;

				if (!(existingJob || chunk.hasVoxelData)) {
					AddRef(chunk);
					chunk.jobData.flags |= EJobFlags.VOXELS;
					chunk.chunkData.Pin();
					chunk.jobData.jobHandle = _createGenVoxelsJob(chunk.pos, ChunkMeshGen.NewPinnedChunkData_t(chunk.chunkData));
					chunk.jobData.subJobHandle = chunk.jobData.jobHandle;
					chunk.jobData.hasSubJob = true;
				} else if (existingJob) {
					chunk.jobData.flags |= EJobFlags.VOXELS;
				}

				chunk.jobData.jobData.voxelStorage.Pin();

				var dependancies = new NativeArray<JobHandle>(_neighbors.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

				for (int i = 0; i < _neighbors.Length; ++i) {
					var neighbor = _neighbors[i];
					chunk.jobData.neighbors[i] = neighbor;

					if (neighbor != null) {
						AddRef(neighbor);
						neighbor.chunkData.Pin();
						chunk.jobData.jobData.neighbors[i] = ChunkMeshGen.NewPinnedChunkData_t(neighbor.chunkData);
						dependancies[i] = neighbor.jobData != null ? neighbor.jobData.jobHandle : default(JobHandle);
					} else {
						chunk.jobData.jobData.neighbors[i] = default(PinnedChunkData_t);
						dependancies[i] = default(JobHandle);
					}
				}

				chunk.jobData.jobHandle = ChunkMeshGen.ScheduleGenTrisJob(ref chunk.jobData.jobData, JobHandle.CombineDependencies(dependancies));
				dependancies.Dispose();
			}

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.GENERATING_TRIS;
#endif

			if (!existingJob) {
				QueueJobData(chunk.jobData);
			}

			return true;
		}

		void CompleteJobs() {
			ChunkJobData prev = null;
			ChunkJobData next;

			for (var job = _usedJobData; job != null; job = next) {
				next = job.next;

				bool jobComplete = false;
				bool jobError = false;

				if (job.ioRead != null) {
					jobComplete = job.ioRead.result == EAsyncChunkReadResult.Success;
					jobError = job.ioRead.result == EAsyncChunkReadResult.Error;
				}  else {
					jobComplete = job.jobHandle.IsCompleted;
					if (jobComplete && ((job.flags&EJobFlags.TRIS) == EJobFlags.TRIS)) {
						job.jobHandle.Complete();
						_chunkWrite?.Invoke(job.chunk);
					}
				}

				if (jobComplete || jobError) {
					job.jobHandle.Complete();

					if (!jobError) {
						QueueJobCompletion(job);
					}

					if (prev != null) {
						prev.next = job.next;
					} else {
						_usedJobData = job.next;
					}

					if (jobError) {
						var flags = job.flags;
						var chunk = job.chunk;
						CompleteJob(job, _flush);
						job.next = _freeJobData;
						_freeJobData = job;

						if (chunk.refCount > 0) {
							// reque the chunk, chunk read should return null because of the error this time.
							if ((flags&EJobFlags.TRIS) == EJobFlags.TRIS) {
								ScheduleGenTrisJob(chunk);
							} else {
								ScheduleGenVoxelsJob(chunk);
							}
						}
					}

				} else {
					if (job.hasSubJob && (job.flags & (EJobFlags.TRIS|EJobFlags.VOXELS)) == (EJobFlags.TRIS|EJobFlags.VOXELS)) {
						// don't wait for tris to notify that we have voxel data.
						if (job.subJobHandle.IsCompleted) {
							job.subJobHandle.Complete();
							job.subJobHandle = default(JobHandle);
							job.hasSubJob = false;
							job.chunk.hasVoxelData = true;
							job.flags &= ~EJobFlags.VOXELS;
							if (!job.chunk.hasLoaded) {
								onChunkVoxelsLoaded?.Invoke(job.chunk);
							}
							onChunkVoxelsUpdated?.Invoke(job.chunk);
							job.chunk.InvokeVoxelsUpdated();
						}
					}

					prev = job;
				}
			}
		}

		class CompleteJobTask : PooledTaskQueueTask<CompleteJobTask> {
			ChunkJobData job;
			Streaming streaming;

			static CompleteJobTask() {
				StaticInit(New, null, 0);
			}

			static CompleteJobTask New() {
				return new CompleteJobTask();
			}

			protected override void OnFlush() {
				Complete(true);
			}

			protected override void OnRun() {
				Complete(false);
			}

			void Complete(bool flush) {
				streaming.CompleteJob(job, flush);
				job.next = streaming._freeJobData;
				streaming._freeJobData = job;
			}

			static public CompleteJobTask New(Streaming streaming, ChunkJobData job) {
				var task = NewTask();
				task.streaming = streaming;
				task.job = job;
				return task;
			}
		};

		void QueueJobCompletion(ChunkJobData job) {
			MainThreadTaskQueue.Queue(CompleteJobTask.New(this, job));
		}

		void CompleteJob(ChunkJobData job, bool flush) {
			var saveFlush = _flush;
			_flush = _flush || flush;

			var chunk = job.chunk;

			chunk.jobData = null;
			chunk.hasVoxelData = true;

			if ((job.flags & EJobFlags.TRIS) != 0) {
				chunk.hasTrisData = true;
				if (job.ioRead == null) {
					job.jobData.voxelStorage.Unpin();
					for (int i = 0; i < job.neighbors.Length; ++i) {
						var neighbor = job.neighbors[i];
						if (neighbor != null) {
							neighbor.chunkData.Unpin();
							Release(neighbor);
						}
					}
				}
			}

			if ((job.flags & EJobFlags.VOXELS) != 0) {
				if (job.ioRead == null) {
					chunk.chunkData.Unpin();
				}
				Release(chunk);
			}

			job.chunk = null;
			job.jobHandle = default(JobHandle);
			job.subJobHandle = default(JobHandle);

			if ((job.ioRead == null) || (job.ioRead.result == EAsyncChunkReadResult.Success)) {
				if (!flush && (chunk.refCount > 0)) {
#if DEBUG_DRAW
					chunk.dbgDraw.state = EDebugDrawState.HAS_VOXELS;
#endif
					if ((job.flags & EJobFlags.VOXELS) != 0) {
						if (!chunk.hasLoaded) {
							onChunkVoxelsLoaded?.Invoke(chunk);
						}
						onChunkVoxelsUpdated?.Invoke(chunk);
						chunk.InvokeVoxelsUpdated();
					}
					if (chunk.hasTrisData) {
						CopyToMesh(job, chunk);
						if (!chunk.hasLoaded) {
							onChunkLoaded?.Invoke(chunk);
						}
						onChunkTrisUpdated?.Invoke(chunk);
						chunk.InvokeTrisUpdated();
					}
				}
			}

			job.ioRead?.Dispose();
			job.ioRead = null;

			_flush = saveFlush;
		}

		void CopyToMesh(ChunkJobData jobData, Chunk chunk) {
			if (_terrainRoot != null) {
				var wpos = WorldToVec3(ChunkToWorld(chunk.pos));
				int baseVertex = 0;
				int baseIndex = 0;

				for (int layer = 0; layer < ChunkLayers.Length; ++layer) {
					if ((chunk.chunkData.flags[0] & ((EChunkFlags)((int)EChunkFlags.LAYER_DEFAULT << layer))) != 0) {
						CreateChunkMesh(ref jobData.jobData, ref chunk.goChunk, wpos, layer, ref baseIndex, ref baseVertex);
					}
				}
			}
		}

		static int[] staticIndices = new int[ushort.MaxValue];
		static Vector3[] staticVec3 = new Vector3[ushort.MaxValue];
		static Color32[] staticColors = new Color32[ushort.MaxValue];

		static T[] Copy<T>(NativeArray<T> src, int size) where T : struct {
			var t = new T[size];
			for (int i = 0; i < size; ++i) {
				t[i] = src[i];
			}
			return t;
		}

		static T[] Copy<T>(T[] dst, NativeArray<T> src, int ofs, int count) where T: struct {
			for (int i = 0; i < count; ++i) {
				dst[i] = src[i+ofs];
			}
			return dst;
		}

		World_ChunkComponent CreateChunkMeshForLayer(ref World_ChunkComponent root, Vector3 pos, int layer) {
			if (root == null) {
				root = GameObject.Instantiate(_chunkPrefab, pos, Quaternion.identity, _terrainRoot.transform);
				root.gameObject.layer = ChunkLayers[layer];
			}
			if (layer == 0) {
				return root;
			}

			var child = root.GetChildComponent<World_ChunkComponent>(ChunkLayerNames[layer]);
			if (child != null) {
				return child;
			}

			child = GameObject.Instantiate(_chunkPrefab, root.transform, false);
			child.gameObject.name = ChunkLayerNames[layer];
			child.gameObject.layer = ChunkLayers[layer];
			return child;
		}

		void DestroyChunkMeshForLayer(ref World_ChunkComponent root, int layer) {
			if (root == null) {
				return;
			}

			if (layer == 0) {
				if (root.transform.childCount == 0) {
					Utils.DestroyGameObject(root.gameObject);
					root = null;
				} else {
					root.Clear();
				}
				return;
			}

			var child = root.GetChildComponent<World_ChunkComponent>(ChunkLayerNames[layer]);
			if (child != null) {
				Utils.DestroyGameObject(child.gameObject);
			}

			if (root.transform.childCount == 0) {
				Utils.DestroyGameObject(root.gameObject);
				root = null;
			}
		}

		void CreateChunkMesh(ref ChunkMeshGen.CompiledChunkData jobData, ref World_ChunkComponent root, Vector3 pos, int layer, ref int baseIndex, ref int baseVertex) {
			var outputVerts = jobData.outputVerts;

			var vertCount = outputVerts.counts[layer*3+0];
			if (vertCount < 1) {
				return;
			}

			var component = CreateChunkMeshForLayer(ref root, pos, layer);
			var mesh = component.mesh;

			mesh.Clear();

			MeshCopyHelper.SetMeshVerts(mesh, Copy(staticVec3, outputVerts.positions, baseVertex, vertCount), vertCount);
			MeshCopyHelper.SetMeshNormals(mesh, Copy(staticVec3, outputVerts.normals, baseVertex, vertCount), vertCount);
			MeshCopyHelper.SetMeshColors(mesh, Copy(staticColors, outputVerts.colors, baseVertex, vertCount), vertCount);

			int submeshidx = 0;
			int indexOfs = 0;

			var maxSubmesh = outputVerts.counts[layer*3+2];

			for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
				int numSubmeshVerts = outputVerts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh];
				if (numSubmeshVerts > 0) {
					MeshCopyHelper.SetSubMeshTris(mesh, submeshidx, Copy(staticIndices, outputVerts.indices, indexOfs+baseIndex, numSubmeshVerts), numSubmeshVerts, true, 0);
					indexOfs += numSubmeshVerts;
					++submeshidx;
				}
			}

			baseVertex += vertCount;
			baseIndex += indexOfs;

			component.UpdateCollider();
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

#if DEBUG_DRAW
		enum EDebugDrawState {
			QUEUED,
			GENERATING_VOXELS,
			HAS_VOXELS,
			GENERATING_TRIS
		};
		
		struct DebugDrawState_t {
			public EDebugDrawState state;
		};
#endif

		class Chunk : IChunkIO {
			public Chunk hashNext;
			public Chunk hashPrev;
			public WorldChunkPos_t pos;
			public int refCount;
			public int genCount;
			public uint hash;
			public bool hasVoxelData;
			public bool hasTrisData;
			public bool hasLoaded;
			public ChunkMeshGen.ChunkData_t chunkData;
			public ChunkJobData jobData;
			public World_ChunkComponent goChunk;

			bool IChunk.hasVoxelData => hasVoxelData;
			bool IChunk.hasTrisData => hasTrisData;
			bool IChunk.isGenerating => (jobData != null) || ((genCount > 0) && !hasTrisData);
			WorldChunkPos_t IChunk.chunkPos => pos;
			Voxel_t[] IChunk.voxeldata => chunkData.voxeldata;
			EChunkFlags IChunk.flags => chunkData.flags[0];
			Decoration_t[] IChunk.decorations => chunkData.decorations;
			int IChunk.decorationCount => chunkData.decorationCount[0];

			EChunkFlags IChunkIO.flags {
				get {
					return chunkData.flags[0];
				}
				set {
					chunkData.flags[0] = value;
				}
			}

			ChunkMeshGen.FinalMeshVerts_t IChunkIO.verts => jobData.jobData.outputVerts;

			public event ChunkGeneratedDelegate onChunkVoxelsLoaded;
			public event ChunkGeneratedDelegate onChunkVoxelsUpdated;
			public event ChunkGeneratedDelegate onChunkTrisUpdated;
			public event ChunkGeneratedDelegate onChunkLoaded;
			public event ChunkGeneratedDelegate onChunkUnloaded;

			public void InvokeVoxelsUpdated() {
				if (!hasLoaded) {
					onChunkVoxelsLoaded?.Invoke(this);
				}
				onChunkVoxelsUpdated?.Invoke(this);
			}

			public void InvokeTrisUpdated() {
				if (!hasLoaded) {
					onChunkLoaded?.Invoke(this);
					hasLoaded = true;
				}
				onChunkTrisUpdated?.Invoke(this);
			}

			public void InvokeChunkUnloaded() {
				onChunkUnloaded?.Invoke(this);
			}

#if DEBUG_DRAW
			public DebugDrawState_t dbgDraw;

			public void DebugDrawState() {
				var lpos = WorldToVec3(ChunkToWorld(pos));

				Color c;
				switch (dbgDraw.state) {
					default: return;
					case EDebugDrawState.QUEUED:
						c = Color.grey;
					break;
					case EDebugDrawState.GENERATING_VOXELS:
						c = Color.red;
					break;
					case EDebugDrawState.HAS_VOXELS:
						c = Color.cyan;
					break;
					case EDebugDrawState.GENERATING_TRIS:
						c = Color.yellow;
					break;
				}

				Utils.DebugDrawBox(lpos, lpos + new Vector3(VOXEL_CHUNK_SIZE_XZ, VOXEL_CHUNK_SIZE_Y, VOXEL_CHUNK_SIZE_XZ), c, 0, true);
			}
#endif

			public bool GetVoxelAt(LocalVoxelPos_t pos, out Voxel_t voxel) {
				if (hasVoxelData) {
					var idx = pos.vx + (pos.vz * VOXEL_CHUNK_SIZE_XZ) + (pos.vy * VOXEL_CHUNK_SIZE_XZ * VOXEL_CHUNK_SIZE_XZ);
					voxel = chunkData.voxeldata[idx];
					return true;
				}
				voxel = default(Voxel_t);
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
			public ChunkMeshGen.CompiledChunkData jobData = ChunkMeshGen.CompiledChunkData.New();
			public EJobFlags flags;
			public JobHandle jobHandle;
			public JobHandle subJobHandle;
			public IAsyncChunkIO ioRead;
			public bool hasSubJob;

			public void Dispose() {
				jobData.Dispose();
			}
		};

		class VolumeData : IVolume, IComparer<VolumeData.ChunkRef_t> {
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
			public int yUp, yDown;
			public WorldChunkPos_t curPos;
			public WorldChunkPos_t nextPos;
			public Streaming streaming;

			int IVolume.xzSize {
				get {
					return xzSize;
				}
			}

			int IVolume.yUp {
				get {
					return yUp;
				}
			}

			int IVolume.yDown {
				get {
					return yDown;
				}
			}

			WorldChunkPos_t IVolume.position {
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
				var yDim = MaxVoxelChunkLine(yUp+yDown);

				var xorg = curPos.cx - xzSize;
				var yorg = curPos.cy - yDown;
				var zorg = curPos.cz - xzSize;

				var yScale = (uint)(Mathf.Max(VOXEL_CHUNK_SIZE_Y, VOXEL_CHUNK_SIZE_XZ) / VOXEL_CHUNK_SIZE_XZ);
								
				for (int y = 0; y < yDim; ++y) {

					var yc = yorg + y;
					uint dy = (uint)Mathf.Abs(yc - curPos.cy) * yScale;
					
					for (int z = 0; z < zPitch; ++z) {

						var zc = zorg + z;
						uint dz = (uint)Mathf.Abs(zc - curPos.cz);
						
						for (int x = 0; x < zPitch; ++x) {

							var xc = xorg + x;
							uint dx = (uint)Mathf.Abs(xc - curPos.cx);
							
							var sort = dx * dx + dy * dy + dz * dz;

							WorldChunkPos_t chunkPos = new WorldChunkPos_t(xc, yc, zc);

							var chunk = streaming.FindOrCreateChunk(chunkPos);
							
							if (chunk != null) {
								++chunk.genCount;
								chunks[count++] = new ChunkRef_t {
									chunk = chunk,
									sort = sort
								};
							}
						}
					}
				}

				Array.Sort(chunks, 0, count, this);

				for (int i = 0; i < prevCount; ++i) {
					var chunk = tempChunks[i].chunk;
					--chunk.genCount;
					streaming.Release(chunk);
				}
			}

			public void Dispose() {
				for (int i = 0; i < chunks.Length; ++i) {
					var chunkRef = chunks[i];
					if (chunkRef.chunk != null) {
						--chunkRef.chunk.genCount;
						streaming.Release(chunkRef.chunk);
					}
				}
				streaming._streamingVolumes.Remove(this);
			}

			public int Compare(ChunkRef_t x, ChunkRef_t y) {
				return x.sort.CompareTo(y.sort);
			}
		};

		public interface IVolume : IDisposable {
			WorldChunkPos_t position { get; set; }
			int xzSize { get; }
			int yUp { get; }
			int yDown { get; }
		};

		public interface IChunk {
			bool GetVoxelAt(LocalVoxelPos_t pos, out Voxel_t voxel);
			bool hasVoxelData { get; }
			bool hasTrisData { get; }
			bool isGenerating { get; }
			EChunkFlags flags { get; }
			WorldChunkPos_t chunkPos { get; }
			Voxel_t[] voxeldata { get; }
			Decoration_t[] decorations { get; }
			int decorationCount { get; }
			event ChunkGeneratedDelegate onChunkVoxelsLoaded;
			event ChunkGeneratedDelegate onChunkVoxelsUpdated;
			event ChunkGeneratedDelegate onChunkTrisUpdated;
			event ChunkGeneratedDelegate onChunkLoaded;
			event ChunkGeneratedDelegate onChunkUnloaded;
		};

		public interface IChunkIO : IChunk {
			EChunkFlags flags { get; set; }
			ChunkMeshGen.FinalMeshVerts_t verts { get; }
		};

		public IChunk GetChunk(WorldChunkPos_t pos) {
			var chunk = FindChunk(pos);
			if (chunk != null) {
				AddRef(chunk);
			}
			return chunk;
		}

		public void AddRef(IChunk chunk) {
			AddRef((Chunk)chunk);
		}

		public void Release(IChunk chunk) {
			Release((Chunk)chunk);
		}
		
		public bool GetVoxelAt(WorldVoxelPos_t pos, out Voxel_t voxel) {
			var cpos = WorldToChunk(pos);

			var chunk = FindChunk(cpos);
			if (chunk != null) {
				return chunk.GetVoxelAt(WorldToLocalVoxel(pos), out voxel);
			}

			voxel = default(Voxel_t);
			return false;
		}
		
		static uint GetChunkPosHash(uint cx, uint cy, uint cz) {
			var hx = cx & (CHUNK_HASH_SIZE_XZ - 1);
			var hy = cy & (CHUNK_HASH_SIZE_Y - 1);
			var hz = cz & (CHUNK_HASH_SIZE_XZ - 1);
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
			chunk.hasLoaded = false;
			chunk.genCount = 0;

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.QUEUED;
#endif

			if (chunk.chunkData.voxeldata == null) {
				chunk.chunkData = ChunkMeshGen.ChunkData_t.New();
			}

			chunk.chunkData.flags[0] = EChunkFlags.NONE;
			chunk.chunkData.decorationCount[0] = 0;

			AddChunkToHash(chunk);
			chunk.refCount = 1;
			return chunk;
		}

		void DestroyChunk(Chunk chunk) {
			if (!_flush) {
				onChunkUnloaded?.Invoke(chunk);
				chunk.InvokeChunkUnloaded();
			}

			RemoveChunkFromHash(chunk);
			if (chunk.goChunk != null) {
				Utils.DestroyGameObject(chunk.goChunk.gameObject);
				chunk.goChunk = null;
			}
			_chunkPool.ReturnObject(chunk);
		}
	}
}
