using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public class GameObjectPool : System.IDisposable {

	GameObject prefab;
	Transform root;
	CustomAllocatedObjectPool<GameObject> pool;
	bool disposed;
	int compactedSize;

	public GameObjectPool(GameObject prefab, Transform root, int initialSize) {
		this.root = root;
		this.prefab = prefab;
		pool = new CustomAllocatedObjectPool<GameObject>(Allocate, Free, initialSize);
		compactedSize = initialSize;
	}

	GameObject Allocate() {
		GameObject obj;
		if (prefab != null) {
			obj = GameObject.Instantiate(prefab);
		} else {
			obj = new GameObject();
		}
		obj.transform.parent = root;
		return obj;
	}

	void Free(GameObject gameObject) {
		gameObject.transform.parent = root;
	}

	void Destroy(GameObject gameObject) {
		Utils.DestroyGameObject(gameObject);
	}

	public GameObject GetObject() {
		compactedSize = -1;
		return pool.GetObject();
	}

	public void ReturnObject(GameObject obj) {
		pool.ReturnObject(obj);
	}

	public void Compact(int initialSize) {
		if (compactedSize != initialSize) {
			compactedSize = initialSize;
			pool.Reset(initialSize, Destroy);
		}
	}

	public void Dispose() {
		if (!disposed) {
			disposed = true;
			pool.Reset(0, Destroy);
			pool = null;
		}
	}
}
