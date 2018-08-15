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
using Unity.Collections.LowLevel.Unsafe;

public partial class World {
	public sealed class Streaming : IDisposable {

		public struct CountersTotal_t {
			public uint completedJobs;
			public long chunksGenerated;
			public long totalTime;
			public long copyTime;
			public ChunkTimingData_t chunkTiming;
		};

		public struct CountersThisFrame_t {
			public uint submittedJobs;
			public uint pendingJobs;
			public uint completedJobs;
			public uint chunksGenerated;
			public uint chunksCopiedToScene;
			public long chunkSceneCopyTime;
			public ChunkTimingData_t chunkTiming;
		};

		static class ShaderID {
			public static readonly int _AlbedoTextureArrayIndices = Shader.PropertyToID("_AlbedoTextureArrayIndices");
			public static readonly int _AlbedoTextureArray = Shader.PropertyToID("_AlbedoTextureArray");
			public static readonly int _NormalsTextureArrayIndices = Shader.PropertyToID("_NormalsTextureArrayIndices");
			public static readonly int _NormalsTextureArray = Shader.PropertyToID("_NormalsTextureArray");
			//public static readonly int _RoughnessTextureArrayIndices = Shader.PropertyToID("_RoughnessTextureArrayIndices");
			//public static readonly int _RoughnessTextureArray = Shader.PropertyToID("_RoughnessTextureArray");
			//public static readonly int _AOTextureArrayIndices = Shader.PropertyToID("_AOTextureArrayIndices");
			//public static readonly int _AOTextureArray = Shader.PropertyToID("_AOTextureArray");
			//public static readonly int _HeightTextureArrayIndices = Shader.PropertyToID("_HeightTextureArrayIndices");
			//public static readonly int _HeightTextureArray = Shader.PropertyToID("_HeightTextureArray");
			public static readonly int _RHOTextureArrayIndices = Shader.PropertyToID("_RHOTextureArrayIndices");
			public static readonly int _RHOTextureArray = Shader.PropertyToID("_RHOTextureArray");
		};

		public CountersThisFrame_t countersThisFrame;
		public CountersTotal_t countersTotal;

		public enum EAsyncChunkReadResult {
			Pending,
			Success,
			Error
		};

		public interface IMMappedChunkData : IDisposable {
			unsafe byte* chunkData { get; }
			int chunkDataLen { get; }
			EChunkFlags chunkFlags { get; }
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
		const int MAX_STREAMING_CHUNKS = 16;
#endif
		public delegate JobHandle CreateGenVoxelsJobDelegate(WorldChunkPos_t cpos, PinnedChunkData_t chunk);
		public delegate void ChunkGeneratedDelegate(IChunk chunk);
		public delegate IMMappedChunkData MMapChunkDataDelegate(IChunk chunk);
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
		readonly WorldChunkComponent _chunkPrefab;
		GameObject _terrainRoot;

		ChunkJobData _freeJobData;
		ChunkJobData _usedJobData;
		CreateGenVoxelsJobDelegate _createGenVoxelsJob;
		MMapChunkDataDelegate _chunkRead;
		ChunkWriteDelegate _chunkWrite;
		NativeArray<int> _blockMaterialIndices;
		WorldAtlasClientData _clientData;
		WorldAtlas.RenderMaterials_t _materials;
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
		
