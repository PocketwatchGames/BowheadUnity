// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;

public sealed class SoundManager : MonoBehaviour {

	public enum ELimitMode {
		Discard,
		StopOldest
	}

	[Serializable]
	class MixerGroupControl {
		public AudioMixerGroup mixerGroup;
		public int maxGroupVoices;
		public float maxGroupRate;
		public ELimitMode groupLimit;
		public int maxSourceVoices;
		public float maxSourceRate;
		public ELimitMode sourceLimit;
	}

	[SerializeField]
	MixerGroupControl[] _mixerControls;
	[SerializeField]
	AudioMixer _mixer;

	class SourcePair {
		public AudioSource audioSource;
		public PlayingSource playingSource;
	}

	class PlayingSource {
		public Actor actor;
		public GameObject go;
		public List<SourcePair> sources = new List<SourcePair>();
		public double lastTime;
		public bool hasPlayed;
	}

	class MixerGroupState {
		public MixerGroupControl mixerControl;
		public bool hasPlayed;
		public double lastTime;
		public int currentVoiceCount;
		public List<SourcePair> activeSources = new List<SourcePair>();
		public PlayingSource positionalSource = new PlayingSource();
		public DictionaryList<GameObject, PlayingSource> playingSources = new DictionaryList<GameObject, PlayingSource>();
	}

	List<GameObject> _staleGOs = new List<GameObject>();
	DictionaryList<AudioMixerGroup, MixerGroupState> _mixerState = new DictionaryList<AudioMixerGroup, MixerGroupState>();

	void Awake() {

		if (_mixerControls != null) {
			for (int i = 0; i < _mixerControls.Length; ++i) {
				var c = _mixerControls[i];
				if (c.mixerGroup != null) {
					MixerGroupState s = new MixerGroupState();
					s.mixerControl = c;
					_mixerState.Add(c.mixerGroup, s);
				}
			}
		}

	}

	void Update() {

		for (int i = 0; i < _mixerState.Values.Count; ++i) {
			var mixerGroup = _mixerState.Values[i];

			mixerGroup.currentVoiceCount = 0;

			for (int k = 0; k < mixerGroup.playingSources.Values.Count; ++k) {
				var ps = mixerGroup.playingSources.Values[k];
				if ((ps.go == null) || ((ps.actor != null) && ps.actor.pendingKill)) {
					_staleGOs.Add(ps.go);
					mixerGroup.activeSources.RemoveAll(x => ps.sources.Contains(x));
					if (ps.go != null) {
						// actor was disposed, but GO is still valid, stop/remove audio sources
						for (int j = 0; j < ps.sources.Count; ++j) {
							var s = ps.sources[j];
							if (s.audioSource != null) {
								Utils.DestroyGameObject(s.audioSource.gameObject);
							}
						}
					}
				} else {
					for (int j = 0; j < ps.sources.Count;) {
						var s = ps.sources[j];
						if ((s.audioSource == null) || !s.audioSource.isPlaying) {
							if (s.audioSource != null) {
								Utils.DestroyGameObject(s.audioSource.gameObject);
							}
							ps.sources.RemoveAt(j);
							mixerGroup.activeSources.Remove(s);
						} else {
							++mixerGroup.currentVoiceCount;
							++j;
						}
					}
				}
			}

			for (int k = 0; k < _staleGOs.Count; ++k) {
				mixerGroup.playingSources.Remove(_staleGOs[k]);
			}
						
			for (int k = 0; k < mixerGroup.positionalSource.sources.Count;) {
				var s = mixerGroup.positionalSource.sources[k];
				if ((s.audioSource == null) || !s.audioSource.isPlaying) {
					if (s.audioSource != null) {
						Utils.DestroyGameObject(s.audioSource.gameObject);
					}
					mixerGroup.positionalSource.sources.RemoveAt(k);
					mixerGroup.activeSources.Remove(s);
				} else {
					++mixerGroup.currentVoiceCount;
					++k;
				}
			}

			_staleGOs.Clear();
		}

	}

