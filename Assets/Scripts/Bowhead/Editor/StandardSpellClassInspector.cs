// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	[CustomEditor(typeof(Actors.Spells.StandardSpellClass), true)]
	[CanEditMultipleObjects]
	public class StandardSpellClassInspector : SpellClassInspector<Actors.Spells.StandardSpell> { }
}