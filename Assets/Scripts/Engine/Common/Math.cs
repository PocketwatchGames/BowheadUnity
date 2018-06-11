// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

namespace IntMath {

	[Serializable]
	public struct Vector2i : System.IEquatable<Vector2i> {
		public int x;
		public int y;

		public Vector2i(int x, int y) {
			this.x = x;
			this.y = y;
		}

		public static readonly Vector2i zero = new Vector2i(0, 0);

		public static bool operator == (Vector2i x, Vector2i y) {
			return (x.x == y.x) && (x.y == y.y);
		}

		public static bool operator != (Vector2i x, Vector2i y) {
			return !(x == y);
		}

		public static Vector2i operator +(Vector2i x, Vector2i y) {
			return new Vector2i(x.x + y.x, x.y + y.y);
		}

		public static Vector2i operator -(Vector2i x, Vector2i y) {
			return new Vector2i(x.x - y.x, x.y - y.y);
		}

		public static Vector2i operator *(Vector2i x, Vector2i y) {
			return new Vector2i(x.x * y.x, x.y * y.y);
		}

		public static Vector2i operator /(Vector2i x, Vector2i y) {
			return new Vector2i(x.x / y.x, x.y / y.y);
		}

		public bool Equals(Vector2i other) {
			return this == other;
		}

		public override int GetHashCode() {
			return x.GetHashCode() ^ y.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is Vector2i) {
				return this == (Vector2i)obj;
			}
			return false;
        }
	}

	[Serializable]
	public struct Vector3i : System.IEquatable<Vector3i> {
		public int x;
		public int y;
		public int z;

		public Vector3i(int x, int y, int z) {
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static readonly Vector3i zero = new Vector3i(0, 0, 0);

		public static bool operator == (Vector3i x, Vector3i y) {
			return (x.x == y.x) && (x.y == y.y) && (x.z == y.z);
		}

		public static bool operator != (Vector3i x, Vector3i y) {
			return !(x == y);
		}

		public static Vector3i operator + (Vector3i x, Vector3i y) {
			return new Vector3i(x.x + y.x, x.y + y.y, x.z + y.z);
		}

		public static Vector3i operator - (Vector3i x, Vector3i y) {
			return new Vector3i(x.x - y.x, x.y - y.y, x.z - y.z);
		}

		public static Vector3i operator * (Vector3i x, Vector3i y) {
			return new Vector3i(x.x * y.x, x.y * y.y, x.z * y.z);
		}

		public static Vector3i operator / (Vector3i x, Vector3i y) {
			return new Vector3i(x.x / y.x, x.y / y.y, x.z / y.z);
		}

		public bool Equals(Vector3i other) {
			return this == other;
		}

		public override int GetHashCode() {
			return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
		}

		public override bool Equals(object obj) {
			if (obj is Vector3i) {
				return this == (Vector3i)obj;
			}
			return false;
		}
	}
}

public static class MathUtils {
	public static Vector2 Abs(Vector2 v) {
		return new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));
	}
	public static Vector3 Abs(Vector3 v) {
		return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
	}
	public static Vector4 Abs(Vector4 v) {
		return new Vector4(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z), Mathf.Abs(v.w));
	}
}
