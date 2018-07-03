// Copyright (c) 2018 Pocketwatch Games LLC.
#if UNITY_5 || UNITY_2018
#define UNITY
#endif

using System;

#if UNITY
using UnityEngine;
using UnityEngine.Assertions;
#endif

public abstract unsafe class Archive : IDisposable {
	bool _isLoading;
	byte[] _bytes = new byte[8];
	bool _disposed = false;
	uint _bitsIn;
	uint _bitsOut;

	int _numBitsOut;
	int _numBitsIn;

	static readonly uint[] INPUT_BIT_MASKS = {
		0,
		0x80,
		0xc0,
        0xe0,
		0xf0,
		0xf8,
		0xfc,
		0xfe,
		0xff
	};

	static readonly uint[] OUTPUT_BIT_MASKS = {
		0,
		0x01,
		0x03,
		0x07,
		0x0f,
		0x1f,
		0x3f,
		0x7f,
		0xff
	};

	public Archive() {
	}

	~Archive() {
		if (!_disposed) {
			Dispose(false);
		}
	}
	
	protected abstract int InternalReadByte();
	protected abstract void InternalWriteByte(byte value);
	protected abstract int InternalSkipBytes(int num);

	public virtual int Read(byte[] bytes, int offset, int num) {
		for (int i = 0; i < num; ++i) {
			bytes[i+offset] = (byte)ReadUnsignedBits(8);
		}
		return num;
	}

	public virtual void Write(byte[] bytes, int offset, int num) {
		for (int i = 0; i < num; ++i) {
			WriteUnsignedBits(bytes[i+offset], 8);
		}
	}

	public abstract long Position {
		get; set;
	}

	public virtual bool EOS {
		get {
			return _numBitsIn == 0;
		}
	}

	static int Min(int a, int b) {
		return (a < b) ? a : b;
	}

#if !UNITY
	static class Assert {
		public static void IsTrue(bool b) { }
	}
#endif

	uint UnpackBits(int num) {
		uint reg = 0;
		int numBitsUnpacked = 0;

		while (numBitsUnpacked < num) {
			int r = Min(num-numBitsUnpacked, _numBitsIn);
			if (r > 0) {
				reg |= (((_bitsIn & INPUT_BIT_MASKS[_numBitsIn]) >> (8-_numBitsIn)) & OUTPUT_BIT_MASKS[r]) << numBitsUnpacked;
				_numBitsIn -= r;
				numBitsUnpacked += r;

				if (numBitsUnpacked == num) {
					break;
				}
			}

			_numBitsIn = 8;

			var x = InternalReadByte();
			if (x == -1) {
				throw new System.IO.IOException("Premature end of archive stream!");
			}

			_bitsIn = (uint)x;
		}

		return reg;
	}

	void PackBits(uint bits, int num) {
		int numBitsPacked = 0;

		while (numBitsPacked < num) {
			int r = Min(8-_numBitsOut, num-numBitsPacked);
			if (r > 0) {
				_bitsOut |= ((bits >> numBitsPacked) & OUTPUT_BIT_MASKS[r]) << _numBitsOut;
				numBitsPacked += r;
				_numBitsOut += r;
			}

			if (_numBitsOut == 8) {
				FlushBitsOut();
			}
		}
	}

	public int ReadSignedBits(int numBits) {
		var neg = ReadUnsignedBits(1);
		if (neg != 0) {
			return -(int)ReadUnsignedBits(numBits);
		}

		return (int)ReadUnsignedBits(numBits);
	}

	public uint ReadUnsignedBits(int numBits) {
		return UnpackBits(numBits);
	}

	public void WriteSignedBits(int value, int numBits) {
		if (value >= 0) {
			PackBits(0, 1);
			PackBits((uint)value, numBits);
		} else {
			PackBits(1, 1);
			PackBits((uint)-value, numBits);
		}
	}

	public void WriteUnsignedBits(int value, int numBits) {
		PackBits((uint)value, numBits);
	}

	public void WriteUnsignedBits(uint value, int numBits) {
		PackBits(value, numBits);
	}

