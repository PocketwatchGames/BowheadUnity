// Copyright (c) 2018 Pocketwatch Games LLC.


using System.Diagnostics;

public static class Perf {

	[Conditional("PROFILING")]	
	public static void Begin(string label) {
		UnityEngine.Profiling.Profiler.BeginSample(label);
	}

	[Conditional("PROFILING")]
	public static void End() {
		UnityEngine.Profiling.Profiler.EndSample();
	}
}