		public Streaming(WorldChunkComponent chunkPrefab, CreateGenVoxelsJobDelegate createGenVoxelsJob, MMapChunkDataDelegate chunkRead, ChunkWriteDelegate chunkWrite) {
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

		public void SetWorldAtlasClientData(WorldAtlasClientData clientData) {
			DisposeClientData();

			_clientData = clientData;

			if ((clientData.block2TextureSet != null) && (clientData.block2TextureSet.Length > 0)) {
				_blockMaterialIndices = new NativeArray<int>(clientData.block2TextureSet, Allocator.Persistent);
			}
			
			_materials.solid = new Material(clientData.renderMaterials.solid);
			_materials.water = new Material(clientData.renderMaterials.water);

			SetMaterialTextureArray(ShaderID._AlbedoTextureArray, clientData.albedo.textureArray);
			SetMaterialTextureArray(ShaderID._NormalsTextureArray, clientData.normals.textureArray);
			//SetMaterialTextureArray(ShaderID._RoughnessTextureArray, clientData.roughness.textureArray);
			//SetMaterialTextureArray(ShaderID._AOTextureArray, clientData.ao.textureArray);
			//SetMaterialTextureArray(ShaderID._HeightTextureArray, clientData.height.textureArray);
			SetMaterialTextureArray(ShaderID._RHOTextureArray, clientData.rho.textureArray);
		}

		void DisposeClientData() {
			if (_blockMaterialIndices.IsCreated) {
				_blockMaterialIndices.Dispose();
			}

			if (_materials.solid) {
				GameObject.Destroy(_materials.solid);
			}

			if (_materials.water) {
				GameObject.Destroy(_materials.water);
			}
		}

		void SetMaterialTextureArray(int name, Texture2DArray texArray) {
			_materials.solid.SetTexture(name, texArray);
			_materials.water.SetTexture(name, texArray);
		}

		long lastTick;

		public void Tick() {

			var tick = Utils.ReadTimestamp();

			if (lastTick == 0) {
				lastTick = tick;
			}

			var deltaTick = tick - lastTick;

			lastTick = tick;

			if (_usedJobData != null) {
				countersTotal.totalTime += deltaTick;
			}
			
			countersThisFrame = default(CountersThisFrame_t);
			
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

			for (var j = _usedJobData; j != null; j = j.next) {
				++countersThisFrame.pendingJobs;
			}

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
						if (chunk.jobData == null) {
							if (TryMMapLoad(chunk)) {
								if (chunk.jobData != null) {
									return true;
								}
							} else {
								return false;
							}
						}

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
														if (TryMMapLoad(neighbor)) {
															if (neighbor.jobData == null) {
																return ScheduleGenVoxelsJob(neighbor);
															}
														} else {
															return false;
														}
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

		bool TryMMapLoad(Chunk chunk) {
			if (chunk.didMMap) {
				return true;
			}

			var jobData = GetFreeJobData();
			if (jobData == null) {
				return false;
			}

			chunk.didMMap = true;

			var mmap = _chunkRead?.Invoke(chunk);
			if (mmap == null) {
				jobData.next = _freeJobData;
				_freeJobData = jobData;
				return true;
			}

			AddRef(chunk);

			jobData.flags = EJobFlags.VOXELS|EJobFlags.TRIS;
			jobData.chunk = chunk;
			jobData.hasSubJob = false;
			chunk.jobData = jobData;

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.GENERATING_VOXELS;
#endif

			jobData.mmappedChunkData = mmap;

			chunk.chunkData.Pin();

			chunk.chunkData.flags[0] = jobData.mmappedChunkData.chunkFlags;
			unsafe {
				jobData.jobHandle = new DecompressChunkDataJob_t() {
					ptr = jobData.mmappedChunkData.chunkData,
					len = jobData.mmappedChunkData.chunkDataLen,
					verts = jobData.jobData.outputVerts,
					chunk = ChunkMeshGen.NewPinnedChunkData_t(chunk.chunkData)
				}.Schedule();
			}

			QueueJobData(jobData);
			return true;
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
			++countersThisFrame.submittedJobs;
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

			{
				var timing = default(ChunkTimingData_t);
				timing.latency = Utils.ReadTimestamp();
				chunk.chunkData.timing[0] = timing;
			}

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.GENERATING_VOXELS;
#endif

			chunk.chunkData.Pin();
			jobData.jobHandle = _createGenVoxelsJob(chunk.pos, ChunkMeshGen.NewPinnedChunkData_t(chunk.chunkData));
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
			}
			
			chunk.jobData.flags = EJobFlags.TRIS;

			if (!(existingJob || chunk.hasVoxelData)) {
				AddRef(chunk);
				var timing = default(ChunkTimingData_t);
				timing.latency = Utils.ReadTimestamp();
				chunk.chunkData.timing[0] = timing;
				chunk.jobData.flags |= EJobFlags.VOXELS;
				chunk.chunkData.Pin();
				chunk.jobData.jobHandle = _createGenVoxelsJob(chunk.pos, ChunkMeshGen.NewPinnedChunkData_t(chunk.chunkData));
				chunk.jobData.subJobHandle = chunk.jobData.jobHandle;
				chunk.jobData.hasSubJob = true;
			} else if (existingJob) {
				chunk.jobData.flags |= EJobFlags.VOXELS;
			} else { // just measure timing from tris gen
				var timing = chunk.chunkData.timing[0];
				timing.latency = Utils.ReadTimestamp();
				chunk.chunkData.timing[0] = timing;
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

			unsafe {
				chunk.jobData.jobHandle = ChunkMeshGen.ScheduleGenTrisJob(ref chunk.jobData.jobData, chunk.chunkData.pinnedTimingData, _blockMaterialIndices, JobHandle.CombineDependencies(dependancies));
			}
			dependancies.Dispose();

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

				var jobComplete = job.jobHandle.IsCompleted;
				
				if (jobComplete) {
					job.jobHandle.Complete();

					if ((job.mmappedChunkData == null) && ((job.flags&EJobFlags.TRIS) == EJobFlags.TRIS)) {
						_chunkWrite?.Invoke(job.chunk);
					}

					QueueJobCompletion(job);
					
					if (prev != null) {
						prev.next = job.next;
					} else {
						_usedJobData = job.next;
					}

				} else {
					if (job.hasSubJob && (job.flags & (EJobFlags.TRIS|EJobFlags.VOXELS)) == (EJobFlags.TRIS|EJobFlags.VOXELS)) {
						// don't wait for tris to notify that we have voxel data.
						if (job.subJobHandle.IsCompleted) {
							job.subJobHandle.Complete();
							job.subJobHandle = default(JobHandle);
							job.hasSubJob = false;
							job.chunk.hasVoxelData = true;
							job.chunk.chunkData.Unpin();
							job.flags &= ~EJobFlags.VOXELS;
							Release(job.chunk);
							if (job.chunk.refCount > 0) {
								if (!job.chunk.hasLoaded) {
									onChunkVoxelsLoaded?.Invoke(job.chunk);
								}
								onChunkVoxelsUpdated?.Invoke(job.chunk);
								job.chunk.InvokeVoxelsUpdated();
							}
							++countersThisFrame.completedJobs;
							++countersTotal.completedJobs;
						}
					}

					prev = job;
				}
			}
		}

		unsafe struct DecompressChunkDataJob_t : IJob {
			[NativeDisableUnsafePtrRestriction]
			public byte* ptr;
			public int len;
			public PinnedChunkData_t chunk;
			public ChunkMeshGen.FinalMeshVerts_t verts;

			public void Execute() {
				chunk = WorldFile.DecompressChunkData(ptr, len, chunk, verts);

				unsafe {
					chunk.pinnedDecorationCount[0] = chunk.decorationCount;
					chunk.pinnedFlags[0] = chunk.flags;
				}
			}
		};

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
				if (job.mmappedChunkData == null) {
					job.jobData.voxelStorage.Unpin();
					for (int i = 0; i < job.neighbors.Length; ++i) {
						var neighbor = job.neighbors[i];
						if (neighbor != null) {
							neighbor.chunkData.Unpin();
							Release(neighbor);
						}
					}

					var timing = chunk.chunkData.timing[0];
					timing.latency = Utils.ReadTimestamp() - timing.latency;

					++countersTotal.chunksGenerated;
					countersTotal.chunkTiming += timing;
					++countersThisFrame.chunksGenerated;
					countersThisFrame.chunkTiming += timing;
				}
			}

