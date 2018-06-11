// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.Generic;

public abstract class MonoBehaviourEx : UnityEngine.MonoBehaviour {

	List<UnityEngine.Object> _gc;

	public T AddGC<T>(T obj) where T : UnityEngine.Object {
		if (_gc == null) {
			_gc = new List<UnityEngine.Object>();
		}
		_gc.Add(obj);
		return obj;
	}

	public T AddGCIfNotAdded<T>(T obj) where T : UnityEngine.Object {
		if ((_gc == null) || !_gc.Contains(obj)) {
			AddGC(obj);
		}
		return obj;
	}

	public void RemoveGC(UnityEngine.Object obj) {
		if (_gc != null) {
			_gc.Remove(obj);
		}
	}

	public T InstantiateGC<T>(T obj) where T : UnityEngine.Object {
		return AddGC(Instantiate(obj));
	}

	protected void GCDestroy() {
		if (_gc != null) {
			for (int i = 0; i < _gc.Count; ++i) {
				var x = _gc[i];
				if (x != null) {
					Destroy(x);
				}
			}
			_gc.Clear();
		}
	}

	protected virtual void OnDestroy() {
		GCDestroy();
	}
}