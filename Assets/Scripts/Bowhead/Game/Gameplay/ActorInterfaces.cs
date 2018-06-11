// Copyright (c) 2018 Pocketwatch Games LLC.

using UnityEngine;
using System.Collections.Generic;

namespace Bowhead {
	public interface ActorWithTeam {
		Actors.Team team { get; }
	}

	public interface TargetableActor : ActorWithTeam {
		
		// true if the actor bounds touch the screen rect.
		bool ProjectedBoundsTouchScreenRect(Camera camera, Rect rect);

		void SetHighlighted(bool highlight, float time);

		bool highlighted {
			get;
		}
	}

	public interface ScorableActor : ActorWithTeam {
		int killValue { get; }
	}
}