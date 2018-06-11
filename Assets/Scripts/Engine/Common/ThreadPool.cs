// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

public sealed class ThreadPool : System.IDisposable {

	public static readonly ThreadPool instance;
	ConditionVariable taskQueued = new ConditionVariable();
	Queue<ThreadPoolTask> tasks = new Queue<ThreadPoolTask>();
	List<ThreadPoolThread> threads = new List<ThreadPoolThread>();
	ThreadPoolInternal internals;
	bool exitFlag;

	static ThreadPool() {
		int numThreads = SystemInfo.processorCount - 1;
		Debug.Log("Starting main thread pool with " + numThreads + " thread(s)");
		instance = new ThreadPool(numThreads);
#if UNITY_EDITOR
		EditorEvents.OnEditorStop += OnEditorStop;
#endif
	}

	public static void StartMainThreadPool() {
	}

#if UNITY_EDITOR
	static void OnEditorStop() {
		Debug.Log("... Stoping main thread pool");
		instance.Flush();
		instance.Dispose();
		Debug.Log("Main thread pool stopped.");
	}
#endif

	public ThreadPool(int numThreads) {
		internals = new ThreadPoolInternal(this);

		for (int i = 0; i < numThreads; ++i) {
			threads.Add(new ThreadPoolThread(internals));
		}
	}

	public void Dispose() {
		exitFlag = true;
		taskQueued.NotifyAll();

		foreach (var t in threads) {
			t.Join();
		}

		threads.Clear();
	}

	public void Flush() {
		lock (tasks) {
			while (tasks.Count > 0) {
				tasks.Dequeue().Flush();
			}
		}
	}

	public void Queue(ThreadPoolTask task) {
		lock (tasks) {
			tasks.Enqueue(task);
		}
		taskQueued.NotifyOne();
	}

	ThreadPoolTask Dequeue() {
		lock (tasks) {
			while (!exitFlag && (tasks.Count < 1)) {
				taskQueued.Wait(tasks);
			}

			if (exitFlag) {
				return null;
			}

			return tasks.Dequeue();
		}
	}

	public class ThreadPoolInternal {
		ThreadPool pool;

		public ThreadPoolInternal(ThreadPool pool) {
			this.pool = pool;
		}

		public ThreadPoolTask Dequeue() {
			return pool.Dequeue();
		}

		public bool exitFlag {
			get {
				return pool.exitFlag;
			}
		}
	}
}

public interface ThreadPoolTask {
	// Execute task
	void Run();
	// Task was flushed from queue and will not be run.
	void Flush();
}

public abstract class PooledThreadPoolTask<T> : ThreadPoolTask where T : PooledThreadPoolTask<T> {
	static CustomAllocatedObjectPool<T> pool;


	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator) {
		StaticInit(allocator, 0);
	}

	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator, int initialSize) {
		pool = new CustomAllocatedObjectPool<T>(allocator, null, initialSize);
	}

	protected static T NewTask() {
		lock (pool) {
			return pool.GetObject();
		}
	}

	public void Run() {
		OnRun();
		lock (pool) {
			pool.ReturnObject((T)this);
		}
	}

	public void Flush() {
		OnFlush();
		lock (pool) {
			pool.ReturnObject((T)this);
		}
	}

	protected abstract void OnRun();
	protected abstract void OnFlush();
}

public sealed class ThreadPoolActionRunnerTask : PooledThreadPoolTask<ThreadPoolActionRunnerTask> {
	System.Action action;

	static ThreadPoolActionRunnerTask() {
		StaticInit(Allocator);
	}

	ThreadPoolActionRunnerTask() {
	}

	public static ThreadPoolActionRunnerTask New(System.Action action) {
		var task = NewTask();
		task.action = action;
		return task;
	}

	static ThreadPoolActionRunnerTask Allocator() {
		return new ThreadPoolActionRunnerTask();
	}

	protected override void OnRun() {
		action();
	}

	protected override void OnFlush() {
	}
}

public class ThreadPoolThread {

	ThreadPool.ThreadPoolInternal internals;
	System.Threading.Thread thread;

	public ThreadPoolThread(ThreadPool.ThreadPoolInternal internals) {
		this.internals = internals;
		thread = new System.Threading.Thread(ThreadProc);
		thread.Start();
	}

	public void Join() {
		thread.Join();
	}

	void ThreadProc() {
		while (!internals.exitFlag) {
			var task = internals.Dequeue();
			if (task != null) {
				try {
					task.Run();
				} catch (System.Exception e) {
					Debug.LogError(e.Message + "\n" + e.StackTrace);
#if UNITY_EDITOR
					UnityEditor.EditorApplication.isPaused = true;
#endif
				}
			}
		}
	}
}
