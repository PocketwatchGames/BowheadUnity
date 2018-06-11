// Copyright (c) 2018 Pocketwatch Games LLC.

#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class EditorProgressBar {

	static bool progressOpen;
	static EditorProgressBar top;

	EditorProgressBar _parent;
	EditorProgressBar _child;

	int _numSteps;
	int _step;
	string _description;

	public EditorProgressBar(string description, int numSteps) {
		_numSteps = numSteps;
		_parent = top;
		if (_parent != null) {
			while (_parent._child != null) {
				_parent = _parent._child;
			}
		}
		_description = description;
		if (_parent != null) {
			_parent._child = this;
		} else {
			top = this;
		}
		Update();
	}

	public void Step(string description) {
		++_step;
		if (description != null) {
			_description = description;
		}
		if (_step >= _numSteps) {
			_step = _numSteps;
			Close();
		} else {
			Update();
		}
	}

	public void Close() {
		if (_parent != null) {
			_parent._child = null;
			_parent = null;
		} else if (top == this) {
			top = null;
			Update();
		}
	}

	public string description {
		get {
			return _description;
		}
		set {
			_description = value;
			if (_child == null) {
				Update();
			}
		}
	}

	public static void Clear() {
		if (progressOpen) {
			EditorUtility.ClearProgressBar();
			progressOpen = false;
		}
		top = null;
	}

	public static void Update() {
		float min = 0f;
		float max = 1f;

		var step = top;
		if (step != null) {
			while (step._child != null) {
				float newMin = min + (max-min)/step._numSteps*step._step;
				max = min + (max-min)/step._numSteps*(step._step+1);
				min = newMin;
				step = step._child;
			}
		}

		if ((step == null) && progressOpen) {
			EditorUtility.ClearProgressBar();
		} else if (step != null) {
			progressOpen = true;
			EditorUtility.DisplayProgressBar(top._description, (top != step) ? (step._description ?? "") : "", min + ((max-min)/step._numSteps*step._step));
		}
	}
}
#endif