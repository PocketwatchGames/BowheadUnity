// Copyright (c) 2018 Pocketwatch Games LLC.

using Unity.Jobs;
using UnityEngine;

public class TerrainTestMono : MonoBehaviour {
	[SerializeField]
	WorldChunkComponent _chunkPrefab;
	[SerializeField]
	bool _tick;
	[SerializeField]
	Bowhead.WorldStreaming.EGenerator _generator;
	[SerializeField]
	WorldAtlasClientData _clientData;

	World.Streaming _streaming;
	World.Streaming.IVolume _volume;
	Bowhead.WorldStreaming.IWorldStreaming _chunkStreaming;

	// Use this for initialization
	void Start () {
		World.Streaming.StaticInit();
		MainThreadTaskQueue.maxFrameTimeMicroseconds = int.MaxValue;

		_chunkStreaming = Bowhead.WorldStreaming.NewProceduralWorldStreaming(0, _generator);

		_tick = true;
		_streaming = new World.Streaming(_chunkPrefab, CreateGenVoxelsJob, null, null);
		_streaming.SetWorldAtlasClientData(_clientData);
		_volume = _streaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y_UP, World.VOXEL_CHUNK_VIS_MAX_Y_DOWN);
		_volume.position = default(WorldChunkPos_t);
		_streaming.shaderQualityLevel = World.Streaming.EShaderQualityLevel.HIGH;

		_streaming.FinishTravel();
	}

	JobHandle CreateGenVoxelsJob(WorldChunkPos_t pos, World.PinnedChunkData_t chunk) {
		return _chunkStreaming.ScheduleChunkGenerationJob(pos, chunk);
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

	bool showDebug;

	void OnGUI() {

		if (_streaming != null) {
			if (Event.current.type == EventType.KeyDown) {
				if (Event.current.keyCode == KeyCode.F3) {
					showDebug = !showDebug;
				}
			}

			if (showDebug) {
				_streaming.DrawDebugHUD();
			}
		}
	}
}
