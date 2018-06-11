// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

public delegate bool InputEventFilter(Event e);

public static class MouseButton {
	public const int Left = 0;
	public const int Right = 1;
	public const int Middle = 2;
}

public interface InputKeyState {
	bool pressed { get; }
	bool released { get; }
	bool doubleClicked { get; }
	bool down { get; }
	bool repeat { get; }
	float heldTime { get; }
	void Flush();
}

public interface InputKeyChord : InputKeyState {

	void Unregister();

	ReadOnlyCollection<InputKey> keys { get; }
	ReadOnlyCollection<int> buttons { get; }
}

public interface InputAction : InputKeyState {

	void DebugLog();
	void Unregister();

	string name { get; }

	ReadOnlyCollection<InputKey> keys { get; }
	ReadOnlyCollection<int> buttons { get; }
}

public class InputActionContainer {
	public List<InputAction> _actions = new List<InputAction>();

	public InputActionContainer() { }

	public InputKeyAction CreateKeyAction(string name, InputKey key) {
		var a = new InputKeyAction(name, key);
		_actions.Add(a);
		return a;
	}

	public InputKeyAction CreateKeyAction(string name, int button) {
		var a = new InputKeyAction(name, button);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, IList<InputKey> keys, IList<int> buttons) {
		var a = new InputChordAction(name, keys, buttons);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, InputKey key) {
		var a = new InputChordAction(name, key);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, IList<InputKey> keys) {
		var a = new InputChordAction(name, keys);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, InputKey key, int button) {
		var a = new InputChordAction(name, key, button);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, int button) {
		var a = new InputChordAction(name, button);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, InputKey key, IList<int> buttons) {
		var a = new InputChordAction(name, key, buttons);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, IList<int> buttons) {
		var a = new InputChordAction(name, buttons);
		_actions.Add(a);
		return a;
	}

	public InputChordAction CreateChordAction(string name, IList<InputKey> keys, int button) {
		var a = new InputChordAction(name, keys, button);
		_actions.Add(a);
		return a;
	}

	public void Remove(InputAction action) {
		_actions.Remove(action);
		action.Unregister();
	}

	public void ClearActions() {
		for (int i = 0; i < _actions.Count; ++i) {
			_actions[i].Unregister();
		}
		_actions.Clear();
	}

	public void Flush() {
		for (int i = 0; i < _actions.Count; ++i) {
			_actions[i].Flush();
		}
	}
}

public sealed class UnboundInputAction : InputAction {
	string _name;

	static readonly ReadOnlyCollection<InputKey> _emptyKeys = new ReadOnlyCollection<InputKey>(new InputKey[0]);
	static readonly ReadOnlyCollection<int> _emptyButtons = new ReadOnlyCollection<int>(new int[0]);

	public UnboundInputAction(string name) {
		_name = name;
	}

	public void DebugLog() { }

	public void Unregister() { }

	InputKeyState inputState {
		get {
			return this;
		}
	}

	public void Flush() {}

	public string name {
		get {
			return _name;
		}
	}

	public ReadOnlyCollection<InputKey> keys {
		get {
			return _emptyKeys;
		}
	}

	public ReadOnlyCollection<int> buttons {
		get {
			return _emptyButtons;
		}
	}

	public bool pressed {
		get {
			return false;
		}
	}

	public bool released {
		get {
			return false;
		}
	}

	public bool doubleClicked {
		get {
			return false;
		}
	}

	public bool down {
		get {
			return false;
		}
	}

	public bool repeat {
		get {
			return false;
		}
	}

	public float heldTime {
		get {
			return 0f;
		}
	}
}

public sealed class InputKeyAction : InputAction {
	string _name;
	InputKey _key;
	int _button;
	ReadOnlyCollection<InputKey> _roKeys;
	ReadOnlyCollection<int> _roButtons;

	static readonly ReadOnlyCollection<InputKey> _emptyKeys = new ReadOnlyCollection<InputKey>(new InputKey[0]);
	static readonly ReadOnlyCollection<int> _emptyButtons = new ReadOnlyCollection<int>(new int[0]);

	public InputKeyAction(string name, InputKey key) {
		_name = name;
		_key = key;
		_roKeys = new ReadOnlyCollection<InputKey>(new[] { _key });
	}

	public InputKeyAction(string name, int button) {
		_name = name;
		_button = button;
		_roButtons = new ReadOnlyCollection<int>(new[] { button });
	}

