// Copyright (c) 2018 Pocketwatch Games LLC.

using Unity.Jobs;
using UnityEngine;

public class TerrainTestMono : MonoBehaviour {
	[SerializeField]
	World_ChunkComponent _chunkPrefab;
	[SerializeField]
	bool _tick;
	[SerializeField]
	Bowhead.WorldStreaming.EGenerator _generator;

	World.Streaming _streaming;
	World.Streaming.IVolume _volume;
	Bowhead.WorldStreaming.IWorldStreaming _chunkStreaming;

	// Use this for initialization
	void Start () {
		World.Streaming.StaticInit();
		MainThreadTaskQueue.maxFrameTimeMicroseconds = 4000;

		_chunkStreaming = Bowhead.WorldStreaming.NewProceduralWorldStreaming(0, _generator);

		_tick = true;
		_streaming = new World.Streaming(_chunkPrefab, CreateGenVoxelsJob, null, null);
		_volume = _streaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y_UP, World.VOXEL_CHUNK_VIS_MAX_Y_DOWN);
		_volume.position = default(WorldChunkPos_t);

		_streaming.FinishTravel();
	}

	JobHandle CreateGenVoxelsJob(WorldChunkPos_t pos, World.PinnedChunkData_t chunk) {
		return _chunkStreaming.ScheduleChunkGenerationJob(pos, chunk, true);
	}

	void Update() {
		if (_tick) {
			_streaming.Tick();
		}

		MainThreadTaskQueue.Run();
	}

	void OnDestroy() {
		MainThreadTaskQueue.Flush();
		_chunkStreaming.Dispose();
		_volume.Dispose();
		_streaming.Dispose();
		World.Streaming.StaticShutdown();
	}
}
