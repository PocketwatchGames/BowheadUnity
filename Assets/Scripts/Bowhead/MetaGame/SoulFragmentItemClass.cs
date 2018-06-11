// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using Bowhead.Actors;
using Bowhead.Server.Actors;

namespace Bowhead.MetaGame {

	public sealed class SoulFragmentItemClass : TransientItemClass {
		[SerializeField]
		float _numFragments;

		public float numFragments {
			get {
				return _numFragments;
			}
		}

		public sealed override bool GrantItem(Unit instigator, ServerPlayerController player, int id, int ilvl, int count) {
		//	player.AddFractionalSoulStones(_numFragments*count);
			return true;
		}
	}
}