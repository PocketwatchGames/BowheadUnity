// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	public class SpellEffectClassInspector<T> : UnityEditor.Editor where T : Actors.Spells.SpellEffectActor  {
		SerializedProperty actorClass;

		protected virtual void OnEnable() {
			actorClass = serializedObject.FindProperty("spellEffectActorClassString");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void DrawInspectorGUI() {
			base.OnInspectorGUI();
			ClassDropDownHelper<DropDownTypes<T>>.ShowClassDropDown("Actor Class", actorClass);
		}

		sealed class DropDownTypes<Z> : ClassDropdownTypes {
			static readonly Type[] _types = new[] { typeof(Z) };

			public Type[] types {
				get {
					return _types;
				}
			}
		}

	}
}