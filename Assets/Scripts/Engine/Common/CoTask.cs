// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public sealed class CoTaskInternal {
	bool _done;
	System.Func<bool> action;

	public CoTaskInternal(System.Func<bool> action) {
		this.action = action;
	}

	public bool Run() {
		return action();
	}

	public bool done {
		get {
			return _done;
		}
		set {
			_done = value;
		}
	}
}

public class CoTask {

	CoTaskInternal state;

	public CoTask() {
	}

	public void Attach(CoTaskInternal state) {
		this.state = state;
	}

	public bool done {
		get {
			return state.done;
		}
	}
}

public sealed class CoTaskQueue {

	bool _running;
	int queueCount;
	List<CoTaskInternal> ops = new List<CoTaskInternal>();

	readonly int maxMilliseconds;

	public CoTaskQueue(int maxMillisecondsPerFrame) {
		maxMilliseconds = maxMillisecondsPerFrame;
	}

	public T AddTask<T>(System.Func<bool> action) where T : CoTask, new() {
		var task = new CoTaskInternal(action);
		var t = new T();
		t.Attach(task);
		ops.Add(task);
		if (_running) {
			++queueCount;
		}
		return t;
	}

	public void Run(MonoBehaviour script) {
		if ((ops.Count > 0) && !_running) {
			queueCount = ops.Count;
			_running = true;
			script.StartCoroutine(RunQueue());
		}
	}

	public void Clear() {
		ops.Clear();
		_running = false;
		queueCount = 0;
	}

	public bool running {
		get {
			return _running;
		}
	}

	public bool empty {
		get {
			return ops.Count == 0;
		}
	}

	public float progress {
		get {
			if (_running) {
				return 1f - (ops.Count / ((float)queueCount));
			} else {
				return 1f;
			}
		}
	}

	IEnumerator RunQueue() {
		while (ops.Count > 0) {
			using (var timer = StopWatchPool.New()) {
				timer.stopWatch.Start();
				for (int i = 0; i < ops.Count;) {
					try {
						if (ops[i].Run()) {
							ops.RemoveAt(i);
						} else {
							++i;
						}
					} catch (System.Exception e) {
						Debug.LogException(e);
						ops.RemoveAt(i);
					}
					if (timer.stopWatch.ElapsedMilliseconds >= maxMilliseconds) {
						break;
					}
				}
			}
			if (ops.Count == 0) {
				break;
			}
			yield return null;
		}

		_running = false;
	}
}

public sealed class CoRunIEnumerator {
	Stack<IEnumerator> _stack;
	IEnumerator _cur;

	public CoRunIEnumerator(IEnumerator enumerator) {
		_cur = enumerator;
	}

	public bool Step() {

		while (_cur != null) {
			if (_cur.MoveNext()) {
				var cur = _cur.Current as IEnumerator;
				if (cur != null) {
					if (_stack == null) {
						_stack = new Stack<IEnumerator>();
					}
					_stack.Push(_cur);
					_cur = cur;
				}
				break;
			} else {
				_cur = null;
				if ((_stack != null) && (_stack.Count > 0)) {
					_cur = _stack.Pop();
				}
			}
		}

		return _cur != null;
	}

	public bool done {
		get {
			return _cur == null;
		}
	}
}