	public void DebugLog() {
		if (pressed || released || doubleClicked) {
			Debug.Log("--- begin: " + name + " --- ");
			if (pressed) {
				Debug.Log("pressed");
			}
			if (released) {
				Debug.Log("released");
			}
			if (doubleClicked) {
				Debug.Log("doubleClicked");
			}
			Debug.Log("--- end: " + name + " --- ");
		}
	}

	public void Unregister() { }

	InputKeyState inputState {
		get {
			if (_key != InputKey.None) {
				return InputManager.GetKeyState(_key);
			}
			return InputManager.GetButtonState(_button);
		}
	}

	public void Flush() {
		if (_key != InputKey.None) {
			InputManager.FlushKeyState(_key);
		} else {
			InputManager.FlushButtonState(_button);
		}
	}

	public string name {
		get {
			return _name;
		}
	}

	public ReadOnlyCollection<InputKey> keys {
		get {
			return _roKeys ?? _emptyKeys;
		}
	}

	public ReadOnlyCollection<int> buttons {
		get {
			return _roButtons ?? _emptyButtons;
		}
	}

	public bool pressed {
		get {
			return inputState.pressed;
		}
	}

	public bool released {
		get {
			return inputState.released;
		}
	}

	public bool doubleClicked {
		get {
			return inputState.doubleClicked;
		}
	}

	public bool down {
		get {
			return inputState.down;
		}
	}

	public bool repeat {
		get {
			return inputState.repeat;
		}
	}

	public float heldTime {
		get {
			return inputState.heldTime;
		}
	}
}

public sealed class InputChordAction : InputAction {

	string _name;
	InputKeyChord _chord;

	public InputChordAction(string name, IList<InputKey> keys, IList<int> buttons) {
		_name = name;
		_chord = InputManager.CreateKeyChord(keys, buttons);
	}

	public InputChordAction(string name, InputKey key) {
		_name = name;
		_chord = InputManager.CreateKeyChord(key);
	}

	public InputChordAction(string name, IList<InputKey> keys) {
		_name = name;
		_chord = InputManager.CreateKeyChord(keys);
	}

	public InputChordAction(string name, InputKey key, int button) {
		_name = name;
		_chord = InputManager.CreateKeyChord(key, button);
	}

	public InputChordAction(string name, int button) {
		_name = name;
		_chord = InputManager.CreateKeyChord(button);
	}

	public InputChordAction(string name, InputKey key, IList<int> buttons) {
		_name = name;
		_chord = InputManager.CreateKeyChord(key, buttons);
	}

	public InputChordAction(string name, IList<int> buttons) {
		_name = name;
		_chord = InputManager.CreateKeyChord(buttons);
	}

	public InputChordAction(string name, IList<InputKey> keys, int button) {
		_name = name;
		_chord = InputManager.CreateKeyChord(keys, button);
	}

	public void DebugLog() {
		if (pressed || released || doubleClicked) {
			Debug.Log("--- begin: " + name + " --- ");
			if (pressed) {
				Debug.Log("pressed");
			}
			if (released) {
				Debug.Log("released");
			}
			if (doubleClicked) {
				Debug.Log("doubleClicked");
			}
			Debug.Log("--- end: " + name + " --- ");
		}
	}

	public void Unregister() {
		if (_chord != null) {
			_chord.Unregister();
		}
	}

	public void Flush() {
		if (_chord != null) {
			_chord.Flush();
		}
	}

	public string name {
		get {
			return _name;
		}
	}

	public ReadOnlyCollection<InputKey> keys {
		get {
			return _chord.keys;
		}
	}

	public ReadOnlyCollection<int> buttons {
		get {
			return _chord.buttons;
		}
	}

	public bool pressed {
		get {
			return _chord.pressed;
		}
	}

	public bool released {
		get {
			return _chord.released;
		}
	}

	public bool doubleClicked {
		get {
			return _chord.doubleClicked;
		}
	}

	public bool down {
		get {
			return _chord.down;
		}
	}

	public bool repeat {
		get {
			return _chord.repeat;
		}
	}

	public float heldTime {
		get {
			return _chord.heldTime;
		}
	}
}

public class InputManager {
	const float FIRST_REPEAT_DELAY = 0.25f;
	const float REPEAT_DELAY = 0.05f;
	const float DOUBLE_CLICK_DELAY = 0.3f;
	const float DOUBLE_CLICK_REPEAT_DELAY = 0.5f;

