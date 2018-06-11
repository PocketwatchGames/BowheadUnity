// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class HashSetList<V> {

	HashSet<V> hashset = new HashSet<V>();
	List<V> list = new List<V>();
	ReadOnlyCollection<V> readonlyList;

	public HashSetList() {
		readonlyList = new ReadOnlyCollection<V>(list);
    }

	public bool Add(V v) {
		if (hashset.Add(v)) {
			list.Add(v);
			return true;
		}
		return false;
	}

	public void Remove(V k) {
		if (hashset.Remove(k)) {
			list.Remove(k);
		}
	}

	public bool Contains(V v) {
		return hashset.Contains(v);
	}

	public void Clear() {
		hashset.Clear();
		list.Clear();
	}

	public bool IsEmpty {
		get {
			return list.Count == 0;
		}
	}

	public ReadOnlyCollection<V> Values {
		get {
			return readonlyList;
		}
	}

	public V[] ValuesToArray() {
		return list.ToArray();
	}
}
