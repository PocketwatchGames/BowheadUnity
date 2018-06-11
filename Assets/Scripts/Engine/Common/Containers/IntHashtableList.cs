// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Reflection;
using System.Collections.Generic;

public class IntHashtableList<V> {

	IntHashtable<V> hashtable = new IntHashtable<V>();
	List<V> list = new List<V>();

	public void Add(int k, V v) {
		hashtable.Add(k, v);
		list.Add(v);
	}

	public bool Remove(int k) {
		V v;
		if (hashtable.TryGetValue(k, out v)) {
			hashtable.Remove(k);
			list.Remove(v);
			return true;
		}
		return false;
	}

	public bool Contains(int k) {
		return hashtable.Contains(k);
	}

	public bool TryGetValue(int k, out V v) {
		return hashtable.TryGetValue(k, out v);
	}

	public bool IsEmpty {
		get {
			return hashtable.IsEmpty;
		}
	}

	public V this[int key] {
		get {
			return hashtable[key];
		}
		set {
			V obj;
			if (hashtable.TryGetValue(key, out obj)) {
				hashtable.Remove(key);
				list.Remove(obj);
			}
			Add(key, value);
		}
	}

	public void Clear() {
		hashtable.Clear();
		list.Clear();
	}

	public List<V> Values {
		get {
			return list;
		}
	}
}
