// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;
using System.Collections.Generic;

public class IntHashtable<T> {

	System.Collections.Hashtable hashtable = new System.Collections.Hashtable();
	IntHashKey tempKey = new IntHashKey(0);

	public IntHashtable() {
	}

	public void Add(int key, T value) {
		hashtable.Add(new IntHashKey(key), value);
	}

	public void Remove(int key) {
		tempKey.key = key;
		hashtable.Remove(tempKey);
	}

	public bool Contains(int key) {
		tempKey.key = key;
		return hashtable.ContainsKey(tempKey);
	}

	public bool TryGetValue(int key, out T value) {
		if (Contains(key)) {
			value = this[key];
			return true;
		}

		value = default(T);
		return false;
	}

	public void Clear() {
		hashtable.Clear();
	}

	public bool IsEmpty {
		get {
			return hashtable.Count == 0;
		}
	}

	public T this[int key] {
		get {
			tempKey.key = key;
			return (T)hashtable[tempKey];
		}
		set {
			hashtable[new IntHashKey(key)] = value;
		}
	}

	public IEnumerable<int> Keys {
		get {
			return new CollectionEnumerable<int>(hashtable.Keys);
		}
	}

	public IEnumerable<T> Values {
		get {
			return new CollectionEnumerable<T>(hashtable.Values);
		}
	}

	class CollectionEnumerable<K> : IEnumerable<K> {
		System.Collections.ICollection col;

		public CollectionEnumerable(System.Collections.ICollection col) {
			this.col = col;
		}

		public System.Collections.Generic.IEnumerator<K> GetEnumerator() {
			return new CollectionEnumerator<K>(col.GetEnumerator());
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
			return new CollectionEnumerator<K>(col.GetEnumerator());
		}
	}

	class CollectionEnumerator<K> : IEnumerator<K> {

		System.Collections.IEnumerator e;

		public CollectionEnumerator(System.Collections.IEnumerator e) {
			this.e = e;
		}

		public bool MoveNext() {
			return e.MoveNext();
		}

		public void Reset() {
			e.Reset();
		}

		public void Dispose() {
		}

		public K Current {
			get {
				return (K)e.Current;
			}
		}

		object System.Collections.IEnumerator.Current {
			get {
				return e.Current;
			}
		}

	}

	class IntHashKey {
		public int key;

		public IntHashKey(int key) {
			this.key = key;
		}

		public override int GetHashCode() {
			return key;
		}

		public override bool Equals(object obj) {
			if (obj is IntHashKey) {
				return key == ((IntHashKey)obj).key;
			}
			return false;
		}
	}
}
