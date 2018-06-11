// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using Bowhead.MetaGame;
using Bowhead.Actors;
using System;

namespace Bowhead.Client.UI {

	public sealed class ItemTooltipWidget : TooltipWidget {
		[Serializable]
		public struct IconPanel {
			public GameObject root;
			public Image icon;
			public Image quality;
		}

		[SerializeField]
		IconPanel _rune;
		[SerializeField]
		IconPanel _relic;
		[SerializeField]
		IconPanel _potion;
		[SerializeField]
		IconPanel _gem;
		//[SerializeField]
		//TextMeshPro _name;
		//[SerializeField]
		//TMPro.TextMeshProUGUI _ilvl;
		//[SerializeField]
		//TMPro.TextMeshProUGUI _quality;
		[SerializeField]
		GameObject _flavorPanel;
		//[SerializeField]
		//TMPro.TextMeshProUGUI _flavorText;
		[SerializeField]
		GameObject _descriptionPanel;
		//[SerializeField]
		//TMPro.TextMeshProUGUI _descriptionText;
		[SerializeField]
		Transform _statList;
		//[SerializeField]
		//StatWidget _statPrefab;
		//[SerializeField]
		//UnitClassStatsWidget _unitClassStatWidgetPrefab;
		//[SerializeField]
		//UnitClass[] _classes;

		public void Init(PlayerState owner, DropItemClass itemClass, int ilvl, float spellPower) {
			var essenceClass = itemClass as SoulFragmentItemClass;
			if (essenceClass != null) {
				InitEssence(owner, essenceClass);
			} else {
				var runeOrGem = itemClass as InventorySocketItemClass;
				if (runeOrGem != null) {
					if (runeOrGem.socket == ESocketItemType.Rune) {
						InitRune(runeOrGem, ilvl, spellPower);
					} else {
						InitGem(runeOrGem, ilvl, spellPower);
					}
				} else {
					var relicPot = (InventoryGrantAbilityItemClass)itemClass;
					InitRelicPot(relicPot, ilvl, spellPower);
				}
			}
		}

		void InitRune(InventorySocketItemClass item, int ilvl, float spellPower) {
			InitPanel(_rune, item, item.FormatLocalizedDescription(spellPower), FormatiLvl(ilvl), null);
			InitStats(item, ilvl);
		}

		void InitGem(InventorySocketItemClass item, int ilvl, float spellPower) {
			InitPanel(_gem, item, item.FormatLocalizedDescription(spellPower), FormatiLvl(ilvl), null);
			InitStats(item, ilvl);
		}

		void InitRelicPot(InventoryGrantAbilityItemClass item, int ilvl, float spellPower) {
			if (item.itemType == EGrantAbilityItemType.Potion) {
				InitPanel(_potion, item, item.FormatLocalizedDescription(spellPower), FormatiLvl(ilvl), null);
			} else {
				InitPanel(_relic, item, item.FormatLocalizedDescription(spellPower), null, null);
			}
		}

		void InitEssence(PlayerState owner, SoulFragmentItemClass itemClass) {
			InitPanel(_gem, itemClass, itemClass.FormatLocalizedDescription(1), Utils.GetLocalizedText("UI.SoulEssenceDropAmount", itemClass.numFragments), "<" + owner.playerName + ">");
		}

		void InitPanel(IconPanel icon, DropItemClass item, string description, string ilvl, string owner) {
			icon.root.SetActive(true);
			icon.icon.sprite = item.LoadIcon();
			//_name.text = item.localizedName;

			//if (ilvl != null) {
			//	_ilvl.text = ilvl.ToString();
			//} else {
			//	_ilvl.gameObject.SetActive(false);
			//}

			//var flavor = item.localizedFlavorText;
			//if (flavor != null) {
			//	_flavorText.text = flavor;
			//} else {
			//	_flavorPanel.SetActive(false);
			//}

			//if (description != null) {
			//	_descriptionText.text = description;
			//} else {
			//	_descriptionPanel.SetActive(false);
			//}

			//if (string.IsNullOrEmpty(owner)) {
			//	_quality.text = Utils.GetLocalizedText("UI.Quality." + item.quality.ToString());
			//	icon.quality.color = item.quality.ToColor();
			//} else {
			//	_quality.text = owner;
			//	icon.quality.gameObject.SetActive(false);
			//}
		}

		static string FormatiLvl(int ilvl) {
			return "(ilvl " + ilvl + ")";
		}

		void InitStats(InventoryGearItemClass itemClass, int ilvl) {
//#if !DEDICATED_SERVER
//			var stats = itemClass.stats;

//			if (stats != null) {
//				var level = GameManager.instance.clientInventory.level;

//				for (int i = 0; i < stats.Length; ++i) {
//					var stat = stats[i];
//					var w = Instantiate(_statPrefab);
//					w.transform.SetParent(_statList, false);
//					var value = stat.GetScaledValue(ilvl);
//					w.SetValue(null, stat.itemStatClass.metaClass.localizedName, value.ToString("0.0"), stat.itemStatClass.metaClass.color);
//				}

//				if (_classes != null) {
//					for (int i = 0; i < _classes.Length; ++i) {
//						var uc = _classes[i];
//						var w = Instantiate(_unitClassStatWidgetPrefab);
//						if (w.Init(itemClass, ilvl, uc, level)) {
//							w.transform.SetParent(_statList, false);
//						} else {
//							Utils.DestroyGameObject(w.gameObject);
//						}
//					}
//				}
//			}
//#endif
		}
	}
}