// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors.Spells;

namespace Bowhead.MetaGame {
	public abstract class DropItemClass : StaticVersionedAssetWithSerializationCallback {
		const int VERSION = 1;

		[SerializeField]
		ItemMetaClass _metaClass;
		[SerializeField]
		EItemQuality _quality;
		[SerializeField]
		Sprite_WRef _icon;
		[SerializeField]
		float _descParam1;
		[SerializeField]
		float _descParam2;
		[SerializeField]
		string _customNameKey;
		[SerializeField]
		string _customDescKey;
		[SerializeField]
		string _customFlavorTextKey;
		[SerializeField]
		bool _dontScaleDescriptionParam;
		[SerializeField]
		protected IntMath.Vector2i _ilvlDropRange;
		[SerializeField]
		Actors.ItemPickupServerPrefab _serverPrefab;
		[SerializeField]
		Actors.ItemPickupClientPrefab_WRef _localPrefab;
		[SerializeField]
		Actors.ItemPickupClientPrefab_WRef _teamPrefab;
		[SerializeField]
		SpellCastRule[] _procOnPickup;
		[SerializeField]
		bool _hasFlavorText;

		public EItemQuality quality {
			get {
				return _quality;
			}
		}

		public ItemMetaClass metaClass {
			get {
				return _metaClass;
			}
		}

		public string customNameKey {
			get {
				return _customNameKey;
			}
		}

		public string customDescKey {
			get {
				return _customDescKey;
			}
		}

		public string customFlavorTextKey {
			get {
				return _customFlavorTextKey;
			}
		}

		public virtual string nameKey {
			get {
				return string.IsNullOrEmpty(_customNameKey) ? ("ItemDef." + name + ".Name") : _customNameKey;
			}
		}

		public virtual string descriptionKey {
			get {
				return string.IsNullOrEmpty(_customDescKey) ? ("ItemDef." + name + ".Description") : _customDescKey;
			}
		}

		public virtual string flavorTextKey {
			get {
				return string.IsNullOrEmpty(_customFlavorTextKey) ? ("ItemDef." + name + ".FlavorText") : _customFlavorTextKey;
			}
		}

		public virtual string localizedName {
			get {
				return Utils.GetLocalizedText(nameKey);
			}
		}

		public string FormatLocalizedDescription(float spellPower) {
			return Utils.GetLocalizedText(descriptionKey, _dontScaleDescriptionParam ? _descParam1 : Mathf.FloorToInt(spellPower*_descParam1), _dontScaleDescriptionParam ? _descParam2 : Mathf.FloorToInt(spellPower*_descParam2), descParam3);
		}

		public string localizedFlavorText {
			get {
				return _hasFlavorText ? Utils.GetLocalizedText(flavorTextKey) : null;
			}
		}

		public virtual float descParam1 {
			get {
				return _descParam1;
			}
		}

		public virtual float descParam2 {
			get {
				return _descParam2;
			}
		}

		public virtual float descParam3 {
			get {
				return 0;
			}
		}

		public bool dontScaleDescriptionParam {
			get {
				return _dontScaleDescriptionParam;
			}
		}

		public Actors.ItemPickupServerPrefab serverPrefab {
			get {
				return _serverPrefab;
			}
		}

		public override void ClientPrecache() {
			base.ClientPrecache();
			WeakAssetRef.Precache(_localPrefab);
			WeakAssetRef.Precache(_teamPrefab);
		}

		public Actors.ItemPickupClientPrefab LoadLocalPrefab() {
			return (_localPrefab != null) ? _localPrefab.Load() : null;
		}

		public Actors.ItemPickupClientPrefab LoadTeamPrefab() {
			return (_teamPrefab != null) ? _teamPrefab.Load() : null;
		}

		public bool hasValidTeamPrefab {
			get {
				return _teamPrefab != null;
			}
		}

		public Sprite LoadIcon() {
			return (_icon != null) ? _icon.Load() : null;
		}

		public abstract int GetiLvlID(int ilvl);

		public virtual bool CheckiLvlRange(int ilvl) {
			if (_ilvlDropRange.x > 0) {
				if (ilvl < _ilvlDropRange.x) {
					return false;
				}
			}
			if (_ilvlDropRange.y > 0) {
				if (ilvl > _ilvlDropRange.y) {
					return false;
				}
			}
			return true;
		}

		public abstract bool GrantItem(Actors.Unit instigator, Server.Actors.ServerPlayerController player, int id, int ilvl, int count);

		public void ProcOnPickup(Actor item, Actors.Unit instigator, Server.Actors.ServerPlayerController player, int ilvl) {
			//if ((instigator != null) && (_procOnPickup != null)) {
			//	SpellCastRule rule;
			//	if (SpellCastRule.GetBestRule(_procOnPickup, instigator.team, instigator, out rule)) {
			//		rule.Execute(ilvl, GameManager.instance.staticData.xpTable.GetSpellPower(ilvl), GameManager.instance.randomNumber, instigator.team, item, player, instigator, null);
			//	}
			//}
		}

#if UNITY_EDITOR
		protected sealed override void InitVersion() {
			base.InitVersion();

			OnInitVersion();

			version = VERSION;
		}

		protected virtual void OnInitVersion() {}

#endif
	}
}