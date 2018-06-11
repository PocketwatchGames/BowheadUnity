// Copyright (c) 2018 Pocketwatch Games LLC.
#if !UNITY_EDITOR
#define LOGGING
#endif

#if LOGGING
public static class Debug {

	public static void Log(object message) {
		Write(ELogLevel.Log, message.ToString());
	}

	public static void Log(object message, UnityEngine.Object context) {
		Write(ELogLevel.Log, message.ToString());
	}

	public static void LogFormat(string format, params object[] args) {
		Write(ELogLevel.Log, string.Format(null, format, args));
	}

	public static void LogFormat(UnityEngine.Object context, string format, params object[] args) {
		Write(ELogLevel.Log, string.Format(null, format, args));
	}

	public static void LogAssertion(object message) {
		UnityEngine.Debug.LogAssertion(message);
	}

	public static void LogAssertion(object message, UnityEngine.Object context) {
		UnityEngine.Debug.LogAssertion(message, context);
	}

	public static void LogAssertionFormat(string format, params object[] args) {
		UnityEngine.Debug.LogAssertionFormat(format, args);
	}

	public static void LogAssertionFormat(UnityEngine.Object context, string format, params object[] args) {
		UnityEngine.Debug.LogAssertionFormat(context, format, args);
	}

	public static void LogWarning(object message) {
		Write(ELogLevel.Warn, message.ToString());
	}

	public static void LogWarning(object message, UnityEngine.Object context) {
		Write(ELogLevel.Warn, message.ToString());
	}

	public static void LogWarningFormat(string format, params object[] args) {
		Write(ELogLevel.Warn, string.Format(null, format, args));
	}

	public static void LogWarningFormat(UnityEngine.Object context, string format, params object[] args) {
		Write(ELogLevel.Warn, string.Format(null, format, args));
	}

	public static void LogError(object message) {
		Write(ELogLevel.Error, message.ToString());
	}

	public static void LogError(object message, UnityEngine.Object context) {
		Write(ELogLevel.Error, message.ToString());
	}

	public static void LogErrorFormat(string format, params object[] args) {
		Write(ELogLevel.Error, string.Format(null, format, args));
	}

	public static void LogErrorFormat(UnityEngine.Object context, string format, params object[] args) {
		Write(ELogLevel.Error, string.Format(null, format, args));
	}

	public static void LogException(System.Exception exception) {
		UnityEngine.Debug.LogException(exception);
	}

	public static void LogException(System.Exception exception, UnityEngine.Object context) {
		UnityEngine.Debug.LogException(exception, context);
	}

	public static void DrawLine(UnityEngine.Vector3 start, UnityEngine.Vector3 end) {
		UnityEngine.Debug.DrawLine(start, end);
	}

	public static void DrawLine(UnityEngine.Vector3 start, UnityEngine.Vector3 end, UnityEngine.Color color) {
		UnityEngine.Debug.DrawLine(start, end, color);
	}

	public static void DrawLine(UnityEngine.Vector3 start, UnityEngine.Vector3 end, UnityEngine.Color color, float duration) {
		UnityEngine.Debug.DrawLine(start, end, color, duration);
	}

	public static void DrawLine(UnityEngine.Vector3 start, UnityEngine.Vector3 end, UnityEngine.Color color, float duration, bool depthTest) {
		UnityEngine.Debug.DrawLine(start, end, color, duration, depthTest);
	}

	public static void DrawRay(UnityEngine.Vector3 start, UnityEngine.Vector3 dir) {
		UnityEngine.Debug.DrawRay(start, dir);
	}

	public static void DrawRay(UnityEngine.Vector3 start, UnityEngine.Vector3 dir, UnityEngine.Color color) {
		UnityEngine.Debug.DrawLine(start, dir, color);
	}

	public static void DrawRay(UnityEngine.Vector3 start, UnityEngine.Vector3 dir, UnityEngine.Color color, float duration) {
		UnityEngine.Debug.DrawLine(start, dir, color, duration);
	}

	public static void DrawRay(UnityEngine.Vector3 start, UnityEngine.Vector3 dir, UnityEngine.Color color, float duration, bool depthTest) {
		UnityEngine.Debug.DrawLine(start, dir, color, duration, depthTest);
	}

	public static void ClearDeveloperConsole() {
		UnityEngine.Debug.ClearDeveloperConsole();
	}

	public static void Break() {
		UnityEngine.Debug.Break();
	}

	public static void DebugBreak() {
		UnityEngine.Debug.DebugBreak();
	}

	public static UnityEngine.ILogger logger {
		get {
			return UnityEngine.Debug.logger;
		}
	}

	public static bool isDebugBuild {
		get {
			return UnityEngine.Debug.isDebugBuild;
		}
	}

	public static bool developerConsoleVisible {
		get {
			return UnityEngine.Debug.developerConsoleVisible;
		}
		set {
			UnityEngine.Debug.developerConsoleVisible = value;
		}
	}

	enum ELogLevel {
		Log,
		Warn,
		Error
	}

	static string GetTimestamp() {
		if (Bowhead.GameManager.instance == null) {
			return "[00:00:00:000]/0";
		}
		var millis = System.Math.Floor(Bowhead.GameManager.instance.timeSinceStart * 1000);
		var hours = System.Math.Floor(millis/(60*60*1000));
		millis -= hours*60*60*1000;
		var mins = System.Math.Floor(millis/(60*1000));
		millis -= mins*60*1000;
		var secs = System.Math.Floor(millis / 1000);
		millis -= secs*1000;

		return string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D3}/{4}", (int)hours, (int)mins, (int)secs, (int)millis, UnityEngine.Time.frameCount);
	}

	static void Write(ELogLevel level, string message) {
		var time = GetTimestamp();

		switch (level) {
			case ELogLevel.Log:
				message = time + " (LOG): " + message;
				Console.Print(UnityEngine.LogType.Log, message);
			break;
			case ELogLevel.Warn:
				message = time + " (WRN): " + message;
				Console.Print(UnityEngine.LogType.Warning, message);
			break;
			case ELogLevel.Error:
				message = time + " (ERR): " + message;
				Console.Print(UnityEngine.LogType.Error, message);
			break;
		}

		System.Console.WriteLine(message);
	}
}
#endif