	void FlushBitsOut() {
		if (_numBitsOut > 0) {
			_numBitsOut = 0;
			InternalWriteByte((byte)_bitsOut);
			_bitsOut = 0;
		}
	}

	void DiscardBitsOut() {
		_numBitsOut = 0;
		_bitsOut = 0;
	}

	void FlushBitsIn() {
		_numBitsIn = 0;
		_bitsIn = 0;
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			_bytes = null;
		}
	}

	public void Dispose() {
		if (!_disposed) {
			_disposed = true;
			Dispose(true);
		}
	}

	public virtual void Flush() {
		FlushBitsIn();
		FlushBitsOut();
	}

	public virtual void Discard() {
		FlushBitsIn();
		DiscardBitsOut();
	}

	public virtual void SkipBytes(int num) {
		if (InternalSkipBytes(num) != num) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
	}

	public virtual bool ReadBool() {
		return ReadUnsignedBits(1) != 0;
	}

	public virtual byte ReadByte() {
		Assert.IsTrue(isLoading);
		return (byte)ReadUnsignedBits(8);
	}

	public virtual sbyte ReadSByte() {
		Assert.IsTrue(isLoading);
		return (sbyte)ReadSignedBits(7);
	}

	public virtual short ReadShort() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 2) != 2) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		short i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 2; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual ushort ReadUShort() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 2) != 2) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		ushort i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 2; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual int ReadInt() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 4) != 4) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		int i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual uint ReadUInt() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 4) != 4) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		uint i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual long ReadLong() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 8) != 8) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		long i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual ulong ReadULong() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 8) != 8) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		ulong i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual float ReadFloat() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 4) != 4) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		float i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual double ReadDouble() {
		Assert.IsTrue(isLoading);
		if (Read(_bytes, 0, 8) != 8) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}
		double i;
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			pp[z] = _bytes[z];
		}
		return i;
	}

	public virtual string ReadString() {
		Assert.IsTrue(isLoading);
		var bytes = ReadByteArray();
		if ((bytes != null) && (bytes.Length > 0)) {
			return System.Text.Encoding.UTF8.GetString(bytes);
		}
		return string.Empty;
	}

	public virtual byte[] ReadByteArray() {
		int len;
		return ReadByteArray(null, out len);
	}

	static readonly byte[] zeroBytes = new byte[0];

	public virtual byte[] ReadByteArray(byte[] arr, out int len) {
		Assert.IsTrue(isLoading);

		FlushBitsIn();

		int numBytes = ReadInt();

		if (numBytes == 0) {
			len = 0;
			return zeroBytes;
		}

		if ((arr == null) || (numBytes > arr.Length)) {
			arr = new byte[numBytes];
		}

		if (Read(arr, 0, numBytes) != numBytes) {
			throw new System.IO.IOException("Premature end of archive stream!");
		}

		len = numBytes;
		return arr;
	}