	struct Button : InputKeyState {

		double _pressTime;
		double _nextRepeatTime;
		double _nextDoubleClickTime;
		float _heldTime;
		bool _pressed;
		bool _released;
		bool _down;
		bool _doubleClicked;
		bool _repeat;

		public void Pressed(double time, int clickCount) {
			if (_down) {
				// filter key-repeats
				return;
			}

			if (!_doubleClicked && (time >= _nextDoubleClickTime)) {
				if (clickCount > 1) {
					_doubleClicked = true;
				} else if ((clickCount == 0) && (_pressTime != 0)) {
					var dt = time - _pressTime;
					if (dt <= DOUBLE_CLICK_DELAY) {
						_doubleClicked = true;
					}
				}

				if (_doubleClicked) {
					_nextDoubleClickTime = time + DOUBLE_CLICK_REPEAT_DELAY;
				}
			}

			_pressed = true;
			_repeat = false;
			_down = true;
			_pressTime = time;
			_nextRepeatTime = time + FIRST_REPEAT_DELAY;
		}

		public void Released(double time) {
			_released = true;
			_down = false;
		}

		public void Tick(double time) {
			_pressed = false;
			_released = false;
			_doubleClicked = false;
			_repeat = false;
			_heldTime = 0;

			if (_down) {
				_heldTime = (float)(time - _pressTime);
				if (time >= _nextRepeatTime) {
					_repeat = true;
					_nextRepeatTime = time + REPEAT_DELAY;
				}
			}
		}

		public void Flush() {
			_pressTime = 0;
			_heldTime = 0;
			_nextRepeatTime = 0;
			_nextDoubleClickTime = 0;
			_pressed = false;
			_released = false;
			_down = false;
			_doubleClicked = false;
			_repeat = false;
		}

		public bool pressed {
			get {
				return _pressed;
			}
		}

		public bool released {
			get {
				return _released;
			}
		}

		public bool doubleClicked {
			get {
				return _doubleClicked;
			}
		}

		public bool down {
			get {
				return _down;
			}
		}

		public bool repeat {
			get {
				return _repeat;
			}
		}

		public float heldTime {
			get {
				return _heldTime;
			}
		}
	}

	class KeyChord : InputKeyChord {
		IList<InputKey> _keys;
		IList<int> _buttons;

		double _nextRepeatTime;
		double _pressTime;
		float _heldTime;
		bool _pressed;
		bool _released;
		bool _doubleClicked;
		bool _repeat;
		bool _wasDown;
		bool _parentDown;
		KeyChord _parent;
		List<KeyChord> _children = new List<KeyChord>();
		ReadOnlyCollection<InputKey> _roKeys;
		ReadOnlyCollection<int> _roButtons;

		static readonly ReadOnlyCollection<InputKey> _emptyKeys = new ReadOnlyCollection<InputKey>(new InputKey[0]);
		static readonly ReadOnlyCollection<int> _emptyButtons = new ReadOnlyCollection<int>(new int[0]);

		public KeyChord(IList<InputKey> keys, IList<int> buttons) {
			refCount = 1;
			_keys = keys;
			_buttons = buttons;

			if (_keys != null) {
				_roKeys = new ReadOnlyCollection<InputKey>(_keys);
			} else {
				_roKeys = _emptyKeys;
			}

			if (_buttons != null) {
				_roButtons = new ReadOnlyCollection<int>(_buttons);
			} else {
				_roButtons = _emptyButtons;
			}
		}

		public void Update(double time) {
			if (_parent == null) {
				InternalUpdate(time);
				UpdateChildren(time);
			}
		}

		public void Tick(double time) {
			_wasDown = _wasDown && !_released;
			_pressed = false;
			_released = false;
			_doubleClicked = false;
			_repeat = false;
			_heldTime = 0;

			if (_wasDown) {
				_heldTime = (float)(time - _pressTime);

				if (time >= _nextRepeatTime) {
					_repeat = true;
					_nextRepeatTime = time + REPEAT_DELAY;
				}
			}
		}

		void InternalUpdate(double time) {
			bool isDown = down;

			if (!_wasDown && isDown) {
				_pressed = true;
				_doubleClicked = anyDoubleClicked;
				_nextRepeatTime = time + FIRST_REPEAT_DELAY;
				_pressTime = time;
			} else if (_wasDown && (!isDown || anyReleased)) {
				_released = true;
			}

			_repeat = _repeat && isDown;
			_wasDown = isDown;
		}

