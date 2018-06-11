// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;

namespace Bowhead.Editor {
	[CustomEditor(typeof(Actors.DirectProjectileClass), true)]
	[CanEditMultipleObjects]
	public sealed class DirectProjectileClassInspector : ProjectileClassInspector<Actors.DirectProjectileActor> {}
}
