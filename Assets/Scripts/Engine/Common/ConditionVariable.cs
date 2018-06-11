// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;
using System.Threading;

public class ConditionVariable {

	CustomAllocatedObjectPool<LinkedListNode<ManualResetEvent>> eventPool;
	LinkedList<ManualResetEvent> waitQueue = new LinkedList<ManualResetEvent>();

	public ConditionVariable() {
		eventPool = new CustomAllocatedObjectPool<LinkedListNode<ManualResetEvent>>(AllocateEvent, null);
	}

	LinkedListNode<ManualResetEvent> AllocateEvent() {
		return new LinkedListNode<ManualResetEvent>(new ManualResetEvent(false));
	}

	public void Wait(Mutex mutex) {
		LinkedListNode<ManualResetEvent> ev;
		lock (eventPool) {
			ev = eventPool.GetObject();
			waitQueue.AddLast(ev);
			mutex.ReleaseMutex();
		}
		ev.Value.WaitOne();
		ev.Value.Reset();
		lock (eventPool) {
			eventPool.ReturnObject(ev);
		}
		mutex.WaitOne();
	}

	public void Wait(object monitor) {
		LinkedListNode<ManualResetEvent> ev;
		lock (eventPool) {
			ev = eventPool.GetObject();
			waitQueue.AddLast(ev);
			Monitor.Exit(monitor);
		}
		ev.Value.WaitOne();
		ev.Value.Reset();
		lock (eventPool) {
			eventPool.ReturnObject(ev);
		}
		while (true) {
			try {
				Monitor.Enter(monitor);
				break;
			} catch (ThreadInterruptedException) {

			}
		}
	}

	public void NotifyOne() {
		LinkedListNode<ManualResetEvent> ev;
		lock (eventPool) {
			ev = waitQueue.First;
			if (ev != null) {
				waitQueue.Remove(ev);
			}
		}
		if (ev != null) {
			ev.Value.Set();
		}
	}

	public void NotifyAll() {
		lock (eventPool) {
			while (waitQueue.First != null) {
				waitQueue.First.Value.Set();
				waitQueue.RemoveFirst();
			}
		}
	}
}
