// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;

public class ClassDropdown : PropertyAttribute {

	Type[] _types;

	public ClassDropdown(Type type) {
		_types = new[] { type };
	}

	public ClassDropdown(Type[] types) {
		_types = types;
	}

	public bool Readonly {
		get;
		set;
	}

	public Type[] types {
		get {
			return _types;
		}
	}
}
