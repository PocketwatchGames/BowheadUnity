// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

public sealed class LeakTracker {

	struct TypeCount {
		public readonly Type type;
		public readonly FieldInfo alive;
		public readonly FieldInfo total;

		public TypeCount(Type type, FieldInfo alive, FieldInfo total) {
			this.type = type;
			this.alive = alive;
			this.total = total;
		}
	}
	
	List<TypeCount> _types = new List<TypeCount>();

	public LeakTracker(IList<Assembly> asms) {
		foreach (var asm in asms) {
			foreach (var type in asm.GetTypes()) {
				TryAddType(type);
			}
		}

		_types.Sort((x, y) => { return x.type.FullName.CompareTo(y.type.FullName); });
	}

	public void WriteCSV(string filename, bool leakedOnly) {
		using (var file = new StreamWriter(filename, false, System.Text.Encoding.ASCII)) {
			file.WriteLine("Name,Alive,Total");
			foreach (var count in _types) {
				var total = (long)count.total.GetValue(null);
				var alive = (long)count.alive.GetValue(null);
				if ((leakedOnly && (alive > 0)) || (!leakedOnly && (total > 0))) {
					file.WriteLine(count.type.FullName + ", " + alive.ToString() + ", " + total.ToString());
				}
			}
			Debug.Log("Wrote " + filename);
		}
	}

	void TryAddType(Type type) {
		if (!type.ContainsGenericParameters) {
			FieldInfo alive = null;
			FieldInfo total = null;

			foreach (var field in type.GetFields()) {
				if (field.Name == "_leakTrace_Total") {
					total = field;
				} else if (field.Name == "_leakTrace_Alive") {
					alive = field;
				}

				if ((total != null) && (alive != null)) {
					break;
				}
			}

			if ((alive != null) && (total != null)) {
				_types.Add(new TypeCount(type, alive, total));
			}
		}

		foreach (var nested in type.GetNestedTypes()) {
			TryAddType(nested);
		}
	}
}