	bool CanPlay(Actor actor, GameObject go, SoundCue sound, double time, out MixerGroupState groupState, out PlayingSource playingSource) {
		groupState = null;
		playingSource = null;

		if (sound.audioSourcePrefab.outputAudioMixerGroup != null) {

			if (_mixerState.TryGetValue(sound.audioSourcePrefab.outputAudioMixerGroup, out groupState)) {
				if (groupState.hasPlayed && (groupState.mixerControl.maxGroupRate > 0f)) {
					double d = time - groupState.lastTime;
					if (d < groupState.mixerControl.maxGroupRate) {
						return false;
					}
				}

				if (go != null) {
					if (!groupState.playingSources.TryGetValue(go, out playingSource)) {
						playingSource = new PlayingSource();
						playingSource.go = go;
						playingSource.actor = actor;
						groupState.playingSources.Add(go, playingSource);
					}
				} else {
					playingSource = groupState.positionalSource;
				}

				if (playingSource.hasPlayed && (groupState.mixerControl.maxSourceRate > 0f)) {
					double d = time - playingSource.lastTime;
					if (d < groupState.mixerControl.maxSourceRate) {
						return false;
					}
				}

				if (groupState.mixerControl.maxGroupVoices > 0) {
					if (groupState.currentVoiceCount >= groupState.mixerControl.maxGroupVoices) {
						var stopSource = groupState.activeSources[0];
						bool shouldStop = true;

						if (groupState.mixerControl.groupLimit == ELimitMode.Discard) {
							bool discard = true;

							if (groupState.mixerControl.maxSourceVoices > 0) {
								if (playingSource.sources.Count >= groupState.mixerControl.maxSourceVoices) {
									if (groupState.mixerControl.sourceLimit != ELimitMode.Discard) {
										if (playingSource != stopSource.playingSource) {
											// the group limit has been reached and the source
											// will not stop it.
											return false;
										}

										// the source voice limit will force a sound on the group to stop from
										// this source, satisfying the group limit
										discard = false;
										shouldStop = false;
									}									
								}
							}

							if (discard) {
								return false;
							}
						}

						if (shouldStop) {
							// stop the oldest playing voice...
							if (groupState.mixerControl.maxSourceVoices > 0) {
								if (playingSource.sources.Count >= groupState.mixerControl.maxSourceVoices) {
									if (groupState.mixerControl.sourceLimit == ELimitMode.Discard) {
										if (playingSource != stopSource.playingSource) {
											// the source limit has been reached, even if we stopped this voice
											// it won't play so don't stop it.
											return false;
										}
									}

									// the source voice limit will force a sound on the group to stop from
									// this source, satisfying the group limit
									shouldStop = false;
								}
							}
						}

						if (shouldStop) {
							if (stopSource.audioSource != null) {
								Utils.DestroyGameObject(stopSource.audioSource.gameObject);
							}
							stopSource.playingSource.sources.Remove(stopSource);
							groupState.activeSources.RemoveAt(0);
							--groupState.currentVoiceCount;
						}
					}
				}
				
				if (groupState.mixerControl.maxSourceVoices > 0) {
					if (playingSource.sources.Count >= groupState.mixerControl.maxSourceVoices) {
						if (groupState.mixerControl.sourceLimit == ELimitMode.Discard) {
							return false;
						}

						var stopSource = playingSource.sources[0];

						if (stopSource.audioSource != null) {
							Utils.DestroyGameObject(stopSource.audioSource.gameObject);
						}

						groupState.activeSources.Remove(stopSource);
						playingSource.sources.RemoveAt(0);
						--groupState.currentVoiceCount;
					}
				}
				
			}
		}

		return true;
	}