#if UNITY
	public virtual Vector2 ReadVector2() {
		return new Vector2(ReadFloat(), ReadFloat());
	}

	public virtual Vector3 ReadVector3() {
		return new Vector3(ReadFloat(), ReadFloat(), ReadFloat());
	}

	public virtual Vector4 ReadVector4() {
		return new Vector4(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
	}

	public virtual Quaternion ReadQuaternion() {
		return new Quaternion(ReadFloat(), ReadFloat(), ReadFloat(), ReadFloat());
	}

	public virtual Matrix4x4 ReadMatrix4x4() {
		Matrix4x4 m = new Matrix4x4();

		for (int i = 0; i < 4; ++i) {
			m.SetColumn(i, ReadVector4());
		}

		return m;
	}

	public virtual IntMath.Vector2i ReadVector2i() {
		return new IntMath.Vector2i(ReadInt(), ReadInt());
	}

	public virtual IntMath.Vector3i ReadVector3i() {
		return new IntMath.Vector3i(ReadInt(), ReadInt(), ReadInt());
	}

	public virtual Color32 ReadColor32() {
		return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
	}
#endif

	public virtual void Write(bool b) {
		WriteUnsignedBits(b ? 1 : 0, 1);
	}

	public virtual void Write(byte i) {
		WriteUnsignedBits(i, 8);
	}

	public virtual void Write(sbyte i) {
		WriteSignedBits(i, 7);
	}

	public virtual void Write(short i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 2; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 2);
	}

	public virtual void Write(ushort i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 2; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 2);
	}

	public virtual void Write(int i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 4);
	}

	public virtual void Write(uint i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 4);
	}

	public virtual void Write(long i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 8);
	}

	public virtual void Write(ulong i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 8);
	}

	public virtual void Write(float i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 4; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 4);
	}

	public virtual void Write(double i) {
		byte* pp = (byte*)&i;
		for (int z = 0; z < 8; ++z) {
			_bytes[z] = pp[z];
		}
		Write(_bytes, 0, 8);
	}

	public virtual void Write(string s) {
		if (s != null) {
			var arr = System.Text.Encoding.UTF8.GetBytes(s);
			WriteByteArray(arr, 0, arr.Length);
		} else {
			FlushBitsOut();
			Write(0);
		}
	}

	public virtual void WriteByteArray(byte[] bytes, int offset, int length) {
		FlushBitsOut();
		Write(length);
		if (length > 0) {
			Write(bytes, offset, bytes.Length);
		}
	}

#if UNITY
	public virtual void Write(Vector2 v) {
		Write(v.x);
		Write(v.y);
	}

	public virtual void Write(Vector3 v) {
		Write(v.x);
		Write(v.y);
		Write(v.z);
	}

	public virtual void Write(Vector4 v) {
		Write(v.x);
		Write(v.y);
		Write(v.z);
		Write(v.w);
	}

	public virtual void Write(Quaternion q) {
		Write(q.x);
		Write(q.y);
		Write(q.z);
		Write(q.w);
	}

	public virtual void Write(Matrix4x4 m) {
		for (int i = 0; i < 4; ++i) {
			Write(m.GetColumn(i));
		}
	}

	public virtual void Write(IntMath.Vector2i v) {
		Write(v.x);
		Write(v.y);
	}

	public virtual void Write(IntMath.Vector3i v) {
		Write(v.x);
		Write(v.y);
		Write(v.z);
	}

	public virtual void Write(Color32 c) {
		Write(c.r);
		Write(c.g);
		Write(c.b);
		Write(c.a);
	}
