// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine.Assertions;
using System.Collections.Generic;

public class ObjectPool<T> where T : new()  {

	public delegate T AllocatorDelegate();

	Stack<T> free;
	int _numUsed;
	int maxItems;

	public AllocatorDelegate allocator;

	public ObjectPool() : this(0, 0) { }

	public ObjectPool(int initialSize) : this(initialSize, 0) { }

	public ObjectPool(int initialSize, int maxItems) {
		free = new Stack<T>(initialSize);
		this.maxItems = maxItems;
    }

	public void Reset() {
		Reset(0);
	}

	public void Reset(int initialSize) {
		_numUsed = 0;
		free = new Stack<T>(initialSize);
	}

	public T GetObject() {
		if ((maxItems > 0) && (_numUsed == maxItems)) {
			return default(T);
		}

		if (free.Count == 0) {
			free.Push((allocator != null) ? allocator() : new T());
		}

		++_numUsed;
		return free.Pop();
	}

	public void ReturnObject(T t) {
		Assert.IsTrue(_numUsed > 0);
		--_numUsed;
		free.Push(t);
	}

	public int numUsed {
		get {
			return _numUsed;
		}
	}

	public int numFree {
		get {
			return free.Count;
		}
	}
}

public class CustomAllocatedObjectPool<T> {

	public delegate T AllocateDelegate();
	public delegate void FreeDelegate(T t);

	Stack<T> free;
	int _numUsed;
	int maxItems;

	public AllocateDelegate allocateDelegate;
	public FreeDelegate freeDelegate;

	public CustomAllocatedObjectPool(AllocateDelegate allocate, FreeDelegate free) : this(allocate, free, 0, 0) { }

	public CustomAllocatedObjectPool(AllocateDelegate allocate, FreeDelegate free, int initialSize) : this(allocate, free, initialSize, 0) { }

	public CustomAllocatedObjectPool(AllocateDelegate allocate, FreeDelegate free, int initialSize, int maxItems) {
		Assert.IsNotNull(allocate);
		allocateDelegate = allocate;
		freeDelegate = free;
		this.free = new Stack<T>(initialSize);
		this.maxItems = maxItems;

		while (initialSize-- > 0) {
			this.free.Push(allocateDelegate());
		}
	}

	public void Reset() {
		Reset(0, null);
	}

	public void Reset(int initialSize, FreeDelegate destructor) {
		_numUsed = 0;

		if (destructor != null) {
			while (free.Count > initialSize) {
				destructor(free.Pop());
			}
		} else {
			while (free.Count > initialSize) {
				free.Pop();
			}
		}
	}

	public T GetObject() {
		if ((maxItems > 0) && (_numUsed == maxItems)) {
			return default(T);
		}

		if (free.Count == 0) {
			free.Push(allocateDelegate());
		}

		++_numUsed;
		return free.Pop();
	}

	public void ReturnObject(T t) {
		Assert.IsTrue(_numUsed > 0);
		--_numUsed;
		if (freeDelegate != null) {
			freeDelegate(t);
		}
		free.Push(t);
	}

	public int numUsed {
		get {
			return _numUsed;
		}
	}

	public int numFree {
		get {
			return free.Count;
		}
	}
}