		void UpdateChildren(double time) {
			for (int i = 0; i < _children.Count; ++i) {
				var c = _children[i];
				c._parentDown = _parentDown || _wasDown;
				c.InternalUpdate(time);
			}
		}

		public bool Removed(KeyChord keyChord) {
			if (ReferenceEquals(keyChord, _parent)) {
				_parent = null;
			} else {
				if (_children.Remove(keyChord)) {
					for (int i = 0; i < keyChord._children.Count; ++i) {
						_children.Add(keyChord._children[i]);
					}

					return true;
				}
			}

			return false;
		}

		public void Added(KeyChord keyChord) {

			if (!IsChild(keyChord)) {
				if (Contains(keyChord)) {
					if (keyChord._parent != null) {
						keyChord._parent._children.Remove(keyChord);
					}

					keyChord._parent = this;
					_children.Add(keyChord);
				} else if (keyChord.Contains(this) && !keyChord.IsChild(this)) {
					if (_parent != null) {
						_parent._children.Remove(this);
					}
					_parent = keyChord;
					keyChord._children.Add(this);
				}
			}
		}

		bool IsChild(KeyChord keyChord) {
			for (var p = keyChord._parent; p != null; p = p._parent) {
				if (ReferenceEquals(p, this)) {
					return true;
				}
			}
			return false;
		}

		public void Flush() {
			_nextRepeatTime = 0;
			_pressTime = 0;
			_heldTime = 0;
			_pressed = false;
			_released = false;
			_doubleClicked = false;
			_repeat = false;
			_wasDown = false;
			_parentDown = false;
		}

		bool Contains(KeyChord keyChord) {
			if (((numButtons <= keyChord.numButtons) &&
				(numKeys <= keyChord.numKeys)) ||
				(numButtons < keyChord.numButtons) ||
				(numKeys < keyChord.numKeys)) {
				return false;
			}

			if (keyChord._buttons != null) {
				for (int i = 0; i < keyChord._buttons.Count; ++i) {
					if (!HasButton(keyChord._buttons[i])) {
						return false;
					}
				}
			}

			if (keyChord._keys != null) {
				for (int i = 0; i < keyChord._keys.Count; ++i) {
					if (!HasKey(keyChord._keys[i])) {
						return false;
					}
				}
			}

			return true;
		}

		bool HasKey(InputKey key) {
			if (_keys != null) {
				return _keys.Contains(key);
			}
			return false;
		}

		bool HasButton(int button) {
			if (_buttons != null) {
				return _buttons.Contains(button);
			}
			return false;
		}

		int numButtons {
			get {
				return (_buttons != null) ? _buttons.Count : 0;
			}
		}

		int numKeys {
			get {
				return (_keys != null) ? _keys.Count : 0;
			}
		}

		public bool pressed {
			get {
				return _pressed;
			}
		}

		public bool released {
			get {
				return _released;
			}
		}

		public bool doubleClicked {
			get {
				return _doubleClicked;
			}
		}

		public bool down {
			get {
				if (_parentDown) {
					return false;
				}

				if ((numKeys == 0) && (numButtons == 0)) {
					return false;
				}

				if (_keys != null) {
					for (int i = 0; i < _keys.Count; ++i) {
						var ks = InputManager.GetKeyState(_keys[i]);

						if (!(ks.down || ks.pressed)) {
							return false;
						}
					}
				}

				if (_buttons != null) {
					for (int i = 0; i < _buttons.Count; ++i) {
						var ks = InputManager.GetButtonState(_buttons[i]);
						if (!(ks.down || ks.pressed)) {
							return false;
						}
					}
				}

				return true;
			}
		}

		bool anyDoubleClicked {
			get {
				if (numButtons > 0) {
					// if we have mouse buttons, they generate the double clicks.
					for (int i = 0; i < _buttons.Count; ++i) {
						if (InputManager.GetButtonState(_buttons[i]).doubleClicked) {
							return true;
						}
					}
				} else if (_keys != null) {
					// otherwise keys do
					for (int i = 0; i < _keys.Count; ++i) {
						if (InputManager.GetKeyState(_keys[i]).doubleClicked) {
							return true;
						}
					}
				}

				return false;
			}
		}

