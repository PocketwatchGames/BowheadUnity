using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
	[CreateAssetMenu(menuName = "Behaviors/Attack")]
	public class AttackData : BehaviorData<Critter.Attack> {
		public float destinationTolerance = 1.5f;
		public float enemyElevationDeltaToJump = 3;
		public float walkDistance = 4;
		public float walkSpeed = 0.4f;
		public float runSpeed = 1.0f;
		public float maxApproachDistance;
		public int attackCount;
		public float waitTime;
		public float weaponStaminaScorePower;
	}

	public partial class Critter : Pawn<Critter, CritterData> {
		public class Attack : CritterBehavior<AttackData> {

			Vector3 desiredOffset;
			public int attackCount;
			public float minRange;
			public float maxRange;
			public Weapon weapon;
			public WeaponData.AttackData attackData;
			public bool attacked;
			public float waitTimer;

			public override void Init(Critter c, BehaviorData d, float score, int weaponIndex, int attackIndex) {
				base.Init(c, d, score, weaponIndex, attackIndex);
				weapon = _critter.GetInventorySlot(weaponIndex) as Weapon;
				if (weapon == null) {
					return;
				}
				if (attackIndex <= weapon.data.attacks.Length) {
					attackData = weapon.data.attacks[attackIndex];
				}
				if (attackData != null) {
					if (attackData.projectiles.Count > 0) {
						maxRange = 10;
						minRange = 4;
					} else {
						maxRange = attackData.stepDistance + attackData.range + attackData.radius * 0.5f;
						minRange = maxRange - attackData.radius * 1.5f;
					}
				}
			}

			public override EvaluationScore Evaluate() {
				if (!_critter.IsPanicked() || !_critter.hasLastKnownPosition) {
					return fail;
				}
				float dist = getDistToAttackPos();
				if (data.maxApproachDistance >= 0 && dist > maxRange + data.maxApproachDistance) {
					return fail;
				}
				return new EvaluationScore(this, Mathf.Pow(weapon.stamina,data.weaponStaminaScorePower));
			}

			public override bool IsValid() {
				if (weapon == null || attackData == null) {
					return false;
				}

				if (waitTimer > data.waitTime) {
					return false;
				}
				return base.IsValid();
			}

			public override void Start() {
				base.Start();

				var angle = Random.Range(0, Mathf.PI * 2);
				desiredOffset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

				attackCount = data.attackCount;

				waitTimer = 0;
				attacked = false;
			}

			private float getDistToAttackPos() {
				var diff = _critter.lastKnownPosition - _critter.position;
				diff.y = 0;

				float enemyRadius = 0.5f;
				return diff.magnitude - enemyRadius;
			}

			override public void Tick(float dt, ref Pawn.Input_t input) {
				float dist = getDistToAttackPos();

				bool isInAttackChain = attackCount < data.attackCount;
				bool canStartAttacking = dist > minRange && dist < maxRange && _critter.canAttack && _critter.activity == Pawn.Activity.OnGround;

				if (attacked) {
					waitTimer += dt;
				} else if (data.attackCount > 0 && attackCount == 0) {
				} else if (isInAttackChain || canStartAttacking) {
					if (canStartAttacking) {
						input.look = _critter.lastKnownPosition - _critter.position;
					}
					if (weapon.CanCast()) {
						if (_critter.CanSee(_critter.gameMode.players[0].playerPawn) > 0) {
							input.attacks[weaponIndex] = new AttackState(weaponIndex, attackIndex, InputState.JustReleased);
							attackCount--;
							if (attackCount <= 0) {
								attacked = true;
							}
						}
					}
				} else if (!attacked) {

					var desiredPos = _critter.lastKnownPosition + desiredOffset * ((maxRange - minRange) / 2 + minRange);
					var move = desiredPos - _critter.position;
					move.y = 0;


					if (dist > data.walkDistance) {
						input.movement = move.normalized * data.runSpeed;
						input.look = input.movement.normalized;
					} else {
						input.movement = move.normalized * data.walkSpeed;
						input.look = _critter.lastKnownPosition - _critter.position;
					}
					if (_critter.rigidBody.position.y - _critter.lastKnownPosition.y <= -data.enemyElevationDeltaToJump) {
						if (_critter.canJump && _critter.activity == Pawn.Activity.OnGround) {
							input.inputs[(int)InputType.Jump] = InputState.JustPressed;
						}
					}
				}
			}

		}

	}
}
