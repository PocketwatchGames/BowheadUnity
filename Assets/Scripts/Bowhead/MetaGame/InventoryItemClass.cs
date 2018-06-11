// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;
using Bowhead.Actors;
using Bowhead.Server.Actors;

namespace Bowhead.MetaGame {
	public enum EInventoryItemPrice {
		None,
		VLV25,
		VLV50,
		VLV75,
		VLV100,
		VLV150,
		VLV200,
		VLV250,
		VLV300,
		VLV350,
		VLV400,
		VLV450,
		VLV500,
		VLV550,
		VLV600,
		VLV650,
		VLV700,
		VLV750,
		VLV800,
		VLV850,
		VLV900,
		VLV1000,
		VLV1100,
		VLV1200,
		VLV1300,
		VLV1400,
		VLV1500,
		VLV1600,
		VLV1700,
		VLV1800,
		VLV1900,
		VLV2000,
		VLV2500,
		VLV3500,
		VLV4000,
		VLV4500,
		VLV5000,
		VLV5500,
		VLV6000,
		VLV6500,
		VLV7000,
		VLV7500,
		VLV8000,
		VLV8500,
		VLV9000,
		VLV9500,
		VLV10000
	}

	public enum EItemQuality {
		Trash,
		Common,
		Rare,
		Epic,
		Legendary
	}

	[Serializable]
	public struct InventoryItemTags {}

	public abstract class InventoryItemClass : DropItemClass {
		[SerializeField]
		[HideInInspector]
		int[] _ids;
		[SerializeField]
		[HideInInspector]
		int[] _ilvl;
		[SerializeField]
		string _customIconName;
		[SerializeField]
		EInventoryItemPrice _price;
		[SerializeField]
		bool _autoGrant;
		[SerializeField]
		bool _marketable;
		[SerializeField]
		bool _tradable;
		[SerializeField]
		bool _canVendor;
		[SerializeField]
		bool _commodity;
		[SerializeField]
		bool _consumable;
		[SerializeField]
		bool _deprecated;
		[SerializeField]
		bool _hidden;
		[SerializeField]
		int _requiredLevel;
		[SerializeField]
		InventoryItemTags _tags;
		[SerializeField]
		GeneratedItems _generatedItems;

		[Serializable]
		public struct GeneratedItems {
			public IntMath.Vector2i ilvlRange;
			public int ilvlStep;
		}

		Dictionary<int, int> _ilvl2id;

		public bool deprecated {
			get {
				return _deprecated;
			}
		}

		public bool hidden {
			get {
				return _hidden;
			}
		}

		public EInventoryItemPrice price {
			get {
				return _autoGrant ? EInventoryItemPrice.None : _price;
			}
		}

		public bool tradable {
			get {
				return _tradable && !_autoGrant;
			}
		}

		public bool marketable {
			get {
				return _marketable && !_autoGrant;
			}
		}

		public bool commodity {
			get {
				return _commodity;
			}
		}

		public bool autoGrant {
			get {
				return _autoGrant;
			}
		}

		public bool canVendor {
			get {
				return _canVendor && !_autoGrant;
			}
		}

		public bool consumable {
			get {
				return _consumable;
			}
		}

		public int requiredLevel {
			get {
				return _requiredLevel;
			}
		}

		public virtual string steamIconName {
			get {
				if (string.IsNullOrEmpty(_customIconName)) {
					return name + ".png";
				}
				return _customIconName;
			}
		}
		
		public string customIconName {
			get {
				return _customIconName;
			}
		}

		public override bool CheckiLvlRange(int ilvl) {
			return base.CheckiLvlRange(ilvl) && (_generatedItems.ilvlStep > 0);
		}

		public override int GetiLvlID(int ilvl) {
			if (ilvl < _generatedItems.ilvlRange.x) {
				return _ilvl2id[_generatedItems.ilvlRange.x];
			} else if (ilvl >= _generatedItems.ilvlRange.y) {
				return _ilvl2id[_generatedItems.ilvlRange.y];
			}

			{
				int id;
				if (_ilvl2id.TryGetValue(ilvl, out id)) {
					return id;
				}
			}

			// Round to nearest steam inventory item.
			var x = ((ilvl - _generatedItems.ilvlRange.x + _generatedItems.ilvlStep - 1) / _generatedItems.ilvlStep) * _generatedItems.ilvlStep;
			x = Mathf.Min(x + _generatedItems.ilvlRange.x, _generatedItems.ilvlRange.y);
			return _ilvl2id[x];
		}

		public bool HasiLvl(int ilvl) {
			var range = generatedItems.ilvlRange;
			if ((ilvl >= range.x) && (ilvl <= range.y)) {
				return _ilvl2id.ContainsKey(ilvl);
			}
			return false;
		}

