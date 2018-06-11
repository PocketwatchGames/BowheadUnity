// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

public abstract class VersionedObject : ScriptableObject
#if UNITY_EDITOR
	, ISerializationCallbackReceiver
#endif
	{

	[HideInInspector]
	[SerializeField]
	protected int version;

#if UNITY_EDITOR
	protected virtual void InitVersion() {}

	public void OnBeforeSerialize() {
		InitVersion();
	}

	public void OnAfterDeserialize() { }
#endif

	public virtual void ClientPrecache() { }

}

public abstract class VersionedObjectWithSerializationCallback : ScriptableObject, ISerializationCallbackReceiver {

	[HideInInspector]
	[SerializeField]
	protected int version;

#if UNITY_EDITOR
	protected virtual void InitVersion() { }
#endif

	public virtual void OnBeforeSerialize() {
#if UNITY_EDITOR
		InitVersion();
#endif
	}

	public virtual void OnAfterDeserialize() { }

	public virtual void ClientPrecache() { }

}