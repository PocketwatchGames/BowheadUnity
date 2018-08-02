// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;

public abstract class StaticAsset : ScriptableObject, StaticAsset.Indexed {
	public interface Indexed {
		int staticIndex {
			get;
#if UNITY_EDITOR
			set;
#endif
		}

		void ClientPrecache();
	}

	[HideInInspector]
	[SerializeField]
	int _index;

	public int staticIndex {
		get {
			return _index;
		}
#if UNITY_EDITOR
		set {
			_index = value;
		}
#endif
	}

	public virtual void ClientPrecache() { }
}

public abstract class StaticVersionedAsset : VersionedObject, StaticAsset.Indexed {
	[HideInInspector]
	[SerializeField]
	int _index;

	public int staticIndex {
		get {
			return _index;
		}
#if UNITY_EDITOR
		set {
			_index = value;
		}
#endif
	}
}

public abstract class StaticVersionedAssetWithSerializationCallback : VersionedObjectWithSerializationCallback, StaticAsset.Indexed {
	[HideInInspector]
	[SerializeField]
	int _index;

	public int staticIndex {
		get {
			return _index;
		}
#if UNITY_EDITOR
		set {
			_index = value;
		}
#endif
	}
}

