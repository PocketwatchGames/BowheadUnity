// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Bowhead {

	public enum EBTResult {
		Fail,
		Success,
		Running
	}

	public abstract class BTNode : IDisposable {

		IEnumerator _it;
		Stack<IEnumerator> _stack;
		double _nextConditionCheck;
		bool _enter;
		bool _lastCondition;

		static protected readonly IEnumerator STILL_RUNNING = new StillRunning();

		class StillRunning : IEnumerator {
			public void Dispose() { }

			public bool MoveNext() {
				return false;
			}

			public void Reset() {}

			public object Current {
				get {
					return null;
				}
			}
		}

		public EBTResult Tick() {
			return Tick(GameManager.instance.gameTimeSinceStart, GameManager.instance.gameTimeDelta);
		}

		public EBTResult Tick(double time, double dt) {
			this.time = time;
			deltaTime = dt;
			return TickNext();		
		}

		public void Dispose() {
			OnDispose();
		}

		public void RunAsCoroutine(MonoBehaviour context) {
			context.StartCoroutine(CoRun());
		}

		public IEnumerator CoRun() {
			while (Tick(GameManager.instance.gameTimeSinceStart, GameManager.instance.gameTimeDelta) == EBTResult.Running) {
				yield return null;
			}
		}

		EBTResult TickNext() {
			state = EBTResult.Fail;
			if (CheckCondition()) {

				if (_it == null) {
					_enter = true;
					OnEnter();
					OnTick();
					_it = Run();
				} else {
					OnTick();
					if (_it == STILL_RUNNING) {
						_it = Run();
					}
				}

				if (_it == STILL_RUNNING) {
					state = EBTResult.Running;
					return EBTResult.Running;
				}

				while (_it != null) {
					if (_it.MoveNext()) {
						var cur = _it.Current as IEnumerator;
						if (cur != null) {
							if (_stack == null) {
								_stack = new Stack<IEnumerator>();
							}
							_stack.Push(_it);
							_it = cur;
						}
						state = EBTResult.Running;
						return EBTResult.Running;
					}

					_it = null;

					if ((_stack != null) && (_stack.Count > 0)) {
						_it = _stack.Pop();
					}
				}

				if (_it == null) {
					OnExit(state);
					_enter = false;
				}

			} else {
				_it = null;
				if (_stack != null) {
					_stack.Clear();
				}
				if (_enter) {
					OnExit(state);
					_enter = false;
				}
			}
			return state;
		}

		bool CheckCondition() {
			_nextConditionCheck -= deltaTime;
			if (_nextConditionCheck <= 0f) {
				_nextConditionCheck = conditionRate;
				_lastCondition = Condition();
			}
			return _lastCondition;
		}

		public void Abort() {
			Reset();
		}

		protected virtual void OnEnter() { }
		protected virtual void OnExit(EBTResult result) { }
		protected virtual void OnTick() { }

		protected virtual bool Condition() { return true; }

		protected virtual float conditionRate {
			get {
				return 0f;
			}
		}

		protected virtual void Reset() {
			state = EBTResult.Fail;
			if (_it != null) {
				_it = null;
				if (_stack != null) {
					_stack.Clear();
				}
				OnExit(EBTResult.Fail);
				_enter = false;
			}
		}

		protected abstract IEnumerator Run();
		
		protected virtual void OnDispose() {
			if (state == EBTResult.Running) {
				Reset();
			}
		}

		protected EBTResult GetOtherState(BTNode other) {
			return other.state;
		}

		protected void SetTime(BTNode other) {
			other.time = time;
			other.deltaTime = deltaTime;
		}

		protected void Reset(BTNode other) {
			other.Reset();
		}

		protected void OnExit(BTNode other, EBTResult result) {
			other.state = result;
			if (other._it != null) {
				other._it = null;
				if (other._stack != null) {
					other._stack.Clear();
				}
				other.OnExit(result);
				other._enter = false;
			}
		}

		public EBTResult state {
			get;
			protected set;
		}

		protected double time {
			get;
			private set;
		}

		protected double deltaTime {
			get;
			private set;
		}

			public bool running {
			get {
				return _enter;
			}
		}
	}

	public abstract class BTNode<T> : BTNode {

		protected BTNode(T args) {
			this.args = args;
		}
		
		public T args {
			get;
			set;
		}
	}

	public abstract class BTCompositeNode : BTNode {

		protected X AddChild<X>(X node) where X : BTNode {
			if (children == null) {
				children = new List<BTNode>();
			}
			children.Add(node);
			return node;
		}

		protected void SetChild<X>(int index, X node) where X : BTNode {
			children[index] = node;
		}

		protected List<BTNode> children {
			get;
			private set;
		}
	}

	public abstract class BTCompositeNode<T> : BTNode<T> {

		protected BTCompositeNode(T args) : base(args) { }

		protected X AddChild<X>(X node) where X: BTNode {
			if (children == null) {
				children = new List<BTNode>();
			}
			children.Add(node);
			return node;
		}

		protected void SetChild<X>(int index, X node) where X : BTNode {
			children[index] = node;
		}

		protected List<BTNode> children {
			get;
			private set;
		}
	}

	public class BTSequenceNode : BTCompositeNode {
		
		BTNode _child;
		int _curChild;

		protected override void Reset() {
			base.Reset();

			_curChild = 0;
			if (_child != null) {
				Reset(_child);
				_child = null;
			}
		}

		protected override void OnEnter() {
			base.OnEnter();
			Assert.IsTrue(_curChild == 0);
			Assert.IsNull(_child);
		}

		protected override void OnExit(EBTResult result) {
			_curChild = 0;
			if (_child != null) {
				OnExit(_child, result);
				_child = null;
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (; _curChild < children.Count; ++_curChild) {
					_child = children[_curChild];
					if (_child != null) {
						var r = _child.Tick(time, deltaTime);
						if (r == EBTResult.Fail) {
							_child = null;
							_curChild = 0;
							return null;
						} else if (r == EBTResult.Running) {
							return STILL_RUNNING;
						}
					}
				}

				state = EBTResult.Success;
				_curChild = 0;
				_child = null;
			}

			return null;
		}
	}

	public class BTSequenceNode<T> : BTCompositeNode<T> {

		public BTSequenceNode(T args) : base(args) { }

		BTNode _child;
		int _curChild;

		protected override void Reset() {
			base.Reset();

			_curChild = 0;
			if (_child != null) {
				Reset(_child);
				_child = null;
			}
		}

		protected override void OnEnter() {
			base.OnEnter();
			Assert.IsTrue(_curChild == 0);
			Assert.IsNull(_child);
		}

		protected override void OnExit(EBTResult result) {
			_curChild = 0;
			if (_child != null) {
				OnExit(_child, result);
				_child = null;
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (; _curChild < children.Count; ++_curChild) {
					_child = children[_curChild];
					if (_child != null) {
						var r = _child.Tick(time, deltaTime);
						if (r == EBTResult.Fail) {
							_child = null;
							_curChild = 0;
							return null;
						} else if (r == EBTResult.Running) {
							return STILL_RUNNING;
						}
					}
				}

				state = EBTResult.Success;
				_curChild = 0;
				_child = null;
			}

			return null;
		}
	}

	public class BTSelectorNode : BTCompositeNode {

		BTNode _child;
		int _curChild;

		protected override void Reset() {
			base.Reset();

			_curChild = 0;
			if (_child != null) {
				Reset(_child);
				_child = null;
			}
		}

		protected override void OnEnter() {
			base.OnEnter();
			Assert.IsTrue(_curChild == 0);
			Assert.IsNull(_child);
		}

		protected override void OnExit(EBTResult result) {
			_curChild = 0;
			if (_child != null) {
				OnExit(_child, result);
				_child = null;
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (; _curChild < children.Count; ++_curChild) {
					_child = children[_curChild];
					if (_child != null) {
						var r = _child.Tick(time, deltaTime);
						if (r == EBTResult.Success) {
							state = EBTResult.Success;
							_child = null;
							_curChild = 0;
							return null;
						} else if (r == EBTResult.Running) {
							return STILL_RUNNING;
						}
					}
				}
				_child = null;
				_curChild = 0;
			}

			return null;
		}
	}

	public class BTSelectorNode<T> : BTCompositeNode<T> {
		public BTSelectorNode(T args) : base(args) { }

		BTNode _child;
		int _curChild;

		protected override void Reset() {
			base.Reset();

			_curChild = 0;
			if (_child != null) {
				Reset(_child);
				_child = null;
			}
		}

		protected override void OnEnter() {
			base.OnEnter();
			Assert.IsTrue(_curChild == 0);
			Assert.IsNull(_child);
		}

		protected override void OnExit(EBTResult result) {
			_curChild = 0;
			if (_child != null) {
				OnExit(_child, result);
				_child = null;
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (; _curChild < children.Count; ++_curChild) {
					_child = children[_curChild];
					if (_child != null) {
						var r = _child.Tick(time, deltaTime);
						if (r == EBTResult.Success) {
							state = EBTResult.Success;
							_child = null;
							_curChild = 0;
							return null;
						} else if (r == EBTResult.Running) {
							return STILL_RUNNING;
						}
					}
				}
				_child = null;
				_curChild = 0;
			}

			return null;
		}
	}

	public abstract class BTDecorator<T> : BTNode where T: BTNode {
		protected T inner {
			get;
			set;
		}

		public BTDecorator() { }

		public BTDecorator(T inner) {
			this.inner = inner;
		}

		protected override IEnumerator Run() {
			if (inner != null) {
				var r = inner.Tick();

				if (r == EBTResult.Running) {
					return STILL_RUNNING;
				}

				state = r;
			}
			return null;
		}

		protected override void Reset() {
			if (inner != null) {
				Reset(inner);
			}
			base.Reset();
		}
	}

	public abstract class BTDecorator<T, X> : BTNode<X> where T: BTNode {
		protected T inner {
			get;
			set;
		}

		public BTDecorator(X args) : base(args) {}

		public BTDecorator(T inner, X args) : base(args) {
			this.inner = inner;
		}
		
		protected override IEnumerator Run() {
			if (inner != null) {
				var r = inner.Tick();

				if (r == EBTResult.Running) {
					return STILL_RUNNING;
				}

				state = r;
			}
			return null;
		}

		protected override void OnExit(EBTResult result) {
			if ((inner != null) && (inner.state == EBTResult.Running)) {
				OnExit(inner, result);
			}
			base.OnExit(result);
		}

		protected override void Reset() {
			if (inner != null) {
				Reset(inner);
			}
			base.Reset();
		}
	}

	public class BTParallel : BTCompositeNode {

		protected override void OnExit(EBTResult result) {
			if (children != null) {
				for (int i = 0; i < children.Count; ++i) {
					var child = children[i];
					if (child.state == EBTResult.Running) {
						OnExit(child, result);
					}
				}
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (int i = 0; i < children.Count; ++i) {
					var child = children[i];
					child.Tick();
				}
			}

			return STILL_RUNNING;
		}
	}

	public class BTParallel<T> : BTCompositeNode<T> {

		public BTParallel(T args) : base(args) { }

		protected override void OnExit(EBTResult result) {
			if (children != null) {
				for (int i = 0; i < children.Count; ++i) {
					var child = children[i];
					if (child.state == EBTResult.Running) {
						OnExit(child, result);
					}
				}
			}

			base.OnExit(result);
		}

		protected override IEnumerator Run() {
			if (children != null) {
				for (int i = 0; i < children.Count; ++i) {
					var child = children[i];
					child.Tick();
				}
			}

			return STILL_RUNNING;
		}
	}
}