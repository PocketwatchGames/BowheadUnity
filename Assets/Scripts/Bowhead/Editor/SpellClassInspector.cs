// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	public class SpellClassInspector<T> : UnityEditor.Editor where T : Actors.Spells.Spell  {
		SerializedProperty spellClass;

		protected virtual void OnEnable() {
			spellClass = serializedObject.FindProperty("spellClassString");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void DrawInspectorGUI() {
			base.OnInspectorGUI();
			ClassDropDownHelper<DropDownTypes<T>>.ShowClassDropDown("Spell Class", spellClass);
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