using UnityEngine;
using System.Collections.Generic;

public sealed class TaskQueue {

	public interface Task {
		// Execute task
		void Run();
		// Task was flushed from queue and will not be run.
		void Flush();
	}

	Queue<Task> tasks = new Queue<Task>();

	public void Flush() {
		lock (tasks) {
			while (tasks.Count > 0) {
				tasks.Dequeue().Flush();
			}
		}
	}

	public void Queue(Task task) {
		lock (tasks) {
			tasks.Enqueue(task);
		}
	}

	public Task Dequeue() {
		lock (tasks) {
			if (tasks.Count < 1) {
				return null;
			}

			return tasks.Dequeue();
		}
	}

}

public abstract class PooledTaskQueueTask<T> : TaskQueue.Task where T : PooledTaskQueueTask<T> {
	static CustomAllocatedObjectPool<T> pool;

	static PooledTaskQueueTask() {
	}


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

public sealed class TaskQueueActionRunnerTask : PooledTaskQueueTask<TaskQueueActionRunnerTask> {
	System.Action action;

	static TaskQueueActionRunnerTask() {
		StaticInit(Allocator);
	}

	TaskQueueActionRunnerTask() {
	}

	public static TaskQueueActionRunnerTask New(System.Action action) {
		var task = NewTask();
		task.action = action;
		return task;
	}

	static TaskQueueActionRunnerTask Allocator() {
		return new TaskQueueActionRunnerTask();
	}

	protected override void OnRun() {
		action();
	}

	protected override void OnFlush() {
	}
}

public sealed class MainThreadTaskQueue {
	static TaskQueue queue = new TaskQueue();

	MainThreadTaskQueue() { }

	public static void Queue(TaskQueue.Task task) {
		queue.Queue(task);
	}

	public static void Flush() {
		queue.Flush();
	}

	public static void Run(int ms) {
		using (var handle = StopWatchPool.New()) {
			handle.stopWatch.Start();
			do {
				var task = queue.Dequeue();
				if (task == null) {
					break;
				}
				task.Run();
			} while (handle.stopWatch.ElapsedMilliseconds < ms);
		}
	}
}