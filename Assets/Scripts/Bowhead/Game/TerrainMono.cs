using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainMono : MonoBehaviour {
	[SerializeField]
	World_ChunkComponent _chunkPrefab;

	World.Streaming _streaming;
	World.Streaming.Volume _volume;

	// Use this for initialization
	void Start () {
		World.Streaming.StaticInit();

		_streaming = new World.Streaming(_chunkPrefab);
		_volume = _streaming.NewStreamingVolume(0, 0);
		_volume.position = new World.WorldChunkPos_t(0, 0, 0);
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