		bool anyReleased {
			get {
				if (numButtons > 0) {
					// if we have mouse buttons, they generate the double clicks.
					for (int i = 0; i < _buttons.Count; ++i) {
						if (InputManager.GetButtonState(_buttons[i]).released) {
							return true;
						}
					}
				} else if (_keys != null) {
					// otherwise keys do
					for (int i = 0; i < _keys.Count; ++i) {
						if (InputManager.GetKeyState(_keys[i]).released) {
							return true;
						}
					}
				}

				return false;
			}
		}

		public bool repeat {
			get {
				return _repeat;
			}
		}

		public float heldTime {
			get {
				return _heldTime;
			}
		}

		public ReadOnlyCollection<InputKey> keys {
			get {
				return _roKeys;
			}
		}

		public ReadOnlyCollection<int> buttons {
			get {
				return _roButtons;
			}
		}

		public int refCount {
			get;
			set;
		}

		public void Unregister() {
			InputManager.UnregisterKeyChord(this);
		}
	}

	const int MAX_KEYS = 510;

	Button[] _keys;
	Button[] _buttons;

	List<Event> _eventQueue = new List<Event>();
	List<KeyChord> _keyChords = new List<KeyChord>();
	List<InputEventFilter> _eventFilters = new List<InputEventFilter>();

	static InputManager _instance;

	static InputManager instance {
		get {
			if (_instance == null) {
				_instance = new InputManager();
			}
			return _instance;
		}
	}

	public static void ProcessEvent(Event e) {
		instance.EnqueueEvent(e);
	}

	public static void Tick(double time) {
		instance.RunEventQueue(time);
	}

	public static InputKeyChord CreateKeyChord(IList<InputKey> keys, IList<int> buttons) {
		return instance.InternalRegisterKeyChord(keys, buttons);
	}

	public static InputKeyChord CreateKeyChord(IList<InputKey> keys) {
		return instance.InternalRegisterKeyChord(keys, null);
	}

	public static InputKeyChord CreateKeyChord(InputKey key) {
		return instance.InternalRegisterKeyChord(new[] { key }, null);
	}

	public static InputKeyChord CreateKeyChord(InputKey key, int button) {
		return instance.InternalRegisterKeyChord(new[] { key }, new[] { button });
	}

	public static InputKeyChord CreateKeyChord(int button) {
		return instance.InternalRegisterKeyChord(null, new[] { button });
	}

	public static InputKeyChord CreateKeyChord(IList<int> buttons) {
		return instance.InternalRegisterKeyChord(null, buttons);
	}

	public static InputKeyChord CreateKeyChord(InputKey key, IList<int> buttons) {
		return instance.InternalRegisterKeyChord(new[] { key }, buttons);
	}

	public static InputKeyChord CreateKeyChord(IList<InputKey> keys, int button) {
		return instance.InternalRegisterKeyChord(keys, new[] { button });
	}

	public static void UnregisterKeyChord(InputKeyChord chord) {
		instance.InternalUnregisterKeyChord((KeyChord)chord);
	}

	public static void ClearUIFocus() {
		EventSystem.current.SetSelectedGameObject(null);
    }

	public static void AddEventFilter(InputEventFilter filter) {
		if (!instance._eventFilters.Contains(filter)) {
			instance._eventFilters.Add(filter);
		}
	}

	public static void RemoveEventFilter(InputEventFilter filter) {
		instance._eventFilters.Remove(filter);
	}

	public static void ClearEventFilters() {
		instance._eventFilters.Clear();
	}

	public static int numButtons {
		get {
			return instance._buttons.Length;
		}
	}

	public static InputKeyState GetButtonState(int button) {
		return instance._buttons[button];
	}

	public static InputKeyState GetKeyState(InputKey key) {
		return instance._keys[(int)key];
	}

	public static void FlushButtonState(int button) {
		instance._buttons[button].Flush();
	}

	public static void FlushKeyState(InputKey key) {
		instance._keys[(int)key].Flush();
	}

	public static bool guiFocused {
		get {
			return guiModalInput || guiMouseFocused;
		}
	}

	public static bool guiModalInput {
		get;
		set;
	}

	public static bool guiMouseFocused {
		get;
		set;
	} 

	InputManager() {
		_keys = new Button[MAX_KEYS];
		_buttons = new Button[10];
	}

