// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bowhead {

	[Serializable]
	public class PhysicalContact {
		public PhysicalMaterialClass material1;
		public PhysicalMaterialClass material2;
		public GameObject_WRef fxPrefab;
		public SoundCue soundCue;
		public float maxContactRate;
		public int maxLivingContacts;
		public Vector2 bloodSplatterRadius;
		public Vector2 bloodSplatterSize;
		public IntMath.Vector2i bloodSplatterCount;

		public void ClientPrecache() {
			Utils.PrecacheWithSounds(fxPrefab);
			SoundCue.Precache(soundCue);
		}

		public static void ClientPrecache(IList<PhysicalContact> contacts) {
			if (contacts != null) {
				for (int i = 0; i < contacts.Count; ++i) {
					var c = contacts[i];
					if (c != null) {
						c.ClientPrecache();
					}
				}
			}
		}
	}

	public class PhysicalContactMatrix : ScriptableObject
#if UNITY_EDITOR
		, ISerializationCallbackReceiver
#endif
		{
		public PhysicalContact[] contacts;
		public PhysicalContact defaultContact;

		public void ClientPrecache() {
			PhysicalContact.ClientPrecache(contacts);
			defaultContact.ClientPrecache();
		}

#if UNITY_EDITOR
		public void OnBeforeSerialize() {
			if (defaultContact == null) {
				defaultContact = new PhysicalContact();
			}
		}

		public void OnAfterDeserialize() {}

		public void UpdateContactMatrix() {

			List<PhysicalMaterialClass> materialClasses = new List<PhysicalMaterialClass>();

			var guids = AssetDatabase.FindAssets("t:PhysicalMaterialClass");
			foreach (var guid in guids) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var obj = (PhysicalMaterialClass)AssetDatabase.LoadAssetAtPath(path, typeof(PhysicalMaterialClass));
				materialClasses.Add(obj);
			}

			List<PhysicalContact> contacts = new List<PhysicalContact>();
			Dictionary<PhysicalMaterialClass, Dictionary<PhysicalMaterialClass, PhysicalContact>> contactMap = new Dictionary<PhysicalMaterialClass, Dictionary<PhysicalMaterialClass, PhysicalContact>>();

			foreach (var m in materialClasses) {
								
				// keep existing contacts for existing material pairs
				// add new contacts for new pairs.

				Dictionary<PhysicalMaterialClass, PhysicalContact> map;
				if (!contactMap.TryGetValue(m, out map)) {
					map = new Dictionary<PhysicalMaterialClass, PhysicalContact>();
					contactMap.Add(m, map);
				}

				foreach (var z in materialClasses) {
					Dictionary<PhysicalMaterialClass, PhysicalContact> zmap;
					contactMap.TryGetValue(z, out zmap);

					if (!map.ContainsKey(z)) {

						// see if z has contact
						PhysicalContact contact = null;

						if ((zmap == null) || !zmap.TryGetValue(m, out contact)) {

							if (this.contacts != null) {
								for (int i = 0; i < this.contacts.Length; ++i) {
									var c = this.contacts[i];
									if (((c.material1 == m) && (c.material2 == z)) || ((c.material1 == z) && (c.material2 == m))) {
										contact = c;
										break;
									}
								}
							}

							if (contact == null) {
								contact = new PhysicalContact();
							}
														
							contact.material1 = m;
							contact.material2 = z;
							contacts.Add(contact);

							if ((zmap != null) && !ReferenceEquals(map, zmap)) {
								zmap.Add(m, contact);
							}
						}
												
						map.Add(z, contact);						
					}
				}
			}

			bool changed = false;

			if ((this.contacts == null) || (this.contacts.Length != contacts.Count)) {
				changed = true;
			}

			if (!changed) {
				for (int i = 0; i < contacts.Count; ++i) {
					var a = this.contacts[i];
					var b = contacts[i];

					if (!ReferenceEquals(a, b)) {
						changed = true;
						break;
					}
				}
			}

			if (changed) {
				this.contacts = contacts.ToArray();
				EditorUtility.SetDirty(this);
			}
		}
