// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Bowhead.Editor {
	[CustomEditor(typeof(PhysicalContactMatrix))]
	public class PhysicalContactMatrixInspector : UnityEditor.Editor {

		class Group {
			bool foldout;
			List<Group> _groups = new List<Group>();
			Group _parent;
			int _index;

			public Group() {}

			public Group(Group parent) {
				_parent = parent;
			}

			public Group Begin(string text) {
				Group g;
				if (_index < _groups.Count) {
					g = _groups[_index];
				} else {
					g = new Group(this);
					_groups.Add(g);
				}

				++_index;

				g.foldout = GUILayoutHelpers.Foldout(g.foldout, text, true);
				if (!g.foldout) {
					return null;
				}

				++EditorGUI.indentLevel;
				return g;
			}

			public Group End() {
				--EditorGUI.indentLevel;
				_index = 0;
				return _parent;
			}
		}

		Group _topLevel = new Group();
		Group _current = null;
		Dictionary<string, Dictionary<string, List<PhysicalContact>>> contacts;

		public override void OnInspectorGUI() {
			_current = _topLevel;

			PhysicalContactMatrix matrix = (PhysicalContactMatrix)target;

			if (contacts == null) {
				contacts = new Dictionary<string, Dictionary<string, List<PhysicalContact>>>();
				if (matrix.contacts != null) {
					foreach (var c in matrix.contacts) {
						Dictionary<string, List<PhysicalContact>> map;
						if (!contacts.TryGetValue(c.material1.name, out map)) {
							map = new Dictionary<string, List<PhysicalContact>>();
							contacts[c.material1.name] = map;
						}
						List<PhysicalContact> list;

						if (!map.TryGetValue(c.material2.name, out list)) {
							list = new List<PhysicalContact>();
							map[c.material2.name] = list;
						}

						list.Add(c);

						if (!ReferenceEquals(c.material1, c.material2)) {
							if (!contacts.TryGetValue(c.material2.name, out map)) {
								map = new Dictionary<string, List<PhysicalContact>>();
								contacts[c.material2.name] = map;
							}

							if (!map.TryGetValue(c.material1.name, out list)) {
								list = new List<PhysicalContact>();
								map[c.material1.name] = list;
							}

							list.Add(c);
						} 
					}
				}
			}

			bool changed = false;

			if (BeginFoldout("Default")) {
				changed = Inspect(matrix.defaultContact) || changed;
				EndFoldout();
			}

			if (BeginFoldout("Contacts")) {
				if (matrix.contacts != null) {
					foreach (var map in contacts) {
						if (BeginFoldout(map.Key)) {
							foreach (var subMap in map.Value) {
								if (BeginFoldout(subMap.Key)) {
									changed = Inspect(subMap.Value[0]) || changed;
									if (subMap.Value.Count > 1) {
										EditorGUILayout.LabelField("ERROR: More than one physical material class named " + map.Key + " or " + subMap.Key);
									}
									EndFoldout();
								}
							}
							EndFoldout();
						}
					}
				}
				EndFoldout();
			}
			EndFoldout();

			if (changed) {
				EditorUtility.SetDirty(target);
			}
		}

		bool Inspect(PhysicalContact contact) {

			var soundCue = (SoundCue)EditorGUILayout.ObjectField("Sound Cue", contact.soundCue, typeof(SoundCue), false);
			var fxPrefabChanged = WeakAssetRefDrawer.WeakRefField("Fx Prefab", contact.fxPrefab);
			var rate = Mathf.Max(0f, EditorGUILayout.FloatField("Max Contact Rate", contact.maxContactRate));
			var max = Mathf.Max(0, EditorGUILayout.IntField("Max Living Contacts", contact.maxLivingContacts));
			var splatterRadius = GUILayoutHelpers.MinMaxSlider("Blood Splatter Radius", contact.bloodSplatterRadius, 0, 8);
			var splatterSize = GUILayoutHelpers.MinMaxSlider("Blood Splatter Size", contact.bloodSplatterSize, 0, 8);
			var splatterCount = GUILayoutHelpers.MinMaxSlider("Blood Splatter Count", contact.bloodSplatterCount, 0, 64);

			if (fxPrefabChanged ||
				!ReferenceEquals(soundCue, contact.soundCue) ||
				(rate != contact.maxContactRate) ||
				(max != contact.maxLivingContacts) ||
				(splatterRadius != contact.bloodSplatterRadius) ||
				(splatterSize != contact.bloodSplatterSize) ||
				(splatterCount != contact.bloodSplatterCount)) {
				contact.soundCue = soundCue;
				contact.maxContactRate = rate;
				contact.maxLivingContacts = max;
				contact.bloodSplatterRadius = splatterRadius;
				contact.bloodSplatterSize = splatterSize;
				contact.bloodSplatterCount = splatterCount;
				return true;
			}

			return false;
		}

		bool BeginFoldout(string text) {
			var g = _current.Begin(text);
			if (g != null) {
				_current = g;
			}
			return g != null;
		}

		void EndFoldout() {
			_current = _current.End();
		}
	}
}