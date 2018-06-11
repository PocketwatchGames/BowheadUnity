// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System;
using System.Collections.Generic;

namespace Bowhead {
	public abstract class ObjectMetaClass<T> : ScriptableObject, ISerializationCallbackReceiver where T : ObjectMetaClass<T> {
		public T[] childClasses;
		HashSet<T> _children = new HashSet<T>();
		T _parent;
		int _depth;

		public bool IsA(T metaClass) {
			return ReferenceEquals(metaClass, this) || metaClass._children.Contains((T)this);
		}

		public bool IsAny(IList<T> metaClasses) {
			if (metaClasses != null) {
				for (int i = 0; i < metaClasses.Count; ++i) {
					var metaClass = metaClasses[i];
					if ((metaClass != null) && IsA(metaClass)) {
						return true;
					}
				}
			}
			return false;
		}

		// return true if any class in metaClasses is a testClass.
		public static bool IsA(IList<T> metaClasses, T testClass) {
			if (metaClasses != null) {
				for (int i = 0; i < metaClasses.Count; ++i) {
					var metaClass = metaClasses[i];
					if ((metaClass != null) && metaClass.IsA(testClass)) {
						return true;
					}
				}
			}
			return false;
		}

		public bool IsParentClass(T metaClass) {
			if (metaClass == null) {
				return true;
			}
			return _children.Contains(metaClass);
		}

		public void OnBeforeSerialize() { }

		public void OnAfterDeserialize() {
			_depth = 0;
			_children.Clear();

			//if (_parent != null) {
			//	Debug.Log(name + ", parent = " + _parent.name);
			//} else {
			//	Debug.Log(name);
			//}

			if ((childClasses != null) && (childClasses.Length > 0)) {
				foreach (var child in childClasses) {
					if (child != null) {
						if (child == this) {
							Debug.LogError("Metaclass recursion: " + name + " is a child of itself!");
							continue;
						} else if (child._parent == null) {
							//Debug.Log(child.name + ".parent = " + name);
							child._parent = (T)this;
						} else if (child._parent.GetInstanceID() != GetInstanceID()) {
							Debug.LogError(child.name + " is a child of " + name + " and " + child._parent.name);
						}

						child.OnAfterDeserialize();
					}
				}
			}

			_depth = 0;
			for (var parent = _parent; parent != null; parent = parent._parent) {
				++_depth;

				if (parent == this) {
					Debug.LogError("Metaclass recursion: " + name + " is a child of itself!");
					continue;
				}

				parent._children.Add((T)this);

				if ((childClasses != null) && (childClasses.Length > 0)) {
					foreach (var child in childClasses) {
						if (child != null) {
							parent._children.Add(child);
						}
					}
				}
			}
		}

		public T parent {
			get {
				return _parent;
			}
		}

		public int depth {
			get {
				return _depth;
			}
		}
	}
}