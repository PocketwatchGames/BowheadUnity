// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public class DictionaryList<K, V> {

	Dictionary<K, V> dict = new Dictionary<K, V>();
	List<V> list = new List<V>();
	ReadOnlyCollection<V> readonlyList;

	public DictionaryList() {
		readonlyList = new ReadOnlyCollection<V>(list);
	}

	public void Add(K k, V v) {
		dict.Add(k, v);
		list.Add(v);
	}

	public bool Remove(K k) {
		V v;
		if (dict.TryGetValue(k, out v)) {
			dict.Remove(k);
			return list.Remove(v);
		}
		return false;
	}

	public bool RemoveFromDict(K k) {
		return dict.Remove(k);
	}

	public bool TryGetValue(K k, out V v) {
		return dict.TryGetValue(k, out v);
	}

	public bool ContainsKey(K k) {
		return dict.ContainsKey(k);
	}

	public Dictionary<K, V>.Enumerator GetEnumerator() {
		return dict.GetEnumerator();
	}

	public bool IsEmpty {
		get {
			return dict.Count == 0;
		}
	}

	public V this[K key] {
		get {
			return dict[key];
		}
		set {
			V obj;
			if (dict.TryGetValue(key, out obj)) {
				dict.Remove(key);
				list.Remove(obj);
			}
			Add(key, value);
		}
	}

	public void Clear() {
		dict.Clear();
		list.Clear();
	}

	public void SetList(List<V> list) {
		this.list = list;
	}

	public ReadOnlyCollection<V> Values {
		get {
			return readonlyList;
		}
	}

	public Dictionary<K, V>.KeyCollection Keys {
		get {
			return dict.Keys;
		}
	}
}
