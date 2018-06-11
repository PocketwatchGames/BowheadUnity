// Copyright (c) 2018 Pocketwatch Games LLC.
using UnityEngine.Assertions;

public class RefCountedUObj<T> : System.IDisposable where T : UnityEngine.Object {

	int _refCount;
	T _obj;

	public RefCountedUObj(T t) {
		_refCount = 1;
		_obj = t;
	}

	public int AddRef() {
		Assert.IsTrue(_refCount > 0);
		return ++_refCount;
	}

	public void Dispose() {
		Assert.IsTrue(_refCount > 0);
		if (--_refCount == 0) {
			UnityEngine.Object.Destroy(_obj);
			_obj = null;
		}
	}

	public T obj {
		get {
			return _obj;
		}
	}

	public T Instantiate() {
		return UnityEngine.Object.Instantiate(_obj);
	}
	
}
