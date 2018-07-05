// Copyright (c) 2018 Pocketwatch Games, LLC.

using UnityEngine;
using UnityEngine.EventSystems;
using System;
using System.Reflection;
using System.Collections.Generic;
using CodeStage.AdvancedFPSCounter.Labels;
using MenuActions = Bowhead.MenuActions;
using System.Linq;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CFunc : Attribute {
	// Only Server CFunc permission levels are checked.

	public const int Admin = 0;
	public const int Officer = 100;
	public const int Moderator = 200;
	public const int Any = 999;

	int _permissionLevel = Admin;
	bool _isCheat = false;
	bool _isServer = false;
	string[] _names;
	
	public CFunc() { }
	public CFunc(CFunc other) {
		_permissionLevel = other._permissionLevel;
		_isCheat = other._isCheat;
		_isServer = other._isServer;
	}

	public int permissionLevel {
		get {
			return _permissionLevel;
		}
		set {
			_permissionLevel = value;
		}
	}

	public bool isCheat {
		get {
			return _isCheat;
		}
		set {
			_isCheat = value;
		}
	}

	public bool isServer {
		get {
			return _isServer;
		}
		set {
			_isServer = value;
		}
	}

	public string[] shortcuts {
		get {
			return _names;
		}
		set {
			_names = value;
		}
	}
}

public class CFuncMethod {
	public MethodInfo method;
	public CFunc cfunc;
}

public class Console : MonoBehaviour {

	const int MAX_CHARS = 16000;
	const int MAX_HISTORY = 16*1024;
	static readonly char[] delims = new char[] { '\n' };

	static Console instance;
	Canvas canvas;
	RectTransform consoleRoot;
	RectTransform consolePanel;
	UnityEngine.UI.InputField inputField;
	UnityEngine.UI.Text text;

	LabelAnchor fpsAnchor;
	LabelAnchor memAnchor;
	LabelAnchor gpuAnchor;
	
	LinkedList<string> history = new LinkedList<string>();
	List<string> commandHistory = new List<string>();

	float lerp = 1f;
	bool open;
	bool dirty;
	bool unterminated;
	int scrollOffset;
	int recallIndex;
	float maxTextVerticalSize;
	Vector2 cachedCavasSize;

	public float ascendSpeed = 0.2f;
	public float descendSpeed = 0.2f;

	Executor executor;
	TabComplete tabComplete;

	List<string> tabCompletionList;
	int tabCompleteIndex;
	
	public delegate void Executor(string commandBuffer);
	public delegate List<string> TabComplete(string commandBuffer);

	void Awake() {
		if (instance != null) {
			throw new System.Exception("Multiple console objects!");
		}

		instance = this;

		canvas = GetComponent<Canvas>();
		canvas.enabled = true;
		consoleRoot = GetComponent<RectTransform>();
		consolePanel = transform.Find("Panel").gameObject.GetComponent<RectTransform>();

		inputField = consolePanel.transform.Find("InputField").GetComponent<UnityEngine.UI.InputField>();
		text = consolePanel.transform.Find("Text").GetComponent<UnityEngine.UI.Text>();
	
		text.text = string.Empty;
		
	}

	// Update is called once per frame
	void Update() {

		if (open && ((cachedCavasSize.x != consoleRoot.rect.width) || (cachedCavasSize.y != consoleRoot.rect.height))) {
			consolePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, consoleRoot.rect.height/2);
			cachedCavasSize = new Vector2(consoleRoot.rect.width, consoleRoot.rect.height);
			Canvas.ForceUpdateCanvases();
			maxTextVerticalSize = consolePanel.rect.height - inputField.GetComponent<RectTransform>().rect.height;
			text.GetComponent<UnityEngine.UI.LayoutElement>().flexibleHeight = 0f;
			dirty = true;
		}

		if (MenuActions.instance.toggleConsole.pressed) {
			InternalToggle();
		}

		if (MenuActions.instance.cancel.pressed && open) {
			InternalClose();
		}

