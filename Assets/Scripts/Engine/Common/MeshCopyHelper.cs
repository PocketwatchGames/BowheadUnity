// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

public class MeshCopyHelper {

	public static void SetMeshVerts(Mesh mesh, Vector3[] verts, int count) {
		unsafe {
			fixed (void* p = verts) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.vertices = verts;
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshNormals(Mesh mesh, Vector3[] normals, int count) {
		unsafe {
			fixed (void* p = normals) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.normals = normals;
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshTangents(Mesh mesh, Vector4[] tangents, int count) {
		unsafe {
			fixed (void* p = tangents) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.tangents = tangents;
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshColors(Mesh mesh, Color32[] colors, int count) {
		unsafe {
			fixed (void* p = colors) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.colors32 = colors;
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshUVs(Mesh mesh, int channel, Vector4[] uvs, int count) {
		// fuck you, unity.
		var dumbfuckList = new List<Vector4>(count);
		foreach (var dumbFuckCopy in uvs) {
			dumbfuckList.Add(dumbFuckCopy);
		}
		mesh.SetUVs(channel, dumbfuckList);
	}

	public static void SetMaterialPropertyBlockFloatArray(MaterialPropertyBlock mpb, int name, float[] values, int count) {
		unsafe {
			fixed (void* p = values) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mpb.SetFloatArray(name, values);
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshUV(Mesh mesh, int index, Vector2[] uvs, int count) {
		unsafe {
			fixed (void* p = uvs) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					switch (index) {
						default:
							mesh.uv = uvs;
							break;
						case 1:
							mesh.uv2 = uvs;
							break;
						case 2:
							mesh.uv3 = uvs;
							break;
						case 3:
							mesh.uv4 = uvs;
							break;
					}
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetMeshTris(Mesh mesh, int[] tris, int count) {
		unsafe {
			fixed (void* p = tris) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.triangles = tris;
				} finally {
					*plen = origLen;
				}
			}
		}
	}

	public static void SetSubMeshTris(Mesh mesh, int submesh, int[] tris, int count, bool calculateBounds, int baseVertex) {
		unsafe {
			fixed (void* p = tris) {
				var plen = ((UIntPtr*)p) - 1;
				UIntPtr origLen = *plen;
				*plen = (UIntPtr)count;
				try {
					mesh.SetTriangles(tris, submesh, calculateBounds, baseVertex);
				} finally {
					*plen = origLen;
				}
			}
		}
	}
}