#endif

	public void SerializeAsInt<T>(ref T t) {
		if (isLoading) {
			t = (T)((object)ReadInt());
		} else {
			Write(Convert.ToInt32((object)t));
		}
	}

	public void SerializeAsShort<T>(ref T t) {
		if (isLoading) {
			t = (T)((object)ReadShort());
		} else {
			Write((short)Convert.ToInt32((object)t));
		}
	}

	public void SerializeAsByte<T>(ref T t) {
		if (isLoading) {
			t = (T)((object)ReadByte());
		} else {
			Write((byte)Convert.ToInt32((object)t));
		}
	}

	public void Serialize(ref byte[] b) {
		if (isLoading) {
			b = ReadByteArray();
		} else {
			WriteByteArray(b, 0, (b != null) ? b.Length : 0);
		}
	}

	public void Serialize(ref bool b) {
		if (isLoading) {
			b = ReadBool();
		} else {
			Write(b);
		}
	}

	public void Serialize(ref byte i) {
		if (isLoading) {
			i = ReadByte();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref sbyte i) {
		if (isLoading) {
			i = ReadSByte();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref short i) {
		if (isLoading) {
			i = ReadShort();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref ushort i) {
		if (isLoading) {
			i = ReadUShort();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref int i) {
		if (isLoading) {
			i = ReadInt();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref uint i) {
		if (isLoading) {
			i = ReadUInt();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref long i) {
		if (isLoading) {
			i = ReadLong();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref ulong i) {
		if (isLoading) {
			i = ReadULong();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref float i) {
		if (isLoading) {
			i = ReadFloat();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref double i) {
		if (isLoading) {
			i = ReadDouble();
		} else {
			Write(i);
		}
	}

	public void Serialize(ref string s) {
		if (isLoading) {
			s = ReadString();
		} else {
			Write(s);
		}
	}

#if UNITY
	public void Serialize(ref Vector2 v) {
		if (isLoading) {
			v = ReadVector2();
		} else {
			Write(v);
		}
	}

	public void Serialize(ref Vector3 v) {
		if (isLoading) {
			v = ReadVector3();
		} else {
			Write(v);
		}
	}

	public void Serialize(ref Vector4 v) {
		if (isLoading) {
			v = ReadVector4();
		} else {
			Write(v);
		}
	}

	public void Serialize(ref Quaternion q) {
		if (isLoading) {
			q = ReadQuaternion();
		} else {
			Write(q);
		}
	}

	public void Serialize(ref Matrix4x4 m) {
		if (isLoading) {
			m = ReadMatrix4x4();
		} else {
			Write(m);
		}
	}

	public void Serialize(ref IntMath.Vector2i v) {
		if (isLoading) {
			v = ReadVector2i();
		} else {
			Write(v);
		}
	}

	public void Serialize(ref IntMath.Vector3i v) {
		if (isLoading) {
			v = ReadVector3i();
		} else {
			Write(v);
		}
	}

	public void Serialize(ref Color32 c) {
		if (isLoading) {
			c = ReadColor32();
		} else {
			Write(c);
		}
	}

#endif

	public bool isLoading {
		get {
			return _isLoading;
		}
	}

	public bool isSaving {
		get {
			return !isLoading;
		}
	}

	public virtual void OpenRead() {
		_isLoading = true;
		FlushBitsIn();
		DiscardBitsOut();
	}

	public virtual void OpenWrite() {
		_isLoading = false;
		FlushBitsIn();
		DiscardBitsOut();
	}

}

public class StreamArchive : Archive {
	System.IO.Stream _stream;
	bool _dispose;

	public StreamArchive() {
	}

	public StreamArchive(System.IO.Stream stream, bool isLoading, bool shouldDispose) {
		_stream = stream;
		_dispose = shouldDispose;
		if (isLoading) {
			OpenRead();
		} else {
			OpenWrite();
		}
	}

	protected override int InternalSkipBytes(int num) {
		_stream.Position += num;
		return num;
	}

	public override long Position {
		get {
			return _stream.Position;
		}
		set {
			_stream.Position = value;
		}
	}

	public override bool EOS {
		get {
			return (_stream.Position >= _stream.Length) && base.EOS;
		}
	}

	protected override int InternalReadByte() {
		return _stream.ReadByte();
	}

	protected override void InternalWriteByte(byte value) {
		_stream.WriteByte(value);
	}

	protected override void Dispose(bool disposing) {
		base.Dispose(disposing);

		if (disposing && (_stream != null)) {
			if (shouldDispose) {
				_stream.Dispose();
			}
			_stream = null;
		}
	}

	public System.IO.Stream stream {
		get {
			return _stream;
		}
		set {
			_stream = value;
		}
	}

	public bool shouldDispose {
		get {
			return _dispose;
		}
		set {
			_dispose = true;
		}
	}
}

public class NetArchive : StreamArchive {

	public NetArchive() {
	}

	public NetArchive(System.IO.Stream stream, bool isLoading, bool shouldDispose) : base(stream, isLoading, shouldDispose) {
	}

	public override byte ReadByte() {
		return (byte)ReadUnsignedBits(8);
	}

	public override void Write(byte i) {
		WriteUnsignedBits(i, 8);
	}

	public override int Read(byte[] bytes, int offset, int num) {
		for (int i = 0; i < num; ++i) {
			bytes[offset+i] = ReadByte();
		}
		return num;
	}

	public override void Write(byte[] bytes, int offset, int num) {
		for (int i = 0; i < num; ++i) {
			Write(bytes[offset+i]);
		}
	}
}