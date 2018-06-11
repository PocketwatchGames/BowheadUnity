// Copyright (c) 2018 Pocketwatch Games LLC.

using System;
using UnityEngine;

namespace Bowhead {
	public class WaitForGameSeconds : CustomYieldInstruction {

		double _time;

		public WaitForGameSeconds(float seconds) {
			_time = GameManager.instance.gameTimeSinceStart + seconds;
		}

		public override bool keepWaiting {
			get {
				return GameManager.instance.gameTimeSinceStart < _time;
			}
		}
	}
}