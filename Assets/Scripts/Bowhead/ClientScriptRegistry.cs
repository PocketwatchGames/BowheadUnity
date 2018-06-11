// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Bowhead {

	public abstract class ClientScriptRegistry<T> : MonoBehaviourEx where T : ClientScriptRegistry<T> {

#if DEDICATED_SERVER
		protected virtual void Awake() {}
		public static ReadOnlyCollection<T> objects { 
			get {
				return null;
			}
		}
#else
		static List<T> _objects;
		static ReadOnlyCollection<T> _roObjects;

		static ClientScriptRegistry() {
			_objects = new List<T>();
			_roObjects = new ReadOnlyCollection<T>(_objects);
		}

		protected virtual void Awake() {
			_objects.Add((T)this);
		}

		protected override void OnDestroy() {
			_objects.Remove((T)this);
			base.OnDestroy();
		}

		public static ReadOnlyCollection<T> objects {
			get {
				return _roObjects;
			}
		}
#endif

	}
}