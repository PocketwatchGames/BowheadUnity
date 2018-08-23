using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/Hover")]
	public class HoverData : BehaviorData<Critter.Hover> {
		public float destinationTolerance = 1.5f;
		public float hoverDistance = 4;
		public float walkSpeed = 0.4f;
		public float waitTime;
	}

	public partial class Critter : Pawn<Critter, CritterData> {
		public class Hover : CritterBehavior<HoverData> {

			Vector3 desiredPosition;
			public float waitTimer;
			public bool reachedPosition;

			public override void Init(Critter c, BehaviorData d, float score, int weaponIndex, int attackIndex) {
				base.Init(c, d, score, weaponIndex, attackIndex);
			}

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}
				return new EvaluationScore(this, 1.0f);
			}

			public override bool IsValid() {
				if (waitTimer > data.waitTime) {
					return false;
				}
				return base.IsValid();
			}

			public override void Start() {
				base.Start();

				var angle = Random.Range(0, Mathf.PI * 2);
				desiredPosition = _critter.lastKnownPosition + data.hoverDistance * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

				waitTimer = 0;
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {

				var move = desiredPosition - _critter.position;
				move.y = 0;


				if (move.magnitude > data.destinationTolerance) {
					input.movement = move.normalized * data.walkSpeed;
					input.look = input.movement.normalized;
				} else {
					reachedPosition = true;
					input.look = _critter.lastKnownPosition - _critter.position;
				}
				if (reachedPosition) { 
					waitTimer += dt;
				}
			}

		}

	}
}
