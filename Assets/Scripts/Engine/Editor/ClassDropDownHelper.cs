// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

public interface ClassDropdownTypes {
	Type[] types { get; }
}

public static class ClassDropDownHelper<T> where T : ClassDropdownTypes, new() {

	static T _types = new T();

	public static string ShowClassDropDown(string label, string current) {
		return ClassDropDownHelper.ShowClassDropDown(_types.types, label, current);
	}

	public static void ShowClassDropDown(string label, SerializedProperty property) {
		ClassDropDownHelper.ShowClassDropDown(_types.types, label, property);
	}
}

public static class ClassDropDownHelper {

	class ClassData {

		public ClassData(Type[] types) {
			_types = types;
			LoadTypes();
		}

		Type[] _types;
		public string[] displayNames;
		public string[] displayNames2;
		public string[] typeNames;
		public Dictionary<string, int> classTypeIndexes;

		void LoadTypes() {
			var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
			var types = ReflectionHelpers.GetTypesThatImplementInterfaces(allAssemblies, _types);

			// only load base types into the list.
			var classTypes = new Dictionary<string, Type>();
			var classShortNames = new Dictionary<string, string>();

			foreach (var t in types) {
				if (typeof(SerializableObject).IsAssignableFrom(t)) {
					var ctor = t.GetConstructor(System.Type.EmptyTypes);
					if (ctor != null) {
						var obj = ctor.Invoke(null) as SerializableObject;
						if (obj.serverType != obj.clientType) {
							var typeName = t.BaseType.FullName;
							if (!classTypes.ContainsKey(typeName)) {
								classTypes[typeName] = t;
								classShortNames[typeName] = t.BaseType.Name;
							}
						} else {
							var typeName = t.FullName;
							classTypes[typeName] = t;
							classShortNames[typeName] = t.Name;
						}
					}
				} else {
					var typeName = t.FullName;
					classTypes[typeName] = t;
					classShortNames[typeName] = t.Name;
				}
			}

			classTypeIndexes = new Dictionary<string, int>();

			var list = new List<string>(classShortNames.Count + 1);
			list.Add("<no class>");

			var list2 = new List<string>(classShortNames.Count);

			var list3 = new List<string>(classShortNames.Count);

			foreach (var name in classShortNames) {
				classTypeIndexes[name.Key] = list.Count;
				list.Add(name.Value);
				list2.Add(name.Value);
				list3.Add(name.Key);
			}

			displayNames = list.ToArray();
			displayNames2 = list2.ToArray();
			typeNames = list3.ToArray();
		}
	}

	static Dictionary<string, ClassData> _classes = new Dictionary<string, ClassData>();

	public static string ShowClassDropDown(Type[] types, string label, string current) {

		if (types.Length < 1) {
			return string.Empty;
		}

		string classTag = "";
		for (int i = 0; i < types.Length; ++i) {
			classTag += types[i].FullName;
		}

		ClassData classData;
		if (!_classes.TryGetValue(classTag, out classData)) {
			classData = new ClassData(types);
			_classes[classTag] = classData;
		}

		var typeIndexes = classData.classTypeIndexes;

		int index;
		if (string.IsNullOrEmpty(current) || !classData.classTypeIndexes.TryGetValue(current, out index)) {
			index = 0;
		}

		var list = classData.displayNames;
		if (index != 0) {
			list = classData.displayNames2;
			index = index - 1;
		} else if (classData.displayNames2.Length > 0) {
			list = classData.displayNames2;
			index = 0;
			current = classData.typeNames[0];
		}

		var newIndex = EditorGUILayout.Popup(label, index, list);

		if (newIndex != index) {
			if (list == classData.displayNames) {
				if (newIndex == 0) {
					current = null;
				} else {
					current = classData.typeNames[newIndex - 1];
				}
			} else {
				current = classData.typeNames[newIndex];
			}
		}

		return current;
	}

	public static string ShowClassDropDown(Rect position, Type[] types, string label, string current) {

		if (types.Length < 1) {
			return string.Empty;
		}

		string classTag = "";
		for (int i = 0; i < types.Length; ++i) {
			classTag += types[i].FullName;
		}

		ClassData classData;
		if (!_classes.TryGetValue(classTag, out classData)) {
			classData = new ClassData(types);
			_classes[classTag] = classData;
		}

		var typeIndexes = classData.classTypeIndexes;

		int index;
		if (string.IsNullOrEmpty(current) || !classData.classTypeIndexes.TryGetValue(current, out index)) {
			index = 0;
		}

		var list = classData.displayNames;
		if (index != 0) {
			list = classData.displayNames2;
			index = index - 1;
		} else if (classData.displayNames2.Length > 0) {
			list = classData.displayNames2;
			index = 0;
			current = classData.typeNames[0];
		}

		var newIndex = EditorGUI.Popup(position, label, index, list);

		if (newIndex != index) {
			if (list == classData.displayNames) {
				if (newIndex == 0) {
					current = null;
				} else {
					current = classData.typeNames[newIndex - 1];
				}
			} else {
				current = classData.typeNames[newIndex];
			}
		}

		return current;
	}

	public static void ShowClassDropDown(Type[] types, string label, SerializedProperty property) {
		property.stringValue = ShowClassDropDown(types, label, property.stringValue);
	}

	public static void ShowClassDropDown(Rect position, Type[] types, string label, SerializedProperty property) {
		property.stringValue = ShowClassDropDown(position, types, label, property.stringValue);
	}
}