		if (open) {
			if (MenuActions.instance.consoleHistoryUp.repeat) {
				++scrollOffset;
				dirty = true;
			} else if (MenuActions.instance.consoleHistoryDown.repeat) {
				scrollOffset = Mathf.Max(0, scrollOffset - 1);
				dirty = true;
			} else if (MenuActions.instance.consoleHistoryEnd.pressed) {
				if (scrollOffset > 0) {
					scrollOffset = 0;
					dirty = true;
				}
			} else if (MenuActions.instance.up.pressed) {
				RecallDown();
			} else if (MenuActions.instance.down.pressed) {
				RecallUp();
			} else if (MenuActions.instance.consoleTabComplete.pressed) {
				CycleTabComplete();
			}
		}
		
		// animate the console.
		if (open) {
			lerp = Mathf.Clamp01(lerp - Time.unscaledDeltaTime * 1/ascendSpeed);
		} else {
			lerp = Mathf.Clamp01(lerp + Time.unscaledDeltaTime * 1/descendSpeed);
		}

		canvas.enabled = lerp != 1f;
		
		var consolePos = consolePanel.transform.localPosition;
		consolePos.y = (lerp*consolePanel.rect.height);

		consolePanel.transform.localPosition = consolePos;

		if (dirty && open) {
			scrollOffset = UpdateText(scrollOffset);
			dirty = false;
		}
	}

	static LabelAnchor MoveAnchorToBottom(LabelAnchor anchor) {
		switch (anchor) {
			case LabelAnchor.UpperCenter:
				return LabelAnchor.LowerCenter;
			case LabelAnchor.UpperLeft:
				return LabelAnchor.LowerLeft;
			case LabelAnchor.UpperRight:
				return LabelAnchor.LowerRight;
			default:
				return anchor;
		}
	}

	void RecallUp() {
		if (commandHistory.Count > 0) {
			++recallIndex;
			if (recallIndex > commandHistory.Count) {
				recallIndex = 1;
			}
			Recall();
		}
	}

	void RecallDown() {
		if (commandHistory.Count > 0) {
			--recallIndex;
			if (recallIndex < 1) {
				recallIndex = commandHistory.Count;
			}
			Recall();
		}
	}

	void Recall() {
		inputField.text = commandHistory[recallIndex - 1];
		inputField.MoveTextEnd(false);
	}

	void CycleTabComplete() {
		if (!string.IsNullOrEmpty(inputField.text)) {
			if (tabCompleteIndex == 0) {
				if (tabComplete != null) {
					tabCompletionList = tabComplete(inputField.text);
					if (tabCompletionList.Count < 1) {
						tabCompletionList = null;
					}
				}

				if (tabCompletionList != null) {
					inputField.text = tabCompletionList[0];
					inputField.MoveTextEnd(false);
					tabCompleteIndex = 1;
				}
			} else {
				var nextIndex = tabCompleteIndex + 1;
				if (nextIndex > tabCompletionList.Count) {
					nextIndex = 1;
				}
				inputField.text = tabCompletionList[nextIndex - 1];
				inputField.MoveTextEnd(false);
				tabCompleteIndex = nextIndex;
			}
		}
	}

	void InternalOpen() {
		if (!open) {
			open = true;
			inputField.enabled = true;
			ClearTildeKeyInput();
			inputField.ActivateInputField();
			inputField.MoveTextEnd(false);

			var fpsCounter = Bowhead.GameManager.instance.fpsCounter;
			if (fpsCounter != null) {
				fpsAnchor = fpsCounter.fpsCounter.Anchor;
				memAnchor = fpsCounter.memoryCounter.Anchor;
				gpuAnchor = fpsCounter.deviceInfoCounter.Anchor;

				fpsCounter.fpsCounter.Anchor = MoveAnchorToBottom(fpsCounter.fpsCounter.Anchor);
				fpsCounter.memoryCounter.Anchor = MoveAnchorToBottom(fpsCounter.memoryCounter.Anchor);
				fpsCounter.deviceInfoCounter.Anchor = MoveAnchorToBottom(fpsCounter.deviceInfoCounter.Anchor);
			}
		}
	}

	void InternalClose() {
		if (open) {
			open = false;
			ClearTildeKeyInput();
			inputField.DeactivateInputField();
			inputField.enabled = false;
			InputManager.ClearUIFocus();

			var fpsCounter = Bowhead.GameManager.instance.fpsCounter;
			if (fpsCounter != null) {
				fpsCounter.fpsCounter.Anchor = fpsAnchor;
				fpsCounter.memoryCounter.Anchor = memAnchor;
				fpsCounter.deviceInfoCounter.Anchor = gpuAnchor;
			}
		}
	}

	void InternalCloseImmediate() {
		InternalClose();
		lerp = 1f;
	}

	void InternalToggle() {
		if (open) {
			InternalClose();
		} else {
			InternalOpen();
		}
	}

	void ClearTildeKeyInput() {
		if (inputField.text.EndsWith("`")) {
			inputField.text = inputField.text.Substring(0, inputField.text.Length - 1);
		}
	}

	void AppendText(string text) {
		if (text == "") {
			return;
		}

		LinkedListNode<string> lineNode = null;

		if (unterminated) {
			lineNode = history.Last;
		}

		unterminated = text[text.Length-1] != '\n';

		string[] lines = text.Split(delims);

		for (int i = 0; i < lines.Length; ++i) {
			if (lines[i] == "") {
				if (i == lines.Length-1) {
					break;
				}
			}

			if (lineNode != null) {
				lineNode.Value += lines[i];
				lineNode = null;
			} else {
				if (history.Count >= MAX_HISTORY) {
					history.RemoveFirst();
				}
				history.AddLast(lines[i]);
			}
		}

		dirty = true;
	}

	int UpdateText(int scrollOffset) {

		string buffer;
		bool done = false;

		do {
			int lineSkip = scrollOffset;

			if (scrollOffset > 0) {
				buffer = "<color=green>^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^\n</color>";
			} else {
				buffer = string.Empty;
			}

			for (var line = history.Last; line != null; line = line.Previous) {
				if (line.Previous == null) {
					done = true;
				}

				if (lineSkip > 0) {
					--lineSkip;
					continue;
				}

				var temp = buffer;
				buffer = line.Value + "\n" + buffer;
				text.text = buffer;

				if ((text.text.Length > MAX_CHARS) || (text.preferredHeight > maxTextVerticalSize)) {
					buffer = temp;
					done = true;
					break;
				}
			}

			// adjust line skip to valid value.
			scrollOffset = scrollOffset - lineSkip;

		} while (!done && (history.Count > 0));

		text.text = buffer;

		return scrollOffset;
	}

	void ClearHistory() {
		scrollOffset = 0;
		history.Clear();
		dirty = true;
	}

	public void InternalTextEntered() {
		if (open && (inputField.text != "")) {
			string command = inputField.text;

			AppendText(inputField.text + "\n");
			inputField.text = "";
			inputField.ActivateInputField();

			Exec(command);
		}
	}

	public void InternalTextChanged() {
		tabCompleteIndex = 0;
	}

	public static bool isOpen {
		get {
			return (instance != null) && instance.open;
		}
	}

	public static bool Open() {
		if (instance != null) {
			instance.InternalOpen();
		}
		return isOpen;
	}

	public static bool Close() {
		if (instance != null) {
			instance.InternalClose();
		}
		return !isOpen;
	}

	public static bool CloseImmediate() {
		if (instance != null) {
			instance.InternalCloseImmediate();
		}
		return !isOpen;
	}

	public static bool Toggle() {
		if (instance != null) {
			instance.InternalToggle();
		}
		return isOpen;
	}

	public static void SetExecutor(Executor executor) {
		instance.executor = executor;
	}

	public static void SetTabComplete(TabComplete completer) {
		instance.tabComplete = completer;
	}

	public static void Exec(string command) {
		if (instance != null) {
			instance.commandHistory.Remove(command);
			instance.commandHistory.Add(command);
			instance.recallIndex = 0;
			if (instance.commandHistory.Count > 256) {
				instance.commandHistory.RemoveAt(0);
			}
			if (instance.executor != null) {
				instance.executor(command);
			}
		}
	}

	public static void ExecNoHistory(string command) {
		if (instance != null) {
			instance.recallIndex = 0;
			if (instance.executor != null) {
				instance.executor(command);
			}
		}
	}

	public static void Print(LogType type, string message) {
		if (instance != null) {
			switch (type) {
				case LogType.Assert:
					message = "<color=orange>" + message + "</color>";
				break;
				case LogType.Error:
					message = "<color=red>" + message + "</color>";
				break;
				case LogType.Exception:
					message = "<color=red>" + message + "</color>";
				break;
				case LogType.Warning:
					message = "<color=yellow>" + message + "</color>";
				break;
			}

			instance.AppendText(message + "\n");
		}
	}

	public static Dictionary<string, CFuncMethod> GetCFuncs(Assembly[] assemblies) {
		Dictionary<string, CFuncMethod> cvarMethods = new Dictionary<string, CFuncMethod>();

		using (var stopWatch = StopWatchPool.New()) {
			stopWatch.stopWatch.Start();

			foreach (var a in assemblies) {
				foreach (var t in a.GetTypes()) {
					if (!t.IsClass) {
						continue;
					}
					foreach (var m in t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static)) {
						if (!m.IsConstructor && (m.DeclaringType == t)) {
							var cvarAttrs = m.GetCustomAttributes(typeof(CFunc), false);
							if (cvarAttrs.Length > 0) {
								var cfuncMethod = new CFuncMethod();
								cfuncMethod.cfunc = (CFunc)cvarAttrs[0];
								cfuncMethod.method = m;

								var nameList = cfuncMethod.cfunc.shortcuts;
								if ((nameList != null) && (nameList.Length > 0)) {
									foreach (var cvarName in nameList) {
										var name = cvarName.ToLower();
										CFuncMethod collision;
										if (cvarMethods.TryGetValue(name, out collision)) {
											throw new System.Exception("CFunc defined multiple times, first at: " + collision.method.DeclaringType.FullName + "." + collision.method.Name + ", and then at: " + cfuncMethod.method.DeclaringType.FullName + "." + cfuncMethod.method.Name);
										} else {
											cvarMethods.Add(name, cfuncMethod);
										}
									}
								}

								{
									var cvarName = m.Name.ToLower();
									CFuncMethod collision;
									if (cvarMethods.TryGetValue(cvarName, out collision)) {
										throw new System.Exception("CFunc defined multiple times, first at: " + collision.method.DeclaringType.FullName + "." + collision.method.Name + ", and then at: " + cfuncMethod.method.DeclaringType.FullName + "." + cfuncMethod.method.Name);
									} else {
										cvarMethods.Add(cvarName, cfuncMethod);
									}
								}
							}
						}
					}
				}
			}

			Debug.Log("Loaded CFuncs in " + stopWatch.stopWatch.Elapsed);
		}

		return cvarMethods;
	}

	public static object[] TryParseCFuncArguments(MethodInfo m, string[] stringArgs, out string error) {
		error = null;

		List<object> args = new List<object>();
		var parameters = m.GetParameters();
		var lastArgIsParamArray = (parameters.Length > 0) ? parameters[parameters.Length-1].GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0 : false;

		// too many parameters?
		if (stringArgs.Length-1 > parameters.Length) {
			if (!lastArgIsParamArray) {
				error = "Expected " + parameters.Length + " arguments.";
				return null;
			}
		}
		
		for (int i = 0; i < parameters.Length; ++i) {
			var param = parameters[i];
			var ptype = param.ParameterType;

			var isParameterArray = param.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0;

			if (i >= stringArgs.Length-1) {
				// error, not enough arguments...
				if (!isParameterArray) {
					if (lastArgIsParamArray) {
						error = "Expected " + (parameters.Length-1) + " or more arguments.";
					} else {
						error = "Expected " + parameters.Length + " arguments.";
					}
					return null;
				}

				// parameter array.
				args.Add(new object[] { });
				break;
			} else if (isParameterArray) {

				List<object> parms = new List<object>();

				// pack in all remaining arguments as strings.
				for (int k = i; k < stringArgs.Length-1; ++k) {
					parms.Add(stringArgs[k+1]);
				}

				args.Add(parms.ToArray());

				break;
			}

			try {
				var val = Convert.ChangeType(stringArgs[i+1], ptype);
				if (val == null) {
					throw new System.InvalidCastException("Cannot parse a " + ptype.FullName + " from a string.");
				}
				args.Add(val);
			} catch (Exception e) {
				error = "Error parsing parameter " + i + ", expected a '" + ptype.FullName + ", exception = " + e.StackTrace;
				return null;
			}
		}

		return args.ToArray();
	}

	public static List<string> Tokenize(string command) {
		var result = command.Split('"')
				.Select((element, index) => index % 2 == 0
									? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)  // Split the item
									: new string[] { element })
				.SelectMany(element => element).ToList();
		return result;
	}

	static string GetTypeName(Type type) {
		if (type == typeof(bool)) {
			return "bool";
		} else if (type == typeof(byte)) {
			return "byte";
		} else if (type == typeof(sbyte)) {
			return "sbyte";
		} else if (type == typeof(short)) {
			return "short";
		} else if (type == typeof(ushort)) {
			return "ushort";
		} else if (type == typeof(int)) {
			return "int";
		} else if (type == typeof(uint)) {
			return "uint";
		} else if (type == typeof(long)) {
			return "long";
		} else if (type == typeof(ulong)) {
			return "ulong";
		} else if (type == typeof(float)) {
			return "float";
		} else if (type == typeof(double)) {
			return "double";
		} else if (type == typeof(string)) {
			return "string";
		} else if (type == typeof(void)) {
			return "void";
		}

		return type.FullName;
	}

	public static string GetFuncPrototype(CFuncMethod cfuncMethod) {
		var prototype = GetTypeName(cfuncMethod.method.ReturnType) + " ";

		if ((cfuncMethod.cfunc.shortcuts != null) && (cfuncMethod.cfunc.shortcuts.Length > 0)) {
			prototype += "[" + cfuncMethod.method.Name.ToLower();

			foreach (var name in cfuncMethod.cfunc.shortcuts) {
				prototype += ", " + name;
			}

			prototype += "]";
		} else {
			prototype += cfuncMethod.method.Name.ToLower();
		}

		prototype += "(";

		bool firstParam = true;
		foreach (var p in cfuncMethod.method.GetParameters()) {
			if (!firstParam) {
				prototype += ", ";
			}
			firstParam = false;

			if (p.GetCustomAttributes(typeof(ParamArrayAttribute), false).Length > 0) {
				prototype += "params string[] " + p.Name;
			} else {
				prototype += GetTypeName(p.ParameterType) + " " + p.Name;
			}
		}

		prototype += ")";
		return prototype;
	}

	public static void PrintCFuncList(Dictionary<string, CFuncMethod> cfuncs) {
		List<CFuncMethod> methods = new List<CFuncMethod>();
		foreach (var cfuncMethod in cfuncs.Values) {
			if (!methods.Contains(cfuncMethod)) {
				methods.Add(cfuncMethod);
				Debug.LogWarning(GetFuncPrototype(cfuncMethod));
			}
		}
	}

	[CFunc(shortcuts = new[] { "cls" })]
	public static void ClearScreen() {
		if (instance != null) {
			instance.ClearHistory();
		}
	}

	[CFunc(shortcuts = new[] { "cch" })]
	public static void ClearCommandHistory() {
		if (instance != null) {
			instance.commandHistory.Clear();
			instance.recallIndex = 0;
		}
	}
}
