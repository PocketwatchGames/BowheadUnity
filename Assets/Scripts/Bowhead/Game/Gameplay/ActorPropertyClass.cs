// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead.Actors {
	public sealed class ActorPropertyClass : ScriptableObject {
		[Serializable]
		public struct StatBonus {
			public float scale;
			public MetaGame.ItemStatMetaClass[] metaClasses;
		}

		[SerializeField]
		string _displayName;
		[SerializeField]
		string _abbreviation;
		[SerializeField]
		string _description;
		[SerializeField]
		XPCurve _scaling;
		[SerializeField]
		XPCurve _pppScaling;
		[SerializeField]
		float _ppp; // points per-percent
		[SerializeField]
		float _pppBase; // points per-percent base
		[SerializeField]
		float _maxPercentBonus;
		[SerializeField]
		StatBonus[] _statBonuses;
		[SerializeField]
		Sprite_WRef _icon;
		[SerializeField]
		Color _color;

		public float GetLevelScaling(int toLevel) {
			if (_scaling != null) {
				return GameManager.instance.staticData.xpTable.Eval(_scaling, toLevel);
			}
			return 1;
		}

		public float GetPPPLevelScaling(int toLevel, float value) {
			value = GetPPPScaleHelper(toLevel, value);
			if (_maxPercentBonus > 0) {
				return Mathf.Min(value, _maxPercentBonus);
			}
			return value;
		}

		public float GetPPPLevelBonus(int toLevel, float value) {
			value = GetPPPScaleHelper(toLevel, value+_pppBase);
			if (_maxPercentBonus > 0) {
				return Mathf.Min(value, _maxPercentBonus);
			}
			return value;
		}

		float GetPPPScaleHelper(int toLevel, float value) {
			if (_pppScaling != null) {
				return (value-_pppBase)/(_ppp*GameManager.instance.staticData.xpTable.Eval(_pppScaling, toLevel));
			}
			return (value-_pppBase)/_ppp;
		}

		public float GetStatBonusScale(MetaGame.ItemStatMetaClass metaClass) {
			StatBonus best = new StatBonus();
			int bestDepth = -1;

			if (_statBonuses != null) {
				for (int i = 0; i < _statBonuses.Length; ++i) {
					var test = _statBonuses[i];
					if (test.metaClasses != null) {
						for (int k = 0; k < test.metaClasses.Length; ++k) {
							var c = test.metaClasses[k];
							if ((c != null) && metaClass.IsA(c) && (c.depth > bestDepth)) {
								best = test;
								bestDepth = c.depth;
							}
						}
					}
				}
			}

			return best.scale;
		}

		public float GetMaxStatBonusScale(IList<MetaGame.ItemStatMetaClass> metaClasses) {
			float scale = 0f;

			for (int i = 0; i < metaClasses.Count; ++i) {
				var metaClass = metaClasses[i];
				if (metaClasses != null) {
					scale = Mathf.Max(scale, GetStatBonusScale(metaClass));
				}
			}

			return scale;
		}

		public Sprite LoadIcon() {
			return (_icon != null) ? _icon.Load() : null;
		}

		public string localizedName {
			get {
				return string.IsNullOrEmpty(_displayName) ? "<missing>" : Utils.GetLocalizedText(_displayName);
			}
		}

		public string localizedAbbreviation {
			get {
				return string.IsNullOrEmpty(_abbreviation) ? "<missing>" : Utils.GetLocalizedText(_abbreviation);
			}
		}

		public string localizedDescription {
			get {
				return string.IsNullOrEmpty(_description) ? "<missing>" : Utils.GetLocalizedText(_description);
			}
		}

		public Color color {
			get {
				return _color;
			}
		}

		public float maxPercentBonus {
			get {
				return _maxPercentBonus;
			}
		}

		public float pppBase {
			get {
				return _pppBase;
			}
		}

		public bool percentBased {
			get {
				return _ppp != 0f;
			}
		}
	}
}