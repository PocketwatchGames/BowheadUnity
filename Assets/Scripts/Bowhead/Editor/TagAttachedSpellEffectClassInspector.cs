// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	[CustomEditor(typeof(Actors.Spells.TagAttachedSpellEffectClass), true)]
	[CanEditMultipleObjects]
	public class TagAttachedSpellEffectClassInspector : SpellEffectClassInspector<Actors.Spells.TagAttachedSpellEffectActor> { }
}