#endif
	}

	public class PhysicalContactMatrixState {
		PhysicalContactMatrix _matrix;
		Dictionary<PhysicalMaterialClass, Dictionary<PhysicalMaterialClass, ContactState>> _contactMap = new Dictionary<PhysicalMaterialClass, Dictionary<PhysicalMaterialClass, ContactState>>();
		ContactState _defaultContact;

		class ContactState {
			double _time = -1f;
			PhysicalContact _contact;
			List<GameObject> _sounds = new List<GameObject>();
			List<GameObject> _impacts = new List<GameObject>();

			public ContactState(PhysicalContact contact) {
				_contact = contact;
			}

			public void SpawnContactFx(double time, Vector3 position, Vector3 normal) {
				if ((_contact.soundCue == null) && (_contact.fxPrefab == null) && ((_contact.bloodSplatterCount.y < 1) || (_contact.bloodSplatterSize.y <= 0f))) {
					return;
				}
				bool canSpawn = false;
				double dt = 0;

				if (_time < 0f) {
					_time = time;
				}

				dt = time - _time;

				if ((_contact.maxContactRate <= 0f) || (dt >= _contact.maxContactRate)) {
					canSpawn = true;
				}

				if (canSpawn) {
					if (_contact.maxLivingContacts > 0) {
						for (int i = 0; i < _sounds.Count;) {
							if (_sounds[i] != null) {
								++i;
							} else {
								_sounds.RemoveAt(i);
							}
						}
						for (int i = 0; i < _impacts.Count;) {
							if (_impacts[i] != null) {
								++i;
							} else {
								_impacts.RemoveAt(i);
							}
						}

						int num = Mathf.Max(_sounds.Count, _impacts.Count);
						canSpawn = num < _contact.maxLivingContacts;
					}

					if (canSpawn) {
						_time = time;

						GameObject fxGo = null;

						if ((_contact.fxPrefab != null) && (_contact.fxPrefab.Load() != null)) {
							fxGo = (GameObject)GameObject.Instantiate(_contact.fxPrefab.Load(), position, Utils.LookBasis(normal));
							if (_contact.maxLivingContacts > 0) {
								_impacts.Add(fxGo);
							}
						}
						
						GameManager.instance.Play(position, _contact.soundCue);
						
						if (GameManager.instance.clientWorld != null) {
							//GameManager.instance.clientWorld.RenderBloodSplats(position, _contact.bloodSplatterRadius, _contact.bloodSplatterSize, _contact.bloodSplatterCount);
						}
					}
				}
			}
		}

		public PhysicalContactMatrixState(PhysicalContactMatrix matrix) {
			_matrix = matrix;
			_defaultContact = new ContactState(matrix.defaultContact);

			if (matrix.contacts != null) {
				for (int i = 0; i < matrix.contacts.Length; ++i) {
					var contact = matrix.contacts[i];
					var state = new ContactState(contact);

					Dictionary<PhysicalMaterialClass, ContactState> map;
					if (!_contactMap.TryGetValue(contact.material1, out map)) {
						map = new Dictionary<PhysicalMaterialClass, ContactState>();
						_contactMap[contact.material1] = map;
					}

					map[contact.material2] = state;

					if (!_contactMap.TryGetValue(contact.material2, out map)) {
						map = new Dictionary<PhysicalMaterialClass, ContactState>();
						_contactMap[contact.material2] = map;
					}

					map[contact.material1] = state;
				}
			}
		}

		public void SpawnContactFx(double time, PhysicalMaterialClass a, PhysicalMaterialClass b, Vector3 position, Vector3 normal) {
			if ((a != null) && (b != null)) {
				Dictionary<PhysicalMaterialClass, ContactState> map;
				if (_contactMap.TryGetValue(a, out map)) {
					ContactState contactState;
					if (map.TryGetValue(b, out contactState)) {
						contactState.SpawnContactFx(time, position, normal);
						return;
					}
				}

				if (a.defaultContact && b.defaultContact) {
					_defaultContact.SpawnContactFx(time, position, normal);
				}
			}
		}
	}

#if UNITY_EDITOR
	public class PhysicalContactMatrixProcessor : AssetPostprocessor {
		static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
			if ((importedAssets.Length > 0) || (deletedAssets.Length > 0)) {
				var contactMatrix = (PhysicalContactMatrix)AssetDatabase.LoadAssetAtPath("Assets/Prefabs/PhysicalContactMatrix.asset", typeof(PhysicalContactMatrix));
				if (contactMatrix != null) {
					contactMatrix.UpdateContactMatrix();
				}
			}
		}
	}
#endif
}