using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/Patrol")]
	public class PatrolData : BehaviorData<Critter.Patrol> {
	}

	public partial class Critter : Pawn<Critter, CritterData> {

		public class Patrol : CritterBehavior<PatrolData> {

			public float destinationTolerance = 0.5f;
			public float patrolRange = 5.0f;
			public float patrolTimeMin = 4.0f;
			public float patrolTimeMax = 8.0f;
			public float patrolSpeed = 0.25f;

			Vector3 patrolPos;
			float patrolTimer;

			public override EvaluationScore Evaluate() {
				if (_critter.IsPanicked()) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override void Tick(float dt, ref Pawn.Input_t input) {
				if (_critter.hasLastKnownPosition && _critter.wary > 0) {
					var diff = _critter.lastKnownPosition - _critter.position;
					input.look = diff.normalized;
					input.movement = Vector3.zero;
				} else {
					patrolTimer -= dt;
					if (patrolTimer <= 0) {
						patrolTimer = Random.Range(patrolTimeMin, patrolTimeMax);
						GetNewPatrolPoint(_critter);
					}

					var diff = patrolPos - _critter.position;
					diff.y = 0;
					if (diff.magnitude > destinationTolerance) {
						input.movement = diff.normalized * patrolSpeed;
						input.look = input.movement;
					}

				}
			}

			private void GetNewPatrolPoint(Critter c) {
				var angle = Random.Range(0, Mathf.PI * 2);
				var desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
				patrolPos = _critter.position + desiredOffset * patrolRange;
			}
		}

	}
}
