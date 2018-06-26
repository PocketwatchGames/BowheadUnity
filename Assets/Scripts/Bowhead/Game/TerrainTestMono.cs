using System.Collections;
using System.Collections.Generic;
using Server;
using UnityEngine;

public class TerrainTestMono : MonoBehaviour, IGameInstance {
	[SerializeField]
	World_ChunkComponent _chunkPrefab;
	[SerializeField]
	bool _tick;

	World.Streaming _streaming;
	World.Streaming.IVolume _volume;

	public bool isDedicatedServer {
		get {
			throw new System.NotImplementedException();
		}
	}

	public bool isServer => true;
	public bool isClient => true;
	public ServerWorld serverWorld { get { throw new System.NotImplementedException(); } }
	public bool fixedUpdateDidRun { get { throw new System.NotImplementedException(); } }
	
	// Use this for initialization
	void Start () {
		World.Streaming.StaticInit();

		_tick = true;
		_streaming = new World.Streaming(this, _chunkPrefab);
		_volume = _streaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y);
		_volume.position = default(WorldChunkPos_t);
	}

	void Update() {
		if (_tick) {
			_streaming.Tick();
		}
	}

	void OnDestroy() {
		_volume.Dispose();
		_streaming.Dispose();
		World.Streaming.StaticShutdown();
	}
}
