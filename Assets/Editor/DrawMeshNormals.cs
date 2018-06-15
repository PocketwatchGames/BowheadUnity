using UnityEditor;
using UnityEngine;

//[CustomEditor(typeof(MeshFilter))]
public class DrawMeshNormals : Editor {

	private Mesh mesh;

	void OnEnable() {
		MeshFilter mf = target as MeshFilter;
		if (mf != null) {
			mesh = mf.sharedMesh;
		}
	}

	void OnSceneGUI() {
		if (mesh == null) {
			return;
		}

		if (mesh.vertexCount > mesh.normals.Length) {
			return;
		}

		Handles.matrix = (target as MeshFilter).transform.localToWorldMatrix;
		Handles.color = Color.yellow;
		Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;
		
		for (int i = 0; i < mesh.vertexCount; i++) {
			Handles.DrawLine(
				mesh.vertices[i],
				mesh.vertices[i] + mesh.normals[i] * 0.5f);
		}
	}
}