	void EnqueueEvent(Event e) {

		if (e.isKey && ((InputKey)e.keyCode).IsModifierKey()) {
			// modifiers are done manually.
			return;
		}

		if (_eventFilters.Count > 0) {
			for (int i = 0; i < _eventFilters.Count; ++i) {
				if (_eventFilters[i](e)) {
					return; // eaten by filter.
				}
			}
		}

		if ((e.type == EventType.MouseDown) ||
			(e.type == EventType.MouseUp)) {

			if (guiMouseFocused) {
				if (e.type == EventType.MouseDown) {
					return;
				} else if (!GetButtonState(e.button).down) {
					// don't send a mouse up unless we have grabbed it already.
					return;
				}
			}

			_eventQueue.Add(new Event(e));

		} else if ((e.type == EventType.KeyDown) || (e.type == EventType.KeyUp)) {

			if (guiModalInput && !Console.isOpen) {
				if (e.keyCode != KeyCode.Escape) {
					if (e.type == EventType.KeyDown) {
						return;
					} else if (!GetKeyState((InputKey)e.keyCode).down) {
						// don't send a key unless we have grabbed it already.
						return;
					}
				}
			}

			_eventQueue.Add(new Event(e));

		} else if (e.type == EventType.ScrollWheel) {
			_eventQueue.Add(new Event(e));
		}
	}

	void InternalProcessEvent(Event e, double time) {
		
		switch (e.type) {
			case EventType.MouseDown:
				SafeButtonDown(e, time);
			break;
			case EventType.MouseUp:
				SafeButtonUp(e, time);
			break;
			case EventType.KeyDown:
				_keys[(int)e.keyCode].Pressed(time, 0);
			break;
			case EventType.KeyUp:
				_keys[(int)e.keyCode].Released(time);
			break;
			case EventType.ScrollWheel:
				if (e.delta.y < 0) {
					_keys[(int)InputKey.ScrollWheelUp].Pressed(time, 1);
				} else {
					_keys[(int)InputKey.ScrollWheelDown].Pressed(time, 1);
				}
			break;
			default:
			return;
		}
		
		for (int i = 0; i < _keyChords.Count; ++i) {
			var chord = _keyChords[i];
			chord.Update(time);
		}

		if (e.type == EventType.ScrollWheel) {
			switch (e.type) {
				case EventType.ScrollWheel:
					if (e.delta.y < 0) {
						_keys[(int)InputKey.ScrollWheelUp].Released(time);
					} else {
						_keys[(int)InputKey.ScrollWheelDown].Released(time);
					}
				break;
			}

			for (int i = 0; i < _keyChords.Count; ++i) {
				var chord = _keyChords[i];
				chord.Update(time);
			}
		}
	}

	KeyChord FindKeyChord(IList<InputKey> keys, IList<int> buttons) {
		for (int i = 0; i < _keyChords.Count; ++i) {
			var chord = _keyChords[i];

			if ((((keys != null) && (chord.keys.Count == keys.Count)) || ((keys == null) && (chord.keys.Count == 0))) &&
				(((buttons != null) && (chord.buttons.Count == buttons.Count)) || ((buttons == null) && (chord.buttons.Count == 0)))) {

				bool match = true;

				if (keys != null) {
					for (int k = 0; k < keys.Count; ++k) {
						if (!chord.keys.Contains(keys[k])) {
							match = false;
							break;
						}
					}
				}

				if (buttons != null) {
					for (int k = 0; k < buttons.Count; ++k) {
						if (!chord.buttons.Contains(buttons[k])) {
							match = false;
							break;
						}
					}
				}

				if (match) {
					return chord;
				}
            }
		}

		return null;
	}

	KeyChord InternalRegisterKeyChord(IList<InputKey> keys, IList<int> buttons) {
		if (((keys == null) || (keys.Count < 1)) &&
			((buttons == null) || (buttons.Count < 1))) {
			return null;
		}

		var chord = FindKeyChord(keys, buttons);
		if (chord != null) {
			++chord.refCount;
			return chord;
		}

		chord = new KeyChord(keys, buttons);

		for (int i = 0; i < _keyChords.Count; ++i) {
			_keyChords[i].Added(chord);
		}

		_keyChords.Add(chord);

		return chord;
	}

	void InternalUnregisterKeyChord(KeyChord keyChord) {
		if (--keyChord.refCount == 0) {
			if (_keyChords.Remove(keyChord)) {
				for (int i = 0; i < _keyChords.Count; ++i) {
					_keyChords[i].Removed(keyChord);
				}
			}
		}
	}

