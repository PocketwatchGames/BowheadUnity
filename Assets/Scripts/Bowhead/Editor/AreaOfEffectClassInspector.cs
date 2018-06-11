using UnityEngine;
using UnityEditor;
using System;
using Bowhead.Actors.Spells;

namespace Bowhead.Editor {
	[CustomEditor(typeof(AreaOfEffectClass), true)]
	[CanEditMultipleObjects]
	public sealed class AreaOfEffectClassInspector : AreaOfEffectClassInspector<AreaOfEffectActor> { }

	public class AreaOfEffectClassInspector<T> : UnityEditor.Editor where T : AreaOfEffectActor  {
		SerializedProperty actorClass;

		protected virtual void OnEnable() {
			actorClass = serializedObject.FindProperty("areaOfEffectActorClassString");
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