			if ((job.flags & EJobFlags.VOXELS) != 0) {
				chunk.chunkData.Unpin();
				Release(chunk);
			}

			job.chunk = null;
			job.jobHandle = default(JobHandle);
			job.subJobHandle = default(JobHandle);

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
					var start = Utils.ReadTimestamp();
					CopyToMesh(job, chunk);
					var total = Utils.ReadTimestamp() - start;
					countersThisFrame.chunkSceneCopyTime += total;
					countersTotal.copyTime += total;
					if (!chunk.hasLoaded) {
						onChunkLoaded?.Invoke(chunk);
					}
					onChunkTrisUpdated?.Invoke(chunk);
					chunk.InvokeTrisUpdated();
					++countersThisFrame.chunksCopiedToScene;
				}
			}

			job.mmappedChunkData?.Dispose();
			job.mmappedChunkData = null;

			_flush = saveFlush;

			++countersTotal.completedJobs;
			++countersThisFrame.completedJobs;
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
		static Vector3[] tan2 = new Vector3[ushort.MaxValue];
		static Color32[] staticColors = new Color32[ushort.MaxValue];
		static Vector4[] staticVec4 = new Vector4[ushort.MaxValue];
		static MaterialPropertyBlock staticMaterialProperties = new MaterialPropertyBlock();
		static float[] staticTextureArrayIndices = new float[12];
		
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

