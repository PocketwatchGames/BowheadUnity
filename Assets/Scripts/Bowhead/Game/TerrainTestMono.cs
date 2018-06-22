using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainTestMono : MonoBehaviour {
	[SerializeField]
	World_ChunkComponent _chunkPrefab;

	World.Streaming _streaming;
	World.Streaming.Volume _volume;

	// Use this for initialization
	void Start () {
		World.Streaming.StaticInit();

		_streaming = new World.Streaming(_chunkPrefab);
		_volume = _streaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y);
		_volume.position = default(WorldChunkPos_t);
	}

	void Update() {
		_streaming.Tick();
	}

	void OnDestroy() {
		_volume.Dispose();
		_streaming.Dispose();
		World.Streaming.StaticShutdown();
	}
}
