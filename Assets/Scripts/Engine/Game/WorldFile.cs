// Copyright (c) 2018 Pocketwatch Games LLC.

#define DISABLE

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using UnityEngine;

using static World;

public sealed class WorldFile : IDisposable {
	
	const int VERSION = 5;

	struct ChunkFile_t {
		public const int SIZE_ON_DISK = 8*4;
		public WorldChunkPos_t pos;
		public uint flags;
		public uint ofs;
		public uint size;
		public ulong modifyCount;

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

	class MMChunkFile : IDisposable {
		int _refCount;
		MemoryMappedFile _file;
		MemoryMappedViewAccessor _view;
		unsafe byte* _ptr;
		long _fileSize;
		public ulong modifyCount;

		public void Open(string path) {
			var fstream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			_fileSize = fstream.Length;

			_file = MemoryMappedFile.CreateFromFile(fstream, null, 0, MemoryMappedFileAccess.Read, null, HandleInheritability.None, false); 
			_view = _file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
			unsafe {
				_view.SafeMemoryMappedViewHandle.AcquirePointer(ref _ptr);
			}
			_refCount = 1;
		}

		public int AddRef() {
			return ++_refCount;
		}

		public int Release() {
			return --_refCount;
		}

		public void Dispose() {
			_view?.SafeMemoryMappedViewHandle.ReleasePointer();
			_view?.Dispose();
			_file?.Dispose();
			_view = null;
			_file = null;
		}

		public unsafe byte* ptr => _ptr;
		public long fileSize => _fileSize;

	};

	Dictionary<WorldChunkPos_t, ChunkFile_t> _chunkFiles;
	ObjectPool<MMChunkFile> _mmChunkFiles = new ObjectPool<MMChunkFile>();
	ObjectPool<AsyncIO> _mmChunkIO = new ObjectPool<AsyncIO>();

	BinaryWriter _indexFile;
	MMChunkFile _chunkRead;
	BinaryWriter _chunkWrite;
	string _chunkFilePath;
	ulong _modifyCount;

	WorldFile() { }

	public static WorldFile OpenOrCreate(string path) {
#if DISABLE
		return null;
#else
		var wf = new WorldFile();
		wf.LoadIndexFile(path);
		if (wf._chunkFiles.Count > 0) {
			wf.OpenChunkFile(path);
		} else {
			wf.NewChunkFile(path);
		}
		return wf;
#endif
	}

	public void WriteChunkToFile(Streaming.IChunkIO chunk) {
		if (_chunkWrite != null) {
			++_modifyCount;

			long ofs = 0;
			long size = 0;

			if ((chunk.flags&EChunkFlags.SOLID) != 0) {
				ofs = _chunkWrite.BaseStream.Position;
				WriteChunkDataToFile(_chunkWrite, chunk);
				size = _chunkWrite.BaseStream.Position - ofs;
			}

			var chunkFile = new ChunkFile_t() {
				pos = chunk.chunkPos,
				ofs = (uint)ofs,
				size = (uint)size,
				flags = (uint)chunk.flags,
				modifyCount = _modifyCount
			};
			_chunkFiles[chunkFile.pos] = chunkFile;
		}
	}

