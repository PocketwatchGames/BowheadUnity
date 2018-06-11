// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
public class EditorOnly : MonoBehaviour {
	protected virtual void Awake () {
		Destroy(gameObject);
	}

	void Reset() {
		gameObject.tag = "EditorOnly";
	}
}
#endif