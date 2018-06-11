// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	[CustomEditor(typeof(Actors.Spells.BleedSpellEffectClass), true)]
	[CanEditMultipleObjects]
	public class BleedSpellEffectClassInspector : SpellEffectClassInspector<Actors.Spells.BleedSpellEffectActor> { }
}