	public Streaming.IMMappedChunkData MMapChunkData(Streaming.IChunk chunk) {
		ChunkFile_t chunkFile;
		if (_chunkFiles.TryGetValue(chunk.chunkPos, out chunkFile)) {

			if ((_chunkRead != null) && (_chunkRead.modifyCount < _modifyCount)) {
				ReleaseMMChunkFile(_chunkRead);
				_chunkRead = null;
			}

			if (_chunkRead == null) {
				_chunkRead = _mmChunkFiles.GetObject();
				try {
					_chunkRead.Open(_chunkFilePath);
					_chunkRead.modifyCount = _modifyCount;
				} catch (Exception e) {
					_chunkRead.Release();
					_chunkRead.Dispose();
					_mmChunkFiles.ReturnObject(_chunkRead);
					_chunkRead = null;
					throw e;
				}
			}

			if ((chunkFile.ofs + chunkFile.size) > _chunkRead.fileSize) {
				_chunkFiles.Remove(chunk.chunkPos);
				return null;
			}

			var io = _mmChunkIO.GetObject();
			io.worldFile = this;
			io.chunkFile = _chunkRead;
			io.flags = (EChunkFlags)chunkFile.flags;
			io.len = (int)chunkFile.size;

			unsafe {
				io.ptr = _chunkRead.ptr + chunkFile.ofs;
			}

			_chunkRead.AddRef();
			return io;
		}
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
				Debug.LogException(e);
			}
		}
	}

	void OpenChunkFile(string path) {
		_chunkFilePath = path + ".cdf";
		_chunkRead = _mmChunkFiles.GetObject();
		_chunkRead.Open(_chunkFilePath);

		try {
			var writeFile = File.Open(_chunkFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);

			_chunkWrite = new BinaryWriter(writeFile);
			_chunkWrite.BaseStream.Position = _chunkWrite.BaseStream.Length;
		} catch (Exception e) {
			_chunkRead.Release();
			_chunkRead.Dispose();
			_mmChunkFiles.ReturnObject(_chunkRead);
			_chunkRead = null;
			throw e;
		}
	}

	void NewChunkFile(string path) {
		_chunkFilePath = path + ".cdf";
		_chunkWrite = new BinaryWriter(File.Open(_chunkFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite));
	}

	public void Dispose() {
		if (_indexFile != null) {
			WriteIndexFile();
			_indexFile.Close();
			_indexFile = null;
		}

		if (_chunkRead != null) {
			ReleaseMMChunkFile(_chunkRead);
			_chunkRead = null;
		}

		_chunkWrite?.Close();
		_chunkWrite = null;
	}

	void ReleaseMMChunkFile(MMChunkFile mmchunkFile) {
		if (mmchunkFile.Release() == 0) {
			mmchunkFile.Dispose();
			_mmChunkFiles.ReturnObject(mmchunkFile);
		}
	}

	class AsyncIO : Streaming.IMMappedChunkData {
		public MMChunkFile chunkFile;
		public WorldFile worldFile;
		public unsafe byte* ptr;
		public int len;
		public EChunkFlags flags;

		unsafe byte* Streaming.IMMappedChunkData.chunkData => ptr;
		int Streaming.IMMappedChunkData.chunkDataLen => len;
		EChunkFlags Streaming.IMMappedChunkData.chunkFlags => flags;
			
		public void Dispose() {
			worldFile.ReleaseMMChunkFile(chunkFile);
			chunkFile = null;
			worldFile._mmChunkIO.ReturnObject(this);
		}
	};

	public static unsafe PinnedChunkData_t DecompressChunkData(byte* ptr, int len, PinnedChunkData_t chunk, ChunkMeshGen.FinalMeshVerts_t verts) {
		var src = ptr;

		{
			var decorationCount = *((int*)src);
			src += 4;

			for (int i = 0; i < decorationCount; ++i) {
				var d = default(Decoration_t);
				d.pos.x = *((float*)src);
				src += 4;
				d.pos.y = *((float*)src);
				src += 4;
				d.pos.z = *((float*)src);
				src += 4;
				d.type = (EDecorationType) (*((int*)src));
				src += 4;

				chunk.AddDecoration(d);
			}
		}

		if ((chunk.flags&EChunkFlags.SOLID) == 0) {
			// empty chunk
			chunk.voxeldata.Broadcast(EVoxelBlockType.Air);

			for (int i = 0; i < verts.counts.Length; ++i) {
				verts.counts[i] = 0;
			}

			for (int i = 0; i < verts.submeshes.Length; ++i) {
				verts.submeshes[i] = 0;
			}

			return chunk;
		}
			
		{
			var voxeldata = chunk.voxeldata;
			var count = chunk.voxeldata.length;
			for (int i = 0; i < count; ++i) {
				voxeldata[i] = new Voxel_t(src[i]);
			}
		}

		src += chunk.voxeldata.length;		
			
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
			
		return chunk;
	}

	static void WriteChunkDataToFile(BinaryWriter file, Streaming.IChunkIO chunk) {
		{
			var decorationCount = chunk.decorationCount;
			file.Write(decorationCount);
			for (int i = 0; i < decorationCount; ++i) {
				var d = chunk.decorations[i];
				file.Write(d.pos.x);
				file.Write(d.pos.y);
				file.Write(d.pos.z);
				file.Write((int)d.type);
			}
		}

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

		for (int layer = 0; layer < ChunkLayers.Length; ++layer) {
			if ((flags & ((EChunkFlags)((int)EChunkFlags.LAYER_DEFAULT << layer))) != 0) {
				var numLayerVerts = verts.counts[layer*3+0];
				if (numLayerVerts > 0) {
					totalVerts += numLayerVerts;

					var maxSubmesh = verts.counts[layer*3+2];
					for (int submesh = 0; submesh <= maxSubmesh; ++submesh) {
						var numSubmeshVerts = verts.submeshes[(layer*MAX_CHUNK_LAYERS)+submesh];
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