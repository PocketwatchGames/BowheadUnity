// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public sealed class SoundClip_WRef : WeakAssetRef<SoundClip> { }

public class SoundClip : VersionedObjectWithSerializationCallback {
	const int VERSION = 1;

	[HideInInspector]
	[SerializeField]
	float _recycleThreshold;
	[SerializeField]
	PClip[] _clips;
	
	float _totalP;
	float _curTotalP;
	int _validClipCount;
	int _recycleThresholdCount;

	List<PClip> _validClips = new List<PClip>();

	[Serializable]
	public struct PClip {
		public AudioClip audioClip;
		public float probability;
		public string textKey;
		[HideInInspector]
		public bool init;
	}

#if UNITY_EDITOR
	public void InspectorSetClips(List<AudioClip> clips) {
		if (clips.Count > 0) {
			_clips = new PClip[clips.Count];
			for (int i = 0; i < _clips.Length; ++i) {
				var c = _clips[i];
				c.init = true;
				c.audioClip = clips[i];
				c.probability = 1f;
				_clips[i] = c;
			}
		} else {
			_clips = null;
		}
		OnAfterDeserialize();
		EditorUtility.SetDirty(this);
	}
	public void InspectorAddClips(List<AudioClip> clips) {
		if (clips.Count > 0) {
			List<PClip> curClips = new List<PClip>((_clips != null) ? _clips : new PClip[0]);
			for (int i = 0; i < clips.Count; ++i) {
				var c = new PClip();
				c.init = true;
				c.audioClip = clips[i];
				c.probability = 1f;
				curClips.Add(c);
			}
			_clips = curClips.ToArray();
			OnAfterDeserialize();
			EditorUtility.SetDirty(this);
		}
	}
#endif

	public AudioClip RandomClip(float random, out string key) {
		if ((_clips != null) && (_totalP > 0f)) {

			if (_validClips.Count < _recycleThresholdCount) {
				_validClips.Clear();
				for (int i = 0; i < _clips.Length; ++i) {
					var c = _clips[i];
					if ((c.audioClip != null) && (c.probability > 0f)) {
						_validClips.Add(c);
					}
				}
				_curTotalP = _totalP;
			}

			float p = 0f;

			for (int i = 0; i < _validClips.Count; ++i) {
				var c = _validClips[i];
				p += c.probability / _curTotalP;

				if (random <= p) {
					_curTotalP -= c.probability;
					_validClips.RemoveAt(i);
					key = c.textKey;
					return c.audioClip;
				}
			}
		}

		key = null;
		return null;
	}

	public override void OnBeforeSerialize() {
		base.OnBeforeSerialize();
#if UNITY_EDITOR
		if (_clips != null) {
			for (int i = 0; i < _clips.Length; ++i) {
				var c = _clips[i];
				if (!c.init) {
					c.init = true;
					c.probability = 1f;
					_clips[i] = c;
				}
			}
		}
#endif
	}

	public override void OnAfterDeserialize() {
		base.OnAfterDeserialize();

		_totalP = 0f;
		_validClipCount = 0;
		_validClips.Clear();

		if (_clips != null) {
			for (int i = 0; i < _clips.Length; ++i) {
				var c = _clips[i];
				if (c.audioClip != null) {
					_totalP += _clips[i].probability;
					_validClips.Add(c);
					++_validClipCount;
				}
			}

			_curTotalP = _totalP;
			_recycleThresholdCount = _validClipCount - Mathf.FloorToInt(_recycleThresholdCount / 100f * _validClipCount);
		}
	}

#if UNITY_EDITOR
	protected override void InitVersion() {
		base.InitVersion();

		if (version < 1) {
			_recycleThreshold = 50;
		}

		version = VERSION;

		ShutTheFuckUpCompilerWarnings();
	}

	void ShutTheFuckUpCompilerWarnings() {
		if (_recycleThreshold > 0) { }
	}

	[MenuItem("Assets/Create/Engine/Sound Clip")]
	static void CreateAsset() {
		Utils.CreateAsset<SoundClip>();
	}
#endif
}