		public IntMath.Vector2i ilvlRange {
			get {
				return _generatedItems.ilvlRange;
			}
		} 

		public sealed override bool GrantItem(Unit instigator, ServerPlayerController player, int id, int ilvl, int count) {
			player.inventorySkills.AsyncGrantItem(id, count);
			player.Owner_ServerGrantedItem(id, count);
			return true;
		}

		public override void OnAfterDeserialize() {
			base.OnAfterDeserialize();

			if (_ids != null) {
				_ilvl2id = new Dictionary<int, int>();
				for (int i = 0; i <_ids.Length; ++i) {
					_ilvl2id.Add(_ilvl[i], _ids[i]);
				}
			} else {
				_ilvl2id = null;
			}
		}

		public GeneratedItems generatedItems {
			get {
				return _generatedItems;
			}
		}

		public Dictionary<int, int> ilvl2id {
			get {
				return _ilvl2id;
			}
		}

#if !SHIP // for XML inventory
		public bool TryGetItemID(int ilvl, out int id) {
			if (_ilvl2id != null) {
				return _ilvl2id.TryGetValue(ilvl, out id);
			}
			id = 0;
			return false;
		}
#endif
#if UNITY_EDITOR
		bool _dirty;
		public void BeginGeneratedItems() {
			_dirty = false;
		}

		public bool ClearIDs(int idMin, int idMax, Dictionary<int, InventoryItemClass> ids) {

			if (_ilvl2id != null) {
				var remove = new List<int>();

				foreach (var pair in _ilvl2id) {
					if ((pair.Value == 0) || (pair.Value < idMin) || (pair.Value >= idMax)) {
						remove.Add(pair.Key);
						_dirty = true;
					} else {
						InventoryItemClass itemClass;
						if (ids.TryGetValue(pair.Value, out itemClass)) {
							if (itemClass != this) {
								remove.Add(pair.Key);
								_dirty = true;
							}
						}
					}
				}

				if (remove.Count > 0) {
					_dirty = true;

					foreach (var x in remove) {
						_ilvl2id.Remove(x);
					}
				}
			}

			return _dirty;
		}

		public void TryAddItemID(int ilvl, int id) {
			if (_ilvl2id == null) {
				_ilvl2id = new Dictionary<int, int>();
			}
			if (!_ilvl2id.ContainsKey(ilvl)) {
				_ilvl2id.Add(ilvl, id);
				_dirty = true;
			}
		}

		public void ConditionalFlushAndMarkDirty() {
			if (_dirty) {
				if (_ilvl2id != null) {
					List<int> ids = new List<int>();
					List<int> ilvls = new List<int>();
					foreach (var pair in _ilvl2id) {
						ilvls.Add(pair.Key);
						ids.Add(pair.Value);
					}
					if (ids.Count > 0) {
						if (ids.Count == ((_ids != null) ? _ids.Length : 0)) {
							_dirty = false;
							for (int i = 0; i < ids.Count; ++i) {
								if ((_ids[i] != ids[i]) || (_ilvl[i] != ilvls[i])) {
									_dirty = true;
									break;
								}
							}
						}

						if (_dirty) {
							_ids = ids.ToArray();
							_ilvl = ilvls.ToArray();
						}
					} else {
						_ids = null;
						_ilvl = null;
					}
				} else {
					if (_ids != null) {
						_ids = null;
						_ilvl = null;
					} else {
						_dirty = false;
					}
				}

				if (_dirty) {
					UnityEditor.EditorUtility.SetDirty(this);
				}
			}
		}
#endif
	}
}

public static class Deadhold_MetaGame_EItemQualityExtensions {
	public static Color ToColor(this Bowhead.MetaGame.EItemQuality quality) {
#if UNITY_EDITOR
		Bowhead.ClientData.ItemQualityColors colors;
		if (Bowhead.GameManager.instance == null) {
			var clientData = Resources.Load<Bowhead.ClientData>("ClientData");
			colors = clientData.itemQualityColors;
		} else {
			colors = Bowhead.GameManager.instance.clientData.itemQualityColors;
		}
#else
		var colors = Bowhead.GameManager.instance.clientData.itemQualityColors;
#endif

		switch (quality) {
			case Bowhead.MetaGame.EItemQuality.Common:
				return colors.common;
			case Bowhead.MetaGame.EItemQuality.Rare:
				return colors.rare;
			case Bowhead.MetaGame.EItemQuality.Epic:
				return colors.epic;
			case Bowhead.MetaGame.EItemQuality.Legendary:
				return colors.legendary;
			default:
				return colors.trash;
		}
	}
}