		WorldChunkComponent CreateChunkMeshForLayer(ref WorldChunkComponent root, Vector3 pos, int layer) {
			if (root == null) {
				root = GameObject.Instantiate(_chunkPrefab, pos, Quaternion.identity, _terrainRoot.transform);
				root.gameObject.layer = ChunkLayers[layer];
			}
			if (layer == 0) {
				return root;
			}

			var child = root.GetChildComponent<WorldChunkComponent>(ChunkLayerNames[layer]);
			if (child != null) {
				return child;
			}

			child = GameObject.Instantiate(_chunkPrefab, root.transform, false);
			child.gameObject.name = ChunkLayerNames[layer];
			child.gameObject.layer = ChunkLayers[layer];
			return child;
		}

		void DestroyChunkMeshForLayer(ref WorldChunkComponent root, int layer) {
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

			var child = root.GetChildComponent<WorldChunkComponent>(ChunkLayerNames[layer]);
			if (child != null) {
				Utils.DestroyGameObject(child.gameObject);
			}

			if (root.transform.childCount == 0) {
				Utils.DestroyGameObject(root.gameObject);
				root = null;
			}
		}

		float[] SetTextureChannelIndices(ChunkMeshGen.TexBlend_t texBlend, WorldAtlasClientData.TerrainTextureChannel channel) {
			if (texBlend.count > 0) {
				CopyChannelIndices(channel, 0, texBlend.x);
			}
			if (texBlend.count > 1) {
				CopyChannelIndices(channel, 3, texBlend.y);
			}
			if (texBlend.count > 2) {
				CopyChannelIndices(channel, 6, texBlend.z);
			}
			if (texBlend.count > 3) {
				CopyChannelIndices(channel, 9, texBlend.w);
			}
			return staticTextureArrayIndices;
		}

		void CopyChannelIndices(WorldAtlasClientData.TerrainTextureChannel channel, int ofs, int m) {
			staticTextureArrayIndices[ofs+0] = channel.textureSet2ArrayIndex[m*3+0];
			staticTextureArrayIndices[ofs+1] = channel.textureSet2ArrayIndex[m*3+1];
			staticTextureArrayIndices[ofs+2] = channel.textureSet2ArrayIndex[m*3+2];
		}

