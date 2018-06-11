// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System;

namespace Bowhead.Editor {
	public abstract class ProjectileClassInspector<T> : UnityEditor.Editor where T : Actors.ProjectileActor {

		SerializedProperty projectileClass;

		protected virtual void OnEnable() {
			projectileClass = serializedObject.FindProperty("_projectileClass");
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();

			DrawInspectorGUI();

			serializedObject.ApplyModifiedProperties();
		}

		protected virtual void DrawInspectorGUI() {
			base.OnInspectorGUI();
			ClassDropDownHelper<DropDownTypes<T>>.ShowClassDropDown("Projectile Actor", projectileClass);
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
