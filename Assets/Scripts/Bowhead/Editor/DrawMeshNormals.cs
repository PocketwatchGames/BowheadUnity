using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshFilter))]
public class DrawMeshNormals : Editor {

	MeshFilter filter;
	static bool show;

	void OnEnable() {
		filter = target as MeshFilter;
	}

	void OnSceneGUI() {
		if (!show || (filter == null) || (filter.sharedMesh == null) || (Selection.activeGameObject != filter.gameObject)) {
			return;
		}

		var mesh = filter.sharedMesh;

		if (mesh.vertexCount > mesh.normals.Length) {
			return;
		}

		Handles.matrix = (target as MeshFilter).transform.localToWorldMatrix;
		Handles.color = Color.yellow;
		Handles.zTest = UnityEngine.Rendering.CompareFunction.Less;

		var numVerts = mesh.vertexCount;
		var vertices = mesh.vertices;
		var normals = mesh.normals;
		
		for (int i = 0; i < numVerts; i++) {
			Handles.DrawLine(
				vertices[i],
				vertices[i] + normals[i] * 0.5f);
		}
	}

	[MenuItem("Bowhead/Show Selected Mesh Normals")]
	static void ShowSelectedMeshNormalsMenu() {
		show = !show;
		Menu.SetChecked("Bowhead/Show Selected Mesh Normals", show);
	}
}