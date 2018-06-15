// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.UI;
using Bowhead.Actors;
using System;

namespace Bowhead.Client.UI {

	public sealed class UIItemTooltip : UIElementTooltip {
		[SerializeField]
		ItemTooltipWidget _prefab;

		PlayerState _owner;
		MetaGame.DropItemClass _itemClass;
		int _ilvl;

		public void Init(PlayerState owner, MetaGame.DropItemClass itemClass, int ilvl) {
			_owner = owner;
			_itemClass = itemClass;
			_ilvl = ilvl;
		}

		protected override Graphic CreateTooltip() {
			var tooltip = Instantiate(_prefab);
			//tooltip.Init(_owner, _itemClass, _ilvl, GameManager.instance.staticData.xpTable.GetSpellPower(_ilvl));
			return tooltip.graphic;
		}
	}
}