	void SafeButtonDown(Event e, double time) {
		if ((e.button >= 0) && (e.button < _buttons.Length)) {
			_buttons[e.button].Pressed(time, e.clickCount);
		}
	}

	void SafeButtonUp(Event e, double time) {
		if ((e.button >= 0) && (e.button < _buttons.Length)) {
			_buttons[e.button].Released(time);
		}
	}

	void GenerateShiftKeys(double time) {
		if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) {
			if (!_keys[(int)KeyCode.LeftShift].down || _keys[(int)KeyCode.LeftShift].released) {
				_keys[(int)KeyCode.LeftShift].Pressed(time, 1);

				for (int i = 0; i < _keyChords.Count; ++i) {
					var chord = _keyChords[i];
					chord.Update(time);
				}
			}
		}
		if (_keys[(int)KeyCode.LeftShift].down && (!(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))) {
			_keys[(int)KeyCode.LeftShift].Released(time);

			for (int i = 0; i < _keyChords.Count; ++i) {
				var chord = _keyChords[i];
				chord.Update(time);
			}
		}
	}

	void GenerateControlKeys(double time) {
		if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl)) {
			if (!_keys[(int)KeyCode.LeftControl].down || _keys[(int)KeyCode.LeftControl].released) {
				_keys[(int)KeyCode.LeftControl].Pressed(time, 1);

				for (int i = 0; i < _keyChords.Count; ++i) {
					var chord = _keyChords[i];
					chord.Update(time);
				}
			}
		}
		if (_keys[(int)KeyCode.LeftControl].down && (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))) {
			_keys[(int)KeyCode.LeftControl].Released(time);

			for (int i = 0; i < _keyChords.Count; ++i) {
				var chord = _keyChords[i];
				chord.Update(time);
			}
		}
	}

	void GenerateAltKeys(double time) {
		if (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)) {
			if (!_keys[(int)KeyCode.LeftAlt].down || _keys[(int)KeyCode.LeftAlt].released) {
				_keys[(int)KeyCode.LeftAlt].Pressed(time, 1);

				for (int i = 0; i < _keyChords.Count; ++i) {
					var chord = _keyChords[i];
					chord.Update(time);
				}
			}
		}
		if (_keys[(int)KeyCode.LeftAlt].down && (!(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))) {
			_keys[(int)KeyCode.LeftAlt].Released(time);

			for (int i = 0; i < _keyChords.Count; ++i) {
				var chord = _keyChords[i];
				chord.Update(time);
			}
		}
	}

	void GenerateCommandKeys(double time) {
		if (Input.GetKeyDown(KeyCode.LeftCommand) || Input.GetKeyDown(KeyCode.RightCommand)) {
			if (!_keys[(int)KeyCode.LeftCommand].down || _keys[(int)KeyCode.RightCommand].released) {
				_keys[(int)KeyCode.LeftCommand].Pressed(time, 1);
			}
		}
		if (_keys[(int)KeyCode.LeftCommand].down && (!(Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)))) {
			_keys[(int)KeyCode.LeftCommand].Released(time);

			for (int i = 0; i < _keyChords.Count; ++i) {
				var chord = _keyChords[i];
				chord.Update(time);
			}
		}
	}

	void RunEventQueue(double time) {
		for (int i = 0; i < _keys.Length; ++i) {
			_keys[i].Tick(time);
		}
		for (int i = 0; i < _buttons.Length; ++i) {
			_buttons[i].Tick(time);
		}
		for (int i = 0; i < _keyChords.Count; ++i) {
			_keyChords[i].Tick(time);
		}

		GenerateShiftKeys(time);
		GenerateControlKeys(time);
		GenerateAltKeys(time);
		GenerateCommandKeys(time);

        for (int i = 0; i < _eventQueue.Count; ++i) {
			InternalProcessEvent(_eventQueue[i], time);
		}

		_eventQueue.Clear();
	}

	void Flush() {
		for (int i = 0; i < _keys.Length; ++i) {
			_keys[i].Flush();
		}
		for (int i = 0; i < _buttons.Length; ++i) {
			_buttons[i].Flush();
		}
		for (int i = 0; i < _keyChords.Count; ++i) {
			_keyChords[i].Flush();
		}
		_eventQueue.Clear();
	}
	
}
