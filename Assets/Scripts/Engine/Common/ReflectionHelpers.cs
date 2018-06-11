// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;

public class ReflectionHelpers {

	public static List<System.Type> GetTypesThatImplementInterfaces(Assembly[] assemblies, System.Type[] interfaces) {
		List<System.Type> types = new List<System.Type>();

		foreach (var a in assemblies) {
			try {
				foreach (var t in a.GetTypes()) {
					if (t.IsClass && !t.IsAbstract) {
						foreach (var i in interfaces) {
							if (i.IsAssignableFrom(t)) {
								types.Add(t);
							}
						}
					}
				}
			} catch (Exception e) {
				Debug.LogError(e.StackTrace);
			}
		}

		return types;
	}
}
