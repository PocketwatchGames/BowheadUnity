// Copyright (c) 2018 Pocketwatch Games LLC.

//#define DISABLE

using System;
using System.Threading;
using System.Text;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
	public sealed class WorldFile : IDisposable {
		const int VERSION = 1;

		struct ChunkFile_t {
			public const int SIZE_ON_DISK = 8*4;
			public WorldChunkPos_t pos;
			public uint flags;
			public uint ofs;
			public uint size;

			public static ChunkFile_t Read(BinaryReader s) {
				var cf = default(ChunkFile_t);

				cf.pos.cx = s.ReadInt32();
				cf.pos.cy = s.ReadInt32();
				cf.pos.cz = s.ReadInt32();
				cf.flags = s.ReadUInt32();
				cf.ofs = s.ReadUInt32();
				cf.size = s.ReadUInt32();

				return cf;
			}

			public void Write(BinaryWriter s) {
				s.Write(pos.cx);
				s.Write(pos.cy);
				s.Write(pos.cz);
				s.Write(flags);
				s.Write(ofs);
				s.Write(size);
			}
		};

		struct Header_t {
			public const int SIZE_ON_DISK = 2*4;
			public int version;
			public int chunkCount;
		};

		abstract class IOReq {
			public World.ChunkMeshGen.CompiledChunkData chunkData;
			public WorldFile file;
			public abstract void Exec();
		};

		class IOReadReq : IOReq {
			public override void Exec() {
				
			}
		};

		class IOWriteReq : IOReq {
			public override void Exec() {
				
			}
		};

		Dictionary<WorldChunkPos_t, ChunkFile_t> _chunkFiles;
		ObjectPool<IOReadReq> _readPool;
		ObjectPool<IOWriteReq> _writePool;

		BinaryWriter _indexFile;
		FileStream _chunkRead;
		BinaryWriter _chunkWrite;
		Thread _ioThread;
		bool _run;

		uint _ofs;

		WorldFile() { }

		public static WorldFile OpenOrCreate(string path) {
			var wf = new WorldFile();
			wf.LoadIndexFile(path);
			if (wf._chunkFiles.Count > 0) {
				wf.OpenChunkFile(path);
			} else {
				wf.NewChunkFile(path);
			}
			return wf;
		}

		public void WriteChunkToFile(World.Streaming.IChunkIO chunk) {
#if !DISABLE
			if (_chunkWrite != null) {
				long ofs = 0;
				long size = 0;

				if ((chunk.flags&World.EChunkFlags.SOLID) != 0) {
					ofs = _chunkWrite.BaseStream.Position;
					WriteChunkDataToFile(_chunkWrite, chunk);
					size = _chunkWrite.BaseStream.Position - ofs;
				}

				var chunkFile = new ChunkFile_t() {
					pos = chunk.chunkPos,
					ofs = (uint)ofs,
					size = (uint)size,
					flags = (uint)chunk.flags
				};
				_chunkFiles[chunkFile.pos] = chunkFile;
			}
#endif
		}

		public World.Streaming.IAsyncChunkIO AsyncReadChunk(World.Streaming.IChunkIO chunk) {
#if !DISABLE
			ChunkFile_t chunkFile;
			if (_chunkFiles.TryGetValue(chunk.chunkPos, out chunkFile)) {
				return ReadChunkData(_chunkRead, chunkFile, chunk);
			}
#endif
			return null;
		}

		void LoadIndexFile(string path) {
			_chunkFiles = new Dictionary<WorldChunkPos_t, ChunkFile_t>();

			bool valid = false;
			var file = File.Open(path + ".cix", FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
			long fpos = 0;
			using (var reader = new BinaryReader(file, Encoding.UTF8)) {
				Header_t header = default(Header_t);
				try {
					header.version = reader.ReadInt32();
					header.chunkCount = reader.ReadInt32();
					valid = header.version == VERSION;

					if (valid) {
						for (int i = 0; i < header.chunkCount; ++i) {
							var cf = ChunkFile_t.Read(reader);
							_chunkFiles.Add(cf.pos, cf);
						}
						fpos = reader.BaseStream.Position;
					}

				} catch (Exception) {
					valid = false;
				}
			}

			file = File.Open(path + ".cix", valid ? FileMode.Open : FileMode.Create, FileAccess.Write, FileShare.None);
			_indexFile = new BinaryWriter(file);
			if (valid) {
				_indexFile.BaseStream.Position = fpos;
			} else {
				_chunkFiles.Clear();
				WriteIndexFile();
				try {
					File.Delete(path + ".cdf");
				} catch (Exception e) {
					UnityEngine.Debug.LogException(e);
				}
			}
		}

		void OpenChunkFile(string path) {
			_chunkRead = File.Open(path + ".cdf", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			try {
				var writeFile = File.Open(path + ".cdf", FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

				_chunkWrite = new BinaryWriter(writeFile);
			} catch (Exception e) {
				_chunkRead.Close();
				_chunkRead = null;
				throw e;
			}
		}

		void NewChunkFile(string path) {
			_chunkWrite = new BinaryWriter(File.Open(path + ".cdf", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

			try {
				_chunkRead = File.Open(path + ".cdf", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			} catch (Exception e) {
				_chunkWrite.Close();
				_chunkWrite = null;
				throw e;
			}
		}

		public void Dispose() {
			if (_indexFile != null) {
				WriteIndexFile();
				_indexFile.Close();
				_indexFile = null;
			}
			_chunkRead?.Close();
			_chunkRead = null;
			_chunkWrite?.Close();
			_chunkWrite = null;
		}

		class AsyncIO : World.Streaming.IAsyncChunkIO {
			public World.Streaming.IChunkIO chunk;

			public static byte[] memblock;

			World.Streaming.EAsyncChunkReadResult World.Streaming.IAsyncChunkIO.result => World.Streaming.EAsyncChunkReadResult.Success;
			World.Streaming.IChunkIO World.Streaming.IAsyncChunkIO.chunkIO => chunk;

			public void Dispose() {}
		};

		static AsyncIO ReadChunkData(FileStream file, ChunkFile_t chunkFile, World.Streaming.IChunkIO chunk) {

			chunk.flags = (World.EChunkFlags)chunkFile.flags;

			if ((chunk.flags&World.EChunkFlags.SOLID) == 0) {
				// empty chunk
				chunk.voxeldata.Broadcast(EVoxelBlockType.Air);
				var verts = chunk.verts;

				for (int i = 0; i < verts.counts.Length; ++i) {
					verts.counts[i] = 0;
				}

				for (int i = 0; i < verts.submeshes.Length; ++i) {
					verts.submeshes[i] = 0;
				}

				return new AsyncIO() {
					chunk = chunk
				};
			}

			if ((AsyncIO.memblock == null) || (AsyncIO.memblock.Length < (int)chunkFile.size)) {
				AsyncIO.memblock = new byte[chunkFile.size];
			}

			file.Position = chunkFile.ofs;
			file.Read(AsyncIO.memblock, 0, (int)chunkFile.size);
			
			unsafe {
				fixed (byte* src = &AsyncIO.memblock[0]) {
					fixed (Voxel_t* dst = &chunk.voxeldata[0]) {
						var count = chunk.voxeldata.Length;
						for (int i = 0; i < count; ++i) {
							dst[i].raw = src[i];
						}
					}
				}
				var verts = chunk.verts;

				fixed (byte* pinned = &AsyncIO.memblock[chunk.voxeldata.Length]) {
					byte* src = pinned;
					for (int i = 0; i < verts.counts.Length; ++i) {
						verts.counts[i] = *((int*)src);
						src += 4;
					}
					for (int i = 0; i < verts.submeshes.Length; ++i) {
						verts.submeshes[i] = *((int*)src);
						src += 4;
					}

					int totalVerts = *((int*)src);
					src += 4;
					int totalIndices = *((int*)src);
					src += 4;

					for (int i = 0; i < totalVerts; ++i) {
						Vector3 v3;
						v3.x = *((float*)src);
						src += 4;
						v3.y = *((float*)src);
						src += 4;
						v3.z = *((float*)src);
						src += 4;
						verts.positions[i] = v3;
					}
					for (int i = 0; i < totalVerts; ++i) {
						Vector3 v3;
						v3.x = *((float*)src);
						src += 4;
						v3.y = *((float*)src);
						src += 4;
						v3.z = *((float*)src);
						src += 4;
						verts.normals[i] = v3;
					}
					for (int i = 0; i < totalVerts; ++i) {
						uint c = *((uint*)src);
						src += 4;
						verts.colors[i] = Utils.GetColor32FromUIntRGBA(c);
					}
					for (int i = 0; i < totalIndices; ++i) {
						verts.indices[i] = *((int*)src);
						src += 4;
					}
				}
			}

			return new AsyncIO() {
				chunk = chunk
			};
		}

		static void WriteChunkDataToFile(BinaryWriter file, World.Streaming.IChunkIO chunk) {
			var numVoxels = chunk.voxeldata.Length;
			for (int i = 0; i < numVoxels; ++i) {
				file.Write(chunk.voxeldata[i].raw);
			}

			var verts = chunk.verts;
			var flags = chunk.flags;

			for (int i = 0; i < verts.counts.Length; ++i) {
				file.Write(verts.counts[i]);
			}

			for (int i = 0; i < verts.submeshes.Length; ++i) {
				file.Write(verts.submeshes[i]);
			}

			var totalVerts = 0;
			var totalIndices = 0;

			for (int layer = 0; layer < World.ChunkLayers.Length; ++layer) {
				if ((flags & ((World.EChunkFlags)((int)World.EChunkFlags.LAYER_DEFAULT << layer))) != 0) {
					var numLayerVerts = verts.counts[layer*3+0];
					if (numLayerVerts > 0) {
						totalVerts += numLayerVerts;

						var maxSubmesh = verts.counts[layer*3+2];
						for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
							var numSubmeshVerts = verts.submeshes[(layer*World.MAX_CHUNK_LAYERS)+submesh];
							totalIndices += numSubmeshVerts;
						}
					}
				}
			}

			file.Write(totalVerts);
			file.Write(totalIndices);

			if (totalVerts > 0) {
				for (int i = 0; i < totalVerts; ++i) {
					var v3 = verts.positions[i];
					file.Write(v3.x);
					file.Write(v3.y);
					file.Write(v3.z);
				}
				for (int i = 0; i < totalVerts; ++i) {
					var v3 = verts.normals[i];
					file.Write(v3.x);
					file.Write(v3.y);
					file.Write(v3.z);
				}
				for (int i = 0; i < totalVerts; ++i) {
					var c = verts.colors[i];
					file.Write(c.ToUIntRGBA());
				}
				for (int i = 0; i < totalIndices; ++i) {
					file.Write(verts.indices[i]);
				}
			}
		}

		void WriteIndexFile() {
			_indexFile.BaseStream.Position = 0;
			_indexFile.Write(VERSION);
			_indexFile.Write(_chunkFiles.Count);
			foreach (var chunk in _chunkFiles.Values) {
				chunk.Write(_indexFile);
			}
		}
	}
}