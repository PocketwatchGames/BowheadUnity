// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

namespace Bowhead.Editor {
	[CustomEditor(typeof(Actors.ExplosionProjectileClass), true)]
	[CanEditMultipleObjects]
	public sealed class ExplosionProjectileClassInspector : ProjectileClassInspector<Actors.ExplosionProjectileActor> { }
}