// Copyright (c) 2018 Pocketwatch Games LLC.
using System.Reflection;

namespace Bowhead {
	public sealed class ActorSingleton<T> where T : class {

		World _world;
		T _obj;
		Actor _actor;

		public ActorSingleton(World world) {
			_world = world;
		}

		public static implicit operator T(ActorSingleton<T> self) {
			if (self._obj == null) {
				foreach (var t in self._world.GetActorIterator<T>()) {
					self._obj = t;
					break;
				}

				self._actor = self._obj as Actor;
			}

			return ((self._actor != null) && !self._actor.disposed) ? self._obj : null;
		}

		public T obj {
			get {
				return this;
			}
		}
	}
}
