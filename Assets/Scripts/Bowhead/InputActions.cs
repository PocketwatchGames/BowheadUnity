// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {

	public static class InputActionExtensions {
		public static string GetLongBindingText(this InputAction action) {
			return GetLongBindingText(action, null);
		}

		public static string GetMixedBindingText(this InputAction action) {
			return GetMixedBindingText(action, null);
		}

		public static string GetMixedBindingText(this InputAction action, string localized) {

			if (localized == null) {
				var longText = UserPrefs.instance.GetString(action.name + ".localized.long", null);
				if (longText != null) {
					return longText;
				}
			}

			string text = string.Empty;
			if (action.keys.Contains(InputKey.LeftControl) || action.keys.Contains(InputKey.RightControl)) {
				text = Append(text, InputKey.LeftControl.GetLongName());
			}
			if (action.keys.Contains(InputKey.LeftShift) || action.keys.Contains(InputKey.RightShift)) {
				text = Append(text, InputKey.LeftShift.GetLongName());
			}
			if (action.keys.Contains(InputKey.LeftAlt) || action.keys.Contains(InputKey.RightAlt)) {
				text = Append(text, InputKey.LeftAlt.GetLongName());
			}

			foreach (var button in action.buttons) {
				text = Append(text, GetShortButtonName(button));
			}

			if (localized != null) {
				text = Append(text, localized);
			} else {
				foreach (var key in action.keys) {
					if (!key.IsModifierKey()) {
						text = Append(text, key.GetLongName());
					}
				}
			}

			return text;
		}

		public static string GetLongBindingText(this InputAction action, string localized) {

			if (localized == null) {
				var longText = UserPrefs.instance.GetString(action.name + ".localized.long", null);
				if (longText != null) {
					return longText;
				}
			}

			string text = string.Empty;
			if (action.keys.Contains(InputKey.LeftControl) || action.keys.Contains(InputKey.RightControl)) {
				text = Append(text, InputKey.LeftControl.GetLongName());
			}
			if (action.keys.Contains(InputKey.LeftShift) || action.keys.Contains(InputKey.RightShift)) {
				text = Append(text, InputKey.LeftShift.GetLongName());
			}
			if (action.keys.Contains(InputKey.LeftAlt) || action.keys.Contains(InputKey.RightAlt)) {
				text = Append(text, InputKey.LeftAlt.GetLongName());
			}

			foreach (var button in action.buttons) {
				text = Append(text, GetLongButtonName(button));
			}

			if (localized != null) {
				text = Append(text, localized);
			} else {
				foreach (var key in action.keys) {
					if (!key.IsModifierKey()) {
						text = Append(text, key.GetLongName());
					}
				}
			}
			
			return text;
		}

		public static string GetShortBindingText(this InputAction action) {
			return GetShortBindingText(action, null);
		}

        public static string GetShortBindingText(this InputAction action, string localized) {

			if (localized != null) {
				var shortText = UserPrefs.instance.GetString(action.name + ".localized.short", null);
				if (shortText != null) {
					return shortText;
				}
			}

			string text = string.Empty;
			if (action.keys.Contains(InputKey.LeftControl) || action.keys.Contains(InputKey.RightControl)) {
				text += InputKey.LeftControl.GetShortName();
			}
			if (action.keys.Contains(InputKey.LeftShift) || action.keys.Contains(InputKey.RightShift)) {
				text += InputKey.LeftShift.GetShortName();
			}
			if (action.keys.Contains(InputKey.LeftAlt) || action.keys.Contains(InputKey.RightAlt)) {
				text += InputKey.LeftAlt.GetShortName();
			}

			foreach (var button in action.buttons) {
				text = Append(text, GetShortButtonName(button));
			}

			if (localized != null) {
				text = Append(text, localized);
			} else {
				foreach (var key in action.keys) {
					if (!key.IsModifierKey()) {
						text += key.GetShortName();
					}
				}
			}

			return text;
		}

		static string Append(string a, string b) {
			if (string.IsNullOrEmpty(a)) {
				return b;
			}
			return a + "+" + b;
		}

		static string GetShortButtonName(int button) {
			switch (button) {
				case 0:
					return "LMB";
				case 1:
					return "RMB";
				case 2:
					return "MMB";
			}

			return "M" + button;
		}

		static string GetLongButtonName(int button) {
			switch (button) {
				case 0:
				return "Left Mouse Button";
				case 1:
				return "Right Mouse Button";
				case 2:
				return "Middle Mouse Button";
			}

			return "MOUSE" + button;
		}
	}

	public sealed class MenuActions : InputActionContainer {

		public readonly InputAction toggleConsole;
		public readonly InputAction consoleHistoryUp;
		public readonly InputAction consoleHistoryDown;
		public readonly InputAction consoleHistoryEnd;
		public readonly InputAction consoleTabComplete;
		public readonly InputAction submit;
		public readonly InputAction cancel;
		public readonly InputAction up;
		public readonly InputAction down;
		public readonly InputAction left;
		public readonly InputAction right;

		static MenuActions _instance;

		MenuActions() {
			toggleConsole = CreateKeyAction("Input.Action.UI.ToggleConsole", InputKey.BackQuote);
			consoleHistoryUp = CreateKeyAction("*ConsoleHistoryUp", InputKey.PageUp);
			consoleHistoryDown = CreateKeyAction("*ConsoleHistoryDown", InputKey.PageDown);
			consoleHistoryEnd = CreateKeyAction("*ConsoleHistoryEnd", InputKey.End);
			consoleTabComplete = CreateKeyAction("*ConsoleTabComplete", InputKey.Tab);
			submit = CreateKeyAction("Input.Action.UI.Submit", InputKey.Return);
			cancel = CreateKeyAction("Input.Action.UI.Cancel", InputKey.Escape);
			up = CreateKeyAction("Input.Action.UI.MoveUp", InputKey.UpArrow);
			down = CreateKeyAction("Input.Action.UI.MoveDown", InputKey.DownArrow);
			left = CreateKeyAction("Input.Action.UI.MoveLeft", InputKey.LeftArrow);
			right = CreateKeyAction("Input.Action.UI.MoveRight", InputKey.RightArrow);
		}

		public static MenuActions instance {
			get {
				if (_instance == null) {
					_instance = new MenuActions();
				}
				return _instance;
			}
		}
	}

	public sealed class GameplayInputActions : InputActionContainer {
		const int BINARY_VERSION = 1;
		public const int NUM_PRESETS = 6;
		public const float DEFAULT_MOUSE_PAN_SPEED = 60;
		public const float DEFAULT_MOUSE_ORBIT_SPEED = 450;
		public const float DEFAULT_SMOOTH_ORBIT_X_SPEED = 40;
		public const float DEFAULT_SMOOTH_ORBIT_Y_SPEED = 120;
		public const float DEFAULT_EDGE_PAN_SPEED = 45;
		public const float DEFAULT_EDGE_TILT_SPEED = 120;
		public const float DEFAULT_EDGE_ORBIT_SPEED = 120;
		public const float DEFAULT_SMOOTH_PAN_SPEED = 45;
		public const float DEFAULT_SNAP_ORBIT_X_INCREMENT = 5;
		public const float DEFAULT_SNAP_ORBIT_Y_INCREMENT = 45;
		public const float DEAFULT_SNAP_ZOOM_INCREMENT = 6f;
		public const float DEFAULT_SMOOTH_ZOOM_SPEED = 60;

		public bool edgePanning;
		public bool invertMousePanX;
		public bool invertMousePanY;
		public bool invertMouseOrbitX;
		public bool invertMouseOrbitY;
		public bool translateByDefault;
		public bool defaultFlexiFormation;
		public float mousePanSpeed;
		public float mouseOrbitSpeed;
		public float smoothOrbitXSpeed;
		public float smoothOrbitYSpeed;
		public float edgePanSpeed;
		public float edgeOrbitSpeed;
		public float edgeTiltSpeed;
		public float smoothPanSpeed;
		public float snapOrbitXIncrement;
		public float snapOrbitYIncrement;
		public float snapZoomIncrement;
		public float smoothZoomSpeed;

		// Select all units OR cancel a current action (like placing an AOE).
		public InputAction selectAll;

		// Tell selected units to defend.
		public InputAction defend;

		// Tell selected units to hold position, but they can attack if they can reach.
		public InputAction hold;

		// Tell units to stop doing anything, and go idle. They won't attack.
		public InputAction stop;

		// Focus the camera on the selection. Double-tap to follow.
		public InputAction focus;

		// Scatter selected units.
		public InputAction scatter;

		// Unit special action
		public InputAction unitSpecial;

		// Move camera left
		public InputAction moveLeft;

		// Move camera right
		public InputAction moveRight;

		// Move camera forward.
		public InputAction moveForward;

		// Move camera backwards
		public InputAction moveBackward;

		// Orbit camera left (smooth)
		public InputAction smoothOrbitLeft;

		// Orbit camera right (smooth)
		public InputAction smoothOrbitRight;

		// Orbit camera up (smooth)
		public InputAction smoothOrbitUp;

		// Orbit camera down (smooth)
		public InputAction smoothOrbitDown;

		// Zoom in (smooth)
		public InputAction smoothZoomIn;

		// Zoom out (smooth)
		public InputAction smoothZoomOut;

		// Orbit camera left (snap)
		public InputAction snapOrbitLeft;

		// Orbit camera right (snap)
		public InputAction snapOrbitRight;

		// Orbit camera up (snap)
		public InputAction snapOrbitUp;

		// Orbit camera down (snap)
		public InputAction snapOrbitDown;

		// Zoom in (snap)
		public InputAction snapZoomIn;

		// Zoom out (snap)
		public InputAction snapZoomOut;

		// Reset the orbit angle.
		public InputAction resetPitch;

		// Orbit with mouse
		public InputAction mouseOrbit;

		// Pan with mouse
		public InputAction mousePan;
		
		// Primary action like attack or move
		public InputAction action;

		// Select
		public InputAction select;

		// Modifier key for primary action
		public InputAction actionModifier;

		// Modifier key for select action
		public InputAction selectModifier;

		// Modifier key for move action
		public InputAction moveModifier;

		// Say (in-game chat message)
		public InputAction say;

		// TeamSay (in-game chat message)
		public InputAction teamSay;

		// View chat history.
		public InputAction peekChat;

		// Spells
		public InputAction[] spells;

		public static GameplayInputActions ResetInputKeys() {
			var x = new GameplayInputActions();

			x.edgePanning = UserPrefs.instance.GetInt("Input.EdgePanning", 0) != 0;
			x.edgePanSpeed = UserPrefs.instance.GetFloat("Input.EdgePanningSpeed", DEFAULT_EDGE_PAN_SPEED);
			x.edgeTiltSpeed = UserPrefs.instance.GetFloat("Input.EdgeTiltSpeed", DEFAULT_EDGE_TILT_SPEED);
			x.edgeOrbitSpeed = UserPrefs.instance.GetFloat("Input.EdgeOrbitSpeed", DEFAULT_EDGE_ORBIT_SPEED);
			x.invertMousePanX = UserPrefs.instance.GetInt("Input.InvertMousePanX", 0) != 0;
			x.invertMousePanY = UserPrefs.instance.GetInt("Input.InvertMousePanY", 0) != 0;
			x.invertMouseOrbitX = UserPrefs.instance.GetInt("Input.InvertMouseOrbitX", 0) != 0;
			x.invertMouseOrbitY = UserPrefs.instance.GetInt("Input.InvertMouseOrbitY", 0) != 0;
			x.translateByDefault = UserPrefs.instance.GetInt("Input.TranslateByDefault", 0) != 0;
			x.defaultFlexiFormation = UserPrefs.instance.GetInt("Input.DefaultFlexiFormation", 0) != 0;
			x.mousePanSpeed = UserPrefs.instance.GetFloat("Input.MousePanSpeed", DEFAULT_MOUSE_PAN_SPEED);
			x.mouseOrbitSpeed = UserPrefs.instance.GetFloat("Input.MouseOrbitSpeed", DEFAULT_MOUSE_ORBIT_SPEED);
			x.smoothOrbitXSpeed = UserPrefs.instance.GetFloat("Input.SmoothOrbitSpeedX", DEFAULT_SMOOTH_ORBIT_X_SPEED);
			x.smoothOrbitYSpeed = UserPrefs.instance.GetFloat("Input.SmoothOrbitSpeedY", DEFAULT_SMOOTH_ORBIT_Y_SPEED);
			x.smoothPanSpeed = UserPrefs.instance.GetFloat("Input.SmoothPanSpeed", DEFAULT_SMOOTH_PAN_SPEED);
			x.snapOrbitXIncrement = UserPrefs.instance.GetFloat("Input.SnapOrbitXIncrement", DEFAULT_SNAP_ORBIT_X_INCREMENT);
			x.snapOrbitYIncrement = UserPrefs.instance.GetFloat("Input.SnapOrbitYIncrement", DEFAULT_SNAP_ORBIT_Y_INCREMENT);
			x.snapZoomIncrement = UserPrefs.instance.GetFloat("Input.SnapZoomIncrement", DEAFULT_SNAP_ZOOM_INCREMENT);
			x.smoothZoomSpeed = UserPrefs.instance.GetFloat("Input.SmoothZoomSpeed", DEFAULT_SMOOTH_ZOOM_SPEED);

			ClearActionText(x.selectAll);
			ClearActionText(x.defend);
			ClearActionText(x.hold);
			ClearActionText(x.stop);
			ClearActionText(x.focus);
			ClearActionText(x.scatter);
			ClearActionText(x.unitSpecial);
			ClearActionText(x.moveLeft);
			ClearActionText(x.moveRight);
			ClearActionText(x.moveForward);
			ClearActionText(x.moveBackward);
			ClearActionText(x.smoothOrbitLeft);
			ClearActionText(x.smoothOrbitRight);
			ClearActionText(x.smoothOrbitUp);
			ClearActionText(x.smoothOrbitDown);
			ClearActionText(x.smoothZoomIn);
			ClearActionText(x.smoothZoomOut);
			ClearActionText(x.snapOrbitLeft);
			ClearActionText(x.snapOrbitRight);
			ClearActionText(x.snapOrbitUp);
			ClearActionText(x.snapOrbitDown);
			ClearActionText(x.snapZoomIn);
			ClearActionText(x.snapZoomOut);
			ClearActionText(x.resetPitch);
			ClearActionText(x.mouseOrbit);
			ClearActionText(x.mousePan);
			ClearActionText(x.say);
			ClearActionText(x.teamSay);
			ClearActionText(x.peekChat);
			
			for (int i = 0; i < x.spells.Length; ++i) {
				ClearActionText(x.spells[i]);
			}

			x.SaveInputSettings();

			return x;
		}

		public static GameplayInputActions LoadInputSettings() {
			var x = new GameplayInputActions();

			x.edgePanning = UserPrefs.instance.GetInt("Input.EdgePanning", 0) != 0;
			x.edgePanSpeed = UserPrefs.instance.GetFloat("Input.EdgePanningSpeed", DEFAULT_EDGE_PAN_SPEED);
			x.edgeTiltSpeed = UserPrefs.instance.GetFloat("Input.EdgeTiltSpeed", DEFAULT_EDGE_TILT_SPEED);
			x.edgeOrbitSpeed = UserPrefs.instance.GetFloat("Input.EdgeOrbitSpeed", DEFAULT_EDGE_ORBIT_SPEED);
			x.invertMousePanX = UserPrefs.instance.GetInt("Input.InvertMousePanX", 0) != 0;
			x.invertMousePanY = UserPrefs.instance.GetInt("Input.InvertMousePanY", 0) != 0;
			x.invertMouseOrbitX = UserPrefs.instance.GetInt("Input.InvertMouseOrbitX", 0) != 0;
			x.invertMouseOrbitY = UserPrefs.instance.GetInt("Input.InvertMouseOrbitY", 0) != 0;
			x.translateByDefault = UserPrefs.instance.GetInt("Input.TranslateByDefault", 0) != 0;
			x.defaultFlexiFormation = UserPrefs.instance.GetInt("Input.DefaultFlexiFormation", 0) != 0;
			x.mousePanSpeed = UserPrefs.instance.GetFloat("Input.MousePanSpeed", DEFAULT_MOUSE_PAN_SPEED);
			x.mouseOrbitSpeed = UserPrefs.instance.GetFloat("Input.MouseOrbitSpeed", DEFAULT_MOUSE_ORBIT_SPEED);
			x.smoothOrbitXSpeed = UserPrefs.instance.GetFloat("Input.SmoothOrbitSpeedX", DEFAULT_SMOOTH_ORBIT_X_SPEED);
			x.smoothOrbitYSpeed = UserPrefs.instance.GetFloat("Input.SmoothOrbitSpeedY", DEFAULT_SMOOTH_ORBIT_Y_SPEED);
			x.smoothPanSpeed = UserPrefs.instance.GetFloat("Input.SmoothPanSpeed", DEFAULT_SMOOTH_PAN_SPEED);
			x.snapOrbitXIncrement = UserPrefs.instance.GetFloat("Input.SnapOrbitXIncrement", DEFAULT_SNAP_ORBIT_X_INCREMENT);
			x.snapOrbitYIncrement = UserPrefs.instance.GetFloat("Input.SnapOrbitYIncrement", DEFAULT_SNAP_ORBIT_Y_INCREMENT);
			x.snapZoomIncrement = UserPrefs.instance.GetFloat("Input.SnapZoomIncrement", DEAFULT_SNAP_ZOOM_INCREMENT);
			x.smoothZoomSpeed = UserPrefs.instance.GetFloat("Input.SmoothZoomSpeed", DEFAULT_SMOOTH_ZOOM_SPEED);

			x.LoadInputAction(ref x.selectAll);
			x.LoadInputAction(ref x.defend);
			x.LoadInputAction(ref x.hold);
			x.LoadInputAction(ref x.stop);
			x.LoadInputAction(ref x.focus);
			x.LoadInputAction(ref x.scatter);
			x.LoadInputAction(ref x.unitSpecial);
			x.LoadInputAction(ref x.moveLeft);
			x.LoadInputAction(ref x.moveRight);
			x.LoadInputAction(ref x.moveForward);
			x.LoadInputAction(ref x.moveBackward);
			x.LoadInputAction(ref x.smoothOrbitLeft);
			x.LoadInputAction(ref x.smoothOrbitRight);
			x.LoadInputAction(ref x.smoothOrbitUp);
			x.LoadInputAction(ref x.smoothOrbitDown);
			x.LoadInputAction(ref x.smoothZoomIn);
			x.LoadInputAction(ref x.smoothZoomOut);
			x.LoadInputAction(ref x.snapOrbitLeft);
			x.LoadInputAction(ref x.snapOrbitRight);
			x.LoadInputAction(ref x.snapOrbitUp);
			x.LoadInputAction(ref x.snapOrbitDown);
			x.LoadInputAction(ref x.snapZoomIn);
			x.LoadInputAction(ref x.snapZoomOut);
			x.LoadInputAction(ref x.resetPitch);
			x.LoadInputAction(ref x.mouseOrbit);
			x.LoadInputAction(ref x.mousePan);
			x.LoadInputAction(ref x.say);
			x.LoadInputAction(ref x.teamSay);
			x.LoadInputAction(ref x.peekChat);
			
			for (int i = 0; i < x.spells.Length; ++i) {
				x.LoadInputAction(ref x.spells[i]);
			}

			return x;
		}

		public void SaveInputSettings() {
			UserPrefs.instance.SetInt("Input.EdgePanning", edgePanning ? 1 : 0);
			UserPrefs.instance.SetFloat("Input.EdgePanningSpeed", edgePanSpeed);
			UserPrefs.instance.SetFloat("Input.EdgeTiltSpeed", edgeTiltSpeed);
			UserPrefs.instance.SetFloat("Input.EdgeOrbitSpeed", edgeOrbitSpeed);
			UserPrefs.instance.SetInt("Input.InvertMousePanX", invertMousePanX ? 1 : 0);
			UserPrefs.instance.SetInt("Input.InvertMousePanY", invertMousePanY ? 1 : 0);
			UserPrefs.instance.SetInt("Input.InvertMouseOrbitX", invertMouseOrbitX ? 1 : 0);
			UserPrefs.instance.SetInt("Input.InvertMouseOrbitY", invertMouseOrbitY ? 1 : 0);
			UserPrefs.instance.SetInt("Input.TranslateByDefault", translateByDefault ? 1 : 0);
			UserPrefs.instance.SetInt("Input.DefaultFlexiFormation", defaultFlexiFormation ? 1 : 0);
            UserPrefs.instance.SetFloat("Input.MousePanSpeed", mousePanSpeed);
			UserPrefs.instance.SetFloat("Input.MouseOrbitSpeed", mouseOrbitSpeed);
			UserPrefs.instance.SetFloat("Input.SmoothOrbitSpeedX", smoothOrbitXSpeed);
			UserPrefs.instance.SetFloat("Input.SmoothOrbitSpeedY", smoothOrbitYSpeed);
			UserPrefs.instance.SetFloat("Input.SmoothPanSpeed", smoothPanSpeed);
			UserPrefs.instance.SetFloat("Input.SnapOrbitXIncrement", snapOrbitXIncrement);
			UserPrefs.instance.SetFloat("Input.SnapOrbitYIncrement", snapOrbitYIncrement);
			UserPrefs.instance.SetFloat("Input.SnapZoomIncrement", snapZoomIncrement);
			UserPrefs.instance.SetFloat("Input.SmoothZoomSpeed", smoothZoomSpeed);

			SaveInputAction(selectAll);
			SaveInputAction(defend);
			SaveInputAction(hold);
			SaveInputAction(stop);
			SaveInputAction(focus);
			SaveInputAction(scatter);
			SaveInputAction(unitSpecial);
			SaveInputAction(moveLeft);
			SaveInputAction(moveRight);
			SaveInputAction(moveForward);
			SaveInputAction(moveBackward);
			SaveInputAction(smoothOrbitLeft);
			SaveInputAction(smoothOrbitRight);
			SaveInputAction(smoothOrbitUp);
			SaveInputAction(smoothOrbitDown);
			SaveInputAction(smoothZoomIn);
			SaveInputAction(smoothZoomOut);
			SaveInputAction(snapOrbitLeft);
			SaveInputAction(snapOrbitRight);
			SaveInputAction(snapOrbitUp);
			SaveInputAction(snapOrbitDown);
			SaveInputAction(snapZoomIn);
			SaveInputAction(snapZoomOut);
			SaveInputAction(resetPitch);
			SaveInputAction(mouseOrbit);
			SaveInputAction(mousePan);
			SaveInputAction(say);
			SaveInputAction(teamSay);
			SaveInputAction(peekChat);

			for (int i = 0; i < spells.Length; ++i) {
				SaveInputAction(spells[i]);
			}

			UserPrefs.instance.Save();
		}

		public static void ClearActionText(InputAction action) {
			UserPrefs.instance.DeleteKey(action.name + ".localized.long");
			UserPrefs.instance.DeleteKey(action.name + ".localized.short");
		}

		void LoadInputAction(ref InputAction action) {
			var keys = LoadKeys(action.name + ".keys");
			var buttons = LoadButtons(action.name + ".buttons");

			if ((keys != null) && (keys.Count == 0) &&
				(buttons != null) && (buttons.Count == 0)) {
				Remove(action);
				action = new UnboundInputAction(action.name);
			} else if ((keys != null) && (keys.Count > 0)) {
				Remove(action);
				if ((buttons != null) && (buttons.Count > 0)) {
					action = CreateChordAction(action.name, keys, buttons[0]);
				} else {
					action = CreateChordAction(action.name, keys);
				}
			} else if ((buttons != null) && (buttons.Count > 0)) {
				Remove(action);
				action = CreateChordAction(action.name, buttons);
			}
		}

		public static void SaveInputAction(InputAction action) {
			SaveKeys(action.name + ".keys", action.keys);
			SaveButtons(action.name + ".buttons", action.buttons);
		}

		static void SaveKeys(string path, IList<InputKey> keys) {
			string str = string.Empty;
			for (int i = 0; i < keys.Count; ++i) {
				var s = keys[i].ToString();
				if (string.IsNullOrEmpty(str)) {
					str = s;
				} else {
					str += ";" + s;
				}
			}

			UserPrefs.instance.SetString(path, str);
		}

		static void SaveButtons(string path, IList<int> buttons) {
			string str = string.Empty;
			for (int i = 0; i < buttons.Count; ++i) {
				var s = buttons[i].ToString();
				if (string.IsNullOrEmpty(str)) {
					str = s;
				} else {
					str += ";" + s;
				}
			}

			UserPrefs.instance.SetString(path, str);
		}

		static IList<InputKey> LoadKeys(string path) {
			var str = UserPrefs.instance.GetString(path, null);
			if (str != null) {
				List<InputKey> keys = new List<InputKey>();

				var names = str.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);

				for (int i = 0; i < names.Length; ++i) {
					keys.Add((InputKey)System.Enum.Parse(typeof(InputKey), names[i]));
				}
				
				return keys;
			}

			return null;
		}

		static IList<int> LoadButtons(string path) {
			var str = UserPrefs.instance.GetString(path, null);
			if (str != null) {
				List<int> buttons = new List<int>();

				var names = str.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);

				for (int i = 0; i < names.Length; ++i) {
					buttons.Add(int.Parse(names[i]));
				}

				return buttons;
			}

			return null;
		}

		GameplayInputActions() {
			selectAll = CreateChordAction("Input.Action.Gameplay.SelectAll", InputKey.Tab);
			defend = CreateChordAction("Input.Action.Gameplay.Defend", InputKey.G);
			hold = CreateChordAction("Input.Action.Gameplay.Hold", InputKey.H);
			stop = CreateChordAction("Input.Action.Gameplay.Stop", InputKey.Space);
			focus = CreateChordAction("Input.Action.Gameplay.Focus", InputKey.F);
			scatter = CreateChordAction("Input.Action.Gameplay.Scatter", InputKey.N);
			unitSpecial = CreateChordAction("Input.Action.Gameplay.UnitSpecial", InputKey.B);
			moveLeft = CreateChordAction("Input.Action.Gameplay.PanLeft", InputKey.A);
			moveRight = CreateChordAction("Input.Action.Gameplay.PanRight", InputKey.D);
			moveForward = CreateChordAction("Input.Action.Gameplay.PanForward", InputKey.W);
			moveBackward = CreateChordAction("Input.Action.Gameplay.PanBackward", InputKey.S);
			smoothOrbitLeft = CreateChordAction("Input.Action.Gameplay.SmoothOrbitLeft", InputKey.Q);
			smoothOrbitRight = CreateChordAction("Input.Action.Gameplay.SmoothOrbitRight", InputKey.E);
			smoothOrbitUp = CreateChordAction("Input.Action.Gameplay.SmoothOrbitUp", InputKey.LeftBracket);
			smoothOrbitDown = CreateChordAction("Input.Action.Gameplay.SmoothOrbitDown", InputKey.RightBracket);
			smoothZoomIn = CreateChordAction("Input.Action.Gameplay.SmoothZoomIn", InputKey.Insert);
			smoothZoomOut = CreateChordAction("Input.Action.Gameplay.SmoothZoomOut", InputKey.Delete);
			snapOrbitLeft = new UnboundInputAction("Input.Action.Gameplay.SnapOrbitLeft");//, InputKey.LeftBracket);
			snapOrbitRight = new UnboundInputAction("Input.Action.Gameplay.SnapOrbitRight");//, InputKey.RightBracket);
			snapOrbitUp = new UnboundInputAction("Input.Action.Gameplay.SnapOrbitUp");//, InputKey.KeypadMinus);
			snapOrbitDown = new UnboundInputAction("Input.Action.Gameplay.SnapOrbitDown");//, InputKey.KeypadPlus);
			snapZoomIn = CreateChordAction("Input.Action.Gameplay.SnapZoomIn", InputKey.ScrollWheelUp);
			snapZoomOut = CreateChordAction("Input.Action.Gameplay.SnapZoomOut", InputKey.ScrollWheelDown);
			resetPitch = CreateChordAction("Input.Action.Gameplay.ResetPitch", InputKey.Home);
			mouseOrbit = new UnboundInputAction("Input.Action.Gameplay.MouseOrbit");//, new[] { InputKey.LeftShift }, new[] { MouseButton.Middle } );
			mousePan = new UnboundInputAction("Input.Action.Gameplay.MousePan");//, MouseButton.Middle);
			action = CreateKeyAction("Input.Action.Gameplay.Action", MouseButton.Right);
			select = CreateKeyAction("Input.Action.Gameplay.Select", MouseButton.Left);
			actionModifier = CreateKeyAction("Input.Action.Gameplay.ActionModifier", InputKey.LeftControl);
			selectModifier = CreateKeyAction("Input.Action.Gameplay.SelectModifier", InputKey.LeftShift);
			moveModifier = CreateKeyAction("Input.Action.Gameplay.MoveModifier", InputKey.LeftAlt);
			say = CreateKeyAction("Input.Action.Gameplay.Say", InputKey.T);
			teamSay = CreateKeyAction("Input.Action.Gameplay.TeamSay", InputKey.Y);
			peekChat = CreateKeyAction("Input.Action.Gameplay.PeekChat", InputKey.UpArrow);

			spells = new InputAction[/*Client.UI.HUDAbilityBar.NUM_ABILITIES*/6];
			for (int i = 0; i < spells.Length; ++i) {
				InputKey key = InputKey.None;

				switch (i) {
					case 0:
						key = InputKey.Z;
					break;
					case 1:
						key = InputKey.X;
					break;
					case 2:
						key = InputKey.C;
					break;
					case 3:
						key = InputKey.Period;
					break;
					case 4:
						key = InputKey.V;
					break;
					case 5:
						key = InputKey.R;
					break;
				}

				spells[i] = CreateKeyAction("Input.Action.Gameplay.Spell" + i, key);
			}
		}

		public void Destroy() {
			ClearActions();
		}
	}
}

