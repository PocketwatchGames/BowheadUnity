// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bowhead.Client.UI {
	using IChunk = World.Streaming.IChunk;

	[RequireComponent(typeof(RawImage))]
	public class Minimap : MonoBehaviourEx {

		class ChunkTile {
			public ChunkTile prev, next;
			public IChunk[] chunks;
			public EVoxelBlockType[] voxelmap;
			public int x, z;
			public uint hash;
			public bool solid;
			public bool dirty;
		};

		const uint HASH_SIZE_XZ = World.VOXEL_CHUNK_SIZE_XZ;
		const uint HASH_SIZE = HASH_SIZE_XZ * HASH_SIZE_XZ;

		ObjectPool<ChunkTile> _tilePool;
		ChunkTile[] _hash;

		ChunkTile[] _tiles;
		int _numTiles;

		[SerializeField]
		int _chunkMinY;
		[SerializeField]
		int _chunkMaxY;
		[SerializeField]
		int _chunkXZSize;
		[SerializeField]
		Material _maskBlitMaterial;
		[SerializeField]
		Texture2D _revealTexture;
		[SerializeField]
		RawImage _maskImage;

		int _chunkX;
		int _chunkZ;
		int _chunkMinX, _chunkMaxX;
		int _chunkMinZ, _chunkMaxZ;
		int _chunkNumY;

		World.Streaming _streaming;
		Texture2D _mainTexture;
		RenderTexture _maskTexture;
		Texture2D _blitTexture;
		Texture2D _blackTexture;
		RawImage _image;
		Color32[] _pixels;
		bool _dirty;

		void Awake() {
			_tilePool = new ObjectPool<ChunkTile>(World.MaxVoxelChunkLine(World.VOXEL_CHUNK_VIS_MAX_XZ)*World.MaxVoxelChunkLine(World.VOXEL_CHUNK_VIS_MAX_XZ));
			_hash = new ChunkTile[HASH_SIZE];
			_tiles = new ChunkTile[World.MaxVoxelChunkLine(_chunkXZSize)*World.MaxVoxelChunkLine(_chunkXZSize)];

			_mainTexture = AddGC(new Texture2D(World.MaxVoxelChunkLine(_chunkXZSize) * World.VOXEL_CHUNK_SIZE_XZ, World.MaxVoxelChunkLine(_chunkXZSize) * World.VOXEL_CHUNK_SIZE_XZ, TextureFormat.ARGB32, false));
			_maskTexture = AddGC(new RenderTexture(World.MaxVoxelChunkLine(_chunkXZSize) * World.VOXEL_CHUNK_SIZE_XZ, World.MaxVoxelChunkLine(_chunkXZSize) * World.VOXEL_CHUNK_SIZE_XZ, 1, RenderTextureFormat.R8, RenderTextureReadWrite.Linear));
			_blitTexture = AddGC(new Texture2D(World.VOXEL_CHUNK_SIZE_XZ, World.VOXEL_CHUNK_SIZE_XZ, TextureFormat.ARGB32, false));
			_blackTexture = AddGC(new Texture2D(World.VOXEL_CHUNK_SIZE_XZ, World.VOXEL_CHUNK_SIZE_XZ, TextureFormat.ARGB32, false));

			_pixels = _blitTexture.GetPixels32();

			_blackTexture.Clear(Color.black);
			_blackTexture.Apply(true, true);

			_mainTexture.Clear(Color.black);
			_mainTexture.Apply(true, true);

			_maskTexture.useMipMap = false;
			_maskTexture.Create();
			Graphics.Blit(_blackTexture, _maskTexture);
									
			_image = GetComponent<RawImage>();
			_image.texture = _mainTexture;

			_maskImage.texture = _maskTexture;
			
			_chunkX = int.MaxValue;
			_chunkZ = int.MaxValue;
			_chunkMinX = int.MaxValue;
			_chunkMaxX = int.MaxValue;
			_chunkMinZ = int.MaxValue;
			_chunkMaxZ = int.MaxValue;

			_chunkNumY = _chunkMaxY - _chunkMinY + 1;
		}

		void Update() {
			if (_dirty) {
				DirtyUpdate();
			}
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			_streaming.onChunkVoxelsUpdated -= OnChunkLoaded;
			_streaming.onChunkUnloaded -= OnChunkUnloaded;
		}

		public void SetStreaming(World.Streaming streaming) {
			_streaming = streaming;
			_streaming.onChunkVoxelsUpdated += OnChunkLoaded;
			_streaming.onChunkUnloaded += OnChunkUnloaded;
		}

		public void SetOrigin(int chunkX, int chunkZ) {
			if ((_chunkX == chunkX) && (_chunkZ == chunkZ)) {
				return;
			}

			_chunkX = chunkX;
			_chunkZ = chunkZ;
			_chunkMinX = _chunkX - _chunkXZSize;
			_chunkMaxX = _chunkX + _chunkXZSize;
			_chunkMinZ = _chunkZ - _chunkXZSize;
			_chunkMaxZ = _chunkZ + _chunkXZSize;

			PurgeOutOfBoundTiles();
			AddBoundedTiles();
			FullUpdate();

			RevealArea(new Vector2(0, 0), 100);
		}

		public void RevealArea(Vector2 pos, float radius) {
			var mapBottomLeft = World.ChunkToWorld(new WorldChunkPos_t(_chunkMinX, 0, _chunkMinZ));
			var mapExtent = World.MaxVoxelChunkLine(_chunkXZSize) * World.VOXEL_CHUNK_SIZE_XZ;
			var relativePos = World.Vec3ToWorld(new Vector3(pos.x, 0, pos.y)) - mapBottomLeft;
						
			GL.sRGBWrite = false;
			Graphics.SetRenderTarget(_maskTexture);
			GL.PushMatrix();
			GL.LoadPixelMatrix(0, mapExtent, 0, mapExtent);
			Graphics.DrawTexture(new Rect(relativePos.vx - radius, relativePos.vz - radius, radius*2, radius*2), _revealTexture, _maskBlitMaterial);
			GL.PopMatrix();
			Graphics.SetRenderTarget(null);
		}

		void FullUpdate() {
			_dirty = false;

			for (int i = 0; i < _numTiles; ++i) {
				RenderTile(_tiles[i]);
			}
		}

		void DirtyUpdate() {
			_dirty = false;

			for (int i = 0; i < _numTiles; ++i) {
				var tile = _tiles[i];
				if (tile.dirty && tile.solid) {
					RenderTile(tile);
				}
			}
		}

		void OnChunkLoaded(IChunk chunk) {
			var pos = chunk.chunkPos;
			if ((pos.cx >= _chunkMinX) && (pos.cx <= _chunkMaxX) && (pos.cy >= _chunkMinY) && (pos.cy <= _chunkMaxY) && (pos.cz >= _chunkMinZ) && (pos.cz <= _chunkMaxZ)) {
				var tile = GetHashedTile(pos.cx, pos.cz);
				if (tile != null) {
					AddChunkToTile(tile, chunk);
				}
			}
		}

		void OnChunkUnloaded(IChunk chunk) {
			var pos = chunk.chunkPos;
			if ((pos.cx >= _chunkMinX) && (pos.cx <= _chunkMaxX) && (pos.cy >= _chunkMinY) && (pos.cy <= _chunkMaxY) && (pos.cz >= _chunkMinZ) && (pos.cz <= _chunkMaxZ)) {
				var tile = GetHashedTile(pos.cx, pos.cz);
				if (tile != null) {
					RemoveChunkFromTile(tile, chunk);
				}
			}
		}

		void RenderTile(ChunkTile tile) {
			if (tile.dirty && tile.solid) {
				tile.voxelmap.Broadcast(EVoxelBlockType.AIR);

				int numSolid = 0;

				for (int i = 0; i < tile.chunks.Length; ++i) {
					var chunk = tile.chunks[i];
					if (chunk != null) {
						if (RenderChunkToTile(tile, chunk, ref numSolid)) {
							break;
						}
					} else {
						break;
					}
				}

				tile.dirty = false;
			}


			var px = tile.x - _chunkMinX;
			var py = tile.z - _chunkMinZ;

			if (tile.solid) {
				RenderTileToBlitTexture(tile);
				BlitToMainTexture(_blitTexture, px * World.VOXEL_CHUNK_SIZE_XZ, py * World.VOXEL_CHUNK_SIZE_XZ);
			} else {
				BlitToMainTexture(_blackTexture, px * World.VOXEL_CHUNK_SIZE_XZ, py * World.VOXEL_CHUNK_SIZE_XZ);
			}
		}

		bool RenderChunkToTile(ChunkTile tile, IChunk chunk, ref int numSolid) {
			var srcVoxels = chunk.voxeldata;
			var dstVoxels = tile.voxelmap;

			for (int y = World.VOXEL_CHUNK_SIZE_Y-1; y >= 0; --y) {
				var ofs = y*World.VOXEL_CHUNK_SIZE_XZ*World.VOXEL_CHUNK_SIZE_XZ;
				for (int z = 0; z < World.VOXEL_CHUNK_SIZE_XZ; ++z) {
					var zofs = z * World.VOXEL_CHUNK_SIZE_XZ;
					for (int x = 0; x < World.VOXEL_CHUNK_SIZE_XZ; ++x) {
						var pixOfs = zofs+x;

						if ((dstVoxels[pixOfs].BlockType() == EVoxelBlockType.AIR) && (srcVoxels[ofs].BlockType() != EVoxelBlockType.AIR)) {
							dstVoxels[pixOfs] = srcVoxels[ofs];
							++numSolid;
							if (numSolid == World.VOXELS_PER_CHUNK_XZ) {
								return true;
							}
						}

						++ofs;
					}
				}
			}

			return false;
		}

		void RenderTileToBlitTexture(ChunkTile tile) {
			var blockColors = World.Streaming.blockColors;

			for (int z = 0; z < World.VOXEL_CHUNK_SIZE_XZ; ++z) {
				var zofs = z * World.VOXEL_CHUNK_SIZE_XZ;
				for (int x = 0; x < World.VOXEL_CHUNK_SIZE_XZ; ++x) {
					var ofs = zofs+x;
					var voxel = tile.voxelmap[ofs].BlockType();
					var color = blockColors[(int)(voxel-1)];
					_pixels[ofs] = color;
				}
			}

			_blitTexture.SetPixels32(_pixels);
			_blitTexture.Apply();
		}

		void BlitToMainTexture(Texture2D tex, int x, int y) {
			Graphics.CopyTexture(tex, 0, 0, 0, 0, tex.width, tex.height, _mainTexture, 0, 0, x, y);
		}

		void PurgeOutOfBoundTiles() {
			for (int i = 0; i < _numTiles;) {
				var tile = _tiles[i];
				if ((tile.x < _chunkMinX) || (tile.x > _chunkMaxX) ||
					(tile.z < _chunkMinZ) || (tile.z > _chunkMaxZ)) {
					_tiles.RemoveAtSwap(i, ref _numTiles);
					FreeTile(tile);
				} else {
					++i;
				}
			}
		}

		void AddBoundedTiles() {
			for (int z = _chunkMinZ; z <= _chunkMaxZ; ++z) {
				for (int x = _chunkMinX; x <= _chunkMaxX; ++x) {
					AddTile(x, z);
				}
			}
		}

		void AddTile(int x, int z) {
			if (GetHashedTile(x, z) == null) {
				var tile = CreateTile(x, z);
				_tiles[_numTiles++] =tile;

				for (int y = _chunkMinY; y <= _chunkMaxY; ++y) {
					var pos = new WorldChunkPos_t(x, y, z);
					var chunk = _streaming.GetChunk(pos);
					if (chunk != null) {
						AddChunkToTile(tile, chunk);
					}
				}
			}
		}
						
		static uint GetHash(int x) {
			const uint H = uint.MaxValue / 2;
			uint hx = (x < 0) ? (H - ((uint)(-x))) : (uint)(H + x);
			return hx;
		}

		static uint GetTileHash(uint x, uint z) {
			var hx = x & (HASH_SIZE_XZ - 1);
			var hz = z & (HASH_SIZE_XZ - 1);
			return hx + (hz*HASH_SIZE_XZ);
		}

		ChunkTile GetHashedTile(int x, int z) {
			var hash = GetTileHash(GetHash(x), GetHash(z));

			for (var tile = _hash[hash]; tile != null; tile = tile.next) {
				if ((tile.x == x) && (tile.z == z)) {
					return tile;
				}
			}

			return null;
		}

		void FreeTile(ChunkTile tile) {
			for (int i = 0; i < tile.chunks.Length; ++i) {
				tile.chunks[i] = null;
			}
			if (tile.prev != null) {
				tile.prev.next = tile.next;
			} else {
				_hash[tile.hash] = tile.next;
			}
			if (tile.next != null) {
				tile.next.prev = tile.prev;
			}

			_tilePool.ReturnObject(tile);
		}

		ChunkTile CreateTile(int x, int z) {
			var tile = _tilePool.GetObject();
			tile.x = x;
			tile.z = z;
			tile.prev = null;

			tile.hash = GetTileHash(GetHash(x), GetHash(z));
			tile.next = _hash[tile.hash];

			if (_hash[tile.hash] != null) {
				_hash[tile.hash].prev = tile;
			}

			_hash[tile.hash] = tile;

			tile.solid = false;
			tile.dirty = false;
			tile.chunks = tile.chunks ?? new IChunk[_chunkNumY];
			tile.voxelmap = tile.voxelmap ?? new EVoxelBlockType[World.VOXELS_PER_CHUNK_XZ];

			return tile;
		}

		static int Compare(IChunk a, IChunk b) {
			if (a == b) {
				return 0;
			}
			if (a == null) {
				return 1;
			}
			if (b == null) {
				return -1;
			}
			
			return -a.chunkPos.cy.CompareTo(b.chunkPos.cy);
		}

		void AddChunkToTile(ChunkTile tile, IChunk chunk) {
			if (!chunk.hasVoxelData || ((chunk.flags & World.EChunkFlags.SOLID) == 0)) {
				return; // air chunk
			}

			for (int i = 0; i < tile.chunks.Length; ++i) {
				if (tile.chunks[i] == chunk) {
					// chunk is already in this tile, just mark as dirty.
					tile.dirty = true;
					_dirty = _dirty || tile.solid;
					return;
				}
				if (tile.chunks[i] == null) {
					tile.chunks[i] = chunk;
					break;
				}
			}

			SortTileChunksAndMarkDirty(tile);
		}

		void RemoveChunkFromTile(ChunkTile tile, IChunk chunk) {
			for (int i = 0; i < tile.chunks.Length; ++i) {
				if (tile.chunks[i] == chunk) {
					tile.chunks[i] = null;
					SortTileChunksAndMarkDirty(tile);
					_dirty = true;
					return;
				}
			}
		}

		void SortTileChunksAndMarkDirty(ChunkTile tile) {
			Array.Sort(tile.chunks, (x, y) => Compare(x, y));

			// go from front to back, find the first solid chunk, and null out all the chunks behind it
			tile.solid = false;

			for (int i = 0; i < tile.chunks.Length; ++i) {
				if (tile.solid) {
					if (tile.chunks[i] != null) {
						tile.chunks[i] = null;
					}
				} else {
					var tc = tile.chunks[i];
					if ((tc != null) && ((tc.flags & World.EChunkFlags.SOLID_XZ_PLANE) != 0)) {
						tile.solid = true;
					}
				}
			}

			if (tile.solid) {
				Array.Sort(tile.chunks, (x, y) => Compare(x, y));
			}

			tile.dirty = true;
			_dirty = _dirty || tile.solid;
		}
	}
}