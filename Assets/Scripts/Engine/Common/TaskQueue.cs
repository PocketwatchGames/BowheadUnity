using UnityEngine;
using System.Collections.Generic;

public sealed class TaskQueue {

	public interface ITask {
		// Execute task
		void Run();
		// Task was flushed from queue and will not be run.
		void Flush();
	}

	Queue<ITask> tasks = new Queue<ITask>();

	public void Flush() {
		lock (tasks) {
			while (tasks.Count > 0) {
				tasks.Dequeue().Flush();
			}
		}
	}

	public void Queue(ITask task) {
		lock (tasks) {
			tasks.Enqueue(task);
		}
	}

	public ITask Dequeue() {
		lock (tasks) {
			if (tasks.Count < 1) {
				return null;
			}

			return tasks.Dequeue();
		}
	}

}

public abstract class PooledTaskQueueTask<T> : TaskQueue.ITask where T : PooledTaskQueueTask<T> {
	static CustomAllocatedObjectPool<T> _pool;

	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator, CustomAllocatedObjectPool<T>.FreeDelegate free, int maxItems) {
		StaticInit(allocator, free, 0, maxItems);
	}

	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator, CustomAllocatedObjectPool<T>.FreeDelegate free, int initialSize, int maxItems) {
		_pool = new CustomAllocatedObjectPool<T>(allocator, free, initialSize, maxItems);
	}

	protected static T NewTask() {
		return _pool.GetObject();
	}

	public void Run() {
		OnRun();
		_pool.ReturnObject((T)this);
	}

	public void Flush() {
		OnFlush();
		_pool.ReturnObject((T)this);
	}

	static protected void ResetPool(int initialSize, CustomAllocatedObjectPool<T>.FreeDelegate free) {
		_pool.Reset(initialSize, free);
	}

	protected abstract void OnRun();
	protected abstract void OnFlush();
}

public abstract class ThreadSafePooledTaskQueueTask<T> : TaskQueue.ITask where T : ThreadSafePooledTaskQueueTask<T> {
	static CustomAllocatedObjectPool<T> _pool;

	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator, CustomAllocatedObjectPool<T>.FreeDelegate free, int maxItems) {
		StaticInit(allocator, free, 0, maxItems);
	}

	protected static void StaticInit(CustomAllocatedObjectPool<T>.AllocateDelegate allocator, CustomAllocatedObjectPool<T>.FreeDelegate free, int initialSize, int maxItems) {
		_pool = new CustomAllocatedObjectPool<T>(allocator, free, initialSize, maxItems);
	}

	protected static T NewTask() {
		lock (_pool) {
			return _pool.GetObject();
		}
	}

	public void Run() {
		OnRun();
		lock (_pool) {
			_pool.ReturnObject((T)this);
		}
	}

	public void Flush() {
		OnFlush();
		lock (_pool) {
			_pool.ReturnObject((T)this);
		}
	}

	static protected void ResetPool(int initialSize, CustomAllocatedObjectPool<T>.FreeDelegate free) {
		lock (_pool) {
			_pool.Reset(initialSize, free);
		}
	}

	protected abstract void OnRun();
	protected abstract void OnFlush();
}

public sealed class TaskQueueActionRunnerTask : PooledTaskQueueTask<TaskQueueActionRunnerTask> {
	System.Action action;

	static TaskQueueActionRunnerTask() {
		StaticInit(New, null, 0);
	}

	public static TaskQueueActionRunnerTask New(System.Action action) {
		var task = NewTask();
		task.action = action;
		return task;
	}

	static TaskQueueActionRunnerTask New() {
		return new TaskQueueActionRunnerTask();
	}

	static public void ResetPool() {
		ResetPool(0, null);
	}

	protected override void OnRun() {
		action();
	}

	protected override void OnFlush() {}
}

public sealed class ThreadSafeTaskQueueActionRunnerTask : PooledTaskQueueTask<ThreadSafeTaskQueueActionRunnerTask> {
	System.Action action;

	static ThreadSafeTaskQueueActionRunnerTask() {
		StaticInit(New, null, 0);
	}

	public static ThreadSafeTaskQueueActionRunnerTask New(System.Action action) {
		var task = NewTask();
		task.action = action;
		return task;
	}

	static ThreadSafeTaskQueueActionRunnerTask New() {
		return new ThreadSafeTaskQueueActionRunnerTask();
	}

	public void ResetPool() {
		ResetPool(0, null);
	}

	protected override void OnRun() {
		action();
	}

	protected override void OnFlush() {}
}

public sealed class MainThreadTaskQueue {
	static TaskQueue queue = new TaskQueue();

	MainThreadTaskQueue() { }

	public static int maxFrameTimeMicroseconds;

	public static void Queue(TaskQueue.ITask task) {
		queue.Queue(task);
	}

	public static void Flush() {
		queue.Flush();
	}

	public static void Run() {
		var start = Utils.ReadMicroseconds();
		do {
			var task = queue.Dequeue();
			if (task == null) {
				break;
			}
			task.Run();
		} while ((Utils.ReadMicroseconds()-start) < maxFrameTimeMicroseconds);
	}
}