		void SetMaterialPropertyBlock(ChunkMeshGen.TexBlend_t texBlend) {
			staticMaterialProperties.Clear();

			for (int i = 0; i < staticTextureArrayIndices.Length; ++i) {
				staticTextureArrayIndices[i] = 0;
			}

			staticMaterialProperties.SetFloatArray(ShaderID._AlbedoTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.albedo));
			staticMaterialProperties.SetFloatArray(ShaderID._NormalsTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.normals));
			//staticMaterialProperties.SetFloatArray(ShaderID._RoughnessTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.roughness));
			//staticMaterialProperties.SetFloatArray(ShaderID._AOTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.ao));
			//staticMaterialProperties.SetFloatArray(ShaderID._HeightTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.height));
			staticMaterialProperties.SetFloatArray(ShaderID._RHOTextureArrayIndices, SetTextureChannelIndices(texBlend, _clientData.rho));
		}

		void CreateChunkMesh(ref ChunkMeshGen.CompiledChunkData jobData, ref WorldChunkComponent root, Vector3 pos, int layer, ref int baseIndex, ref int baseVertex) {
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
			MeshCopyHelper.SetMeshUVs(mesh, 0, Copy(staticVec4, outputVerts.textureBlending, baseVertex, vertCount), vertCount);
			
			int submeshidx = 0;
			int indexOfs = 0;

			var maxSubmesh = outputVerts.counts[layer*3+2];

			for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
				int numSubmeshVerts = outputVerts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh];
				if (numSubmeshVerts > 0) {
					++submeshidx;
				}
			}

			mesh.subMeshCount = submeshidx;

			submeshidx = 0;

			for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
				int numSubmeshVerts = outputVerts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh];
				if (numSubmeshVerts > 0) {
					MeshCopyHelper.SetSubMeshTris(mesh, submeshidx, Copy(staticIndices, outputVerts.indices, indexOfs+baseIndex, numSubmeshVerts), numSubmeshVerts, true, 0);
					indexOfs += numSubmeshVerts;
					++submeshidx;
				}
			}

			component.SetSubmeshMaterials(_materials, submeshidx);
			{
				submeshidx = 0;
				for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
					int numSubmeshVerts = outputVerts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh];
					if (numSubmeshVerts > 0) {
						var texBlend = outputVerts.submeshTextures[(layer*MAX_CHUNK_LAYERS)+submesh];
						SetMaterialPropertyBlock(texBlend);
						component.SetPropertyBlock(staticMaterialProperties, submeshidx);
						++submeshidx;
					}
				}
			}

			ComputeAndSetTangentVectors(mesh, outputVerts.positions, outputVerts.normals, outputVerts.indices, baseVertex, vertCount, baseIndex, indexOfs);

			baseVertex += vertCount;
			baseIndex += indexOfs;

			component.UpdateCollider();
		}

		// axial UV
		static Vector2 GetUV(Vector3 p, Vector3 n) {

			if (Mathf.Abs(n.y) > 0.5f) {
				return new Vector2(p.x, p.z);
			} else if (Mathf.Abs(n.x) > 0.5f) {
				return new Vector2(p.z, p.y);
			}

			return new Vector2(p.x, p.y);
		}

		// Based on:
		// http://www.terathon.com/code/tangent.html

		static void ComputeAndSetTangentVectors(Mesh mesh, NativeArray<Vector3> verts, NativeArray<Vector3> normals, NativeArray<int> indices, int baseVertex, int numVerts, int baseIndex, int numIndices) {
			staticVec3.Broadcast(Vector3.zero, 0, numVerts);
			tan2.Broadcast(Vector3.zero, 0, numVerts);

			for (int i = 0; i < numIndices; i += 3) {
				var i1 = indices[baseIndex+i];
				var i2 = indices[baseIndex+i+1];
				var i3 = indices[baseIndex+i+2];

				var v1 = verts[i1+baseVertex];
				var v2 = verts[i2+baseVertex];
				var v3 = verts[i3+baseVertex];

				var w1 = GetUV(v1, normals[i1+baseVertex]);
				var w2 = GetUV(v2, normals[i2+baseVertex]);
				var w3 = GetUV(v3, normals[i3+baseVertex]);

				var x1 = v2.x - v1.x;
				var x2 = v3.x - v1.x;
				var y1 = v2.y - v1.y;
				var y2 = v3.y - v1.y;
				var z1 = v2.z - v1.z;
				var z2 = v3.z - v1.z;

				var s1 = w2.x - w1.x;
				var s2 = w3.x - w1.x;
				var t1 = w2.y - w1.y;
				var t2 = w3.y - w1.y;

				var r = 1 / (s1 * t2 - s2 * t1);

				Vector3 sdir = new Vector3((t2* x1 -t1 * x2) *r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
				Vector3 tdir = new Vector3((s1* x2 -s2 * x1) *r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

				staticVec3[i1] += sdir;
				staticVec3[i2] += sdir;
				staticVec3[i3] += sdir;

				tan2[i1] += tdir;
				tan2[i2] += tdir;
				tan2[i3] += tdir;
			}

			for (int i = 0; i < numVerts; ++i) {
				var n = normals[i+baseVertex];
				var t = staticVec3[i];

				// Gram-Schmidt orthogonalize
				Vector4 v4 = (t - n * Vector3.Dot(n, t)).normalized;

				// Calculate handedness
				// NOTE: This is flipped from Lengyel's implementation, assuming it's a left-handed Unity thing.
				v4.w = (Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f) ? 1f : -1f;

				staticVec4[i] = v4;
			}

			MeshCopyHelper.SetMeshTangents(mesh, staticVec4, numVerts);
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

			DisposeClientData();	
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
			public WorldChunkComponent goChunk;
			public bool didMMap;

			bool IChunk.hasVoxelData => hasVoxelData;
			bool IChunk.hasTrisData => hasTrisData;
			bool IChunk.isGenerating => (jobData != null) || ((genCount > 0) && !hasTrisData);
			WorldChunkPos_t IChunk.chunkPos => pos;
			Voxel_t[] IChunk.voxeldata => chunkData.voxeldata;
			EChunkFlags IChunk.flags => chunkData.flags[0];
			Decoration_t[] IChunk.decorations => chunkData.decorations;
			int IChunk.decorationCount => chunkData.decorationCount[0];
			WorldChunkComponent IChunk.component => goChunk;

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
			public IMMappedChunkData mmappedChunkData;
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

			int IVolume.xzSize => xzSize;
			int IVolume.yUp => yUp;
			int IVolume.yDown => yDown;

			int IVolume.totalChunkCount => count;
			int IVolume.loadedChunkCount => loadNext;

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

				uint yScale = 1;// (uint)(Mathf.Max(VOXEL_CHUNK_SIZE_Y, VOXEL_CHUNK_SIZE_XZ) / VOXEL_CHUNK_SIZE_XZ);
								
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
			int totalChunkCount { get; }
			int loadedChunkCount { get; }
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
			WorldChunkComponent component { get; }
			event ChunkGeneratedDelegate onChunkVoxelsLoaded;
			event ChunkGeneratedDelegate onChunkVoxelsUpdated;
			event ChunkGeneratedDelegate onChunkTrisUpdated;
			event ChunkGeneratedDelegate onChunkLoaded;
			event ChunkGeneratedDelegate onChunkUnloaded;
		};

		public interface IChunkIO : IChunk {
			new EChunkFlags flags { get; set; }
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
			chunk.didMMap = false;
			chunk.genCount = 0;

#if DEBUG_DRAW
			chunk.dbgDraw.state = EDebugDrawState.QUEUED;
#endif

			if (chunk.chunkData.voxeldata == null) {
				chunk.chunkData = ChunkMeshGen.ChunkData_t.New();
			}

			chunk.chunkData.flags[0] = EChunkFlags.NONE;
			chunk.chunkData.decorationCount[0] = 0;
			chunk.chunkData.timing[0] = default(ChunkTimingData_t);

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
