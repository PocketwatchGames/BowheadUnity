using UnityEngine;
using System.Collections.Generic;

public interface StopWatchHandle : System.IDisposable {
	System.Diagnostics.Stopwatch stopWatch {
		get;
	}
}

public sealed class StopWatchPool {

	sealed class Handle : StopWatchHandle {

		System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();

		public System.Diagnostics.Stopwatch stopWatch {
			get {
				return watch;
			}
		}

		public void Dispose() {
			pool.ReturnObject(this);
		}
	}

	static ObjectPool<Handle> pool = new ObjectPool<Handle>();

	StopWatchPool() { }

	public static StopWatchHandle New() {
		var watch = pool.GetObject();
		watch.stopWatch.Reset();
		return watch;
	}
}