	public AudioSource Play(GameObject instigator, SoundCue sound, double time, float random, float random2, float random3, float random4, float random5) {
		if ((instigator != null) && (sound != null) && (sound.audioSourcePrefab != null)) {

			MixerGroupState groupState;
			PlayingSource playingSource;

			if (!CanPlay(null, instigator, sound, time, out groupState, out playingSource)) {
				return null;
			}

			var clip = sound.RandomClip(random, random2);
			if (clip != null) {

				AudioSource source = GameObject.Instantiate(sound.audioSourcePrefab);
				source.gameObject.name = sound.name + "(" + clip.name + ")";
				source.transform.parent = instigator.transform;
				source.transform.localPosition = Vector3.zero;
				source.transform.localRotation = Quaternion.identity;
				source.clip = clip;
				source.volume = sound.RandomVolume(random3);
				source.pitch = sound.RandomPitch(random4);
				source.reverbZoneMix = sound.RandomReverb(random5);
				source.loop = sound.loop;
				source.Play();

				if (groupState != null) {
					groupState.hasPlayed = true;
					groupState.lastTime = time;
					playingSource.hasPlayed = true;
					playingSource.lastTime = time;
					var sourcePair = new SourcePair();
					sourcePair.audioSource = source;
					sourcePair.playingSource = playingSource;
					playingSource.sources.Add(sourcePair);
					groupState.activeSources.Add(sourcePair);
					++groupState.currentVoiceCount;
				} else {
					source.gameObject.AddComponent<AudioSourceGC>();
				}

				return source;
			}
		}

		return null;
	}

	public AudioSource Play(Actor instigator, SoundCue sound, double time, float random, float random2, float random3, float random4, float random5) {
		if ((instigator != null) && (sound != null) && (sound.audioSourcePrefab != null)) {

			MixerGroupState groupState;
			PlayingSource playingSource;

			if (!CanPlay(instigator, instigator.go, sound, time, out groupState, out playingSource)) {
				return null;
			}

			var clip = sound.RandomClip(random, random2);
			if (clip != null) {

				AudioSource source = GameObject.Instantiate(sound.audioSourcePrefab);
				source.gameObject.name = sound.name + "(" + clip.name + ")";
				source.transform.parent = instigator.go.transform;
				source.transform.localPosition = Vector3.zero;
				source.transform.localRotation = Quaternion.identity;
				source.clip = clip;
				source.volume = sound.RandomVolume(random3);
				source.pitch = sound.RandomPitch(random4);
				source.reverbZoneMix = sound.RandomReverb(random5);
				source.loop = sound.loop;
				source.Play();

				if (groupState != null) {
					groupState.hasPlayed = true;
					groupState.lastTime = time;
					playingSource.hasPlayed = true;
					playingSource.lastTime = time;
					var sourcePair = new SourcePair();
					sourcePair.audioSource = source;
					sourcePair.playingSource = playingSource;
					playingSource.sources.Add(sourcePair);
					groupState.activeSources.Add(sourcePair);
					++groupState.currentVoiceCount;
				} else {
					source.gameObject.AddComponent<AudioSourceGC>();
				}

				return source;
			}
		}

		return null;
	}

	public AudioSource Play(Vector3 position, SoundCue sound, double time, float random, float random2, float random3, float random4, float random5) {
		if ((sound != null) && (sound.audioSourcePrefab != null)) {

			MixerGroupState groupState;
			PlayingSource playingSource;

			if (!CanPlay(null, null, sound, time, out groupState, out playingSource)) {
				return null;
			}

			var clip = sound.RandomClip(random, random2);
			if (clip != null) {

				AudioSource source = ((GameObject)GameObject.Instantiate(sound.audioSourcePrefab.gameObject, position, Quaternion.identity)).GetComponent<AudioSource>();
				source.gameObject.name = sound.name + "(" + clip.name + ")";
				source.clip = clip;
				source.volume = sound.RandomVolume(random3);
				source.pitch = sound.RandomPitch(random4);
				source.reverbZoneMix = sound.RandomReverb(random5);
				source.loop = sound.loop;
				source.Play();

				if (groupState != null) {
					groupState.hasPlayed = true;
					groupState.lastTime = time;
					playingSource.hasPlayed = true;
					playingSource.lastTime = time;
					var sourcePair = new SourcePair();
					sourcePair.audioSource = source;
					sourcePair.playingSource = playingSource;
					playingSource.sources.Add(sourcePair);
					groupState.activeSources.Add(sourcePair);
					++groupState.currentVoiceCount;
				} else {
					source.gameObject.AddComponent<AudioSourceGC>();
				}

				return source;
			}
		}

		return null;
	}
}
