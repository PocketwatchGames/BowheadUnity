using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    public enum ECritterBehaviorType {
        Flee,
        Attack
    }

    abstract public class CritterBehavior {
        abstract public void Tick(Critter c, float dt, ref Pawn.Input_t input);

        public static CritterBehavior Create(ECritterBehaviorType t) {
            switch (t) {
                case ECritterBehaviorType.Flee:
                    return new CritterBehaviorFlee();
                case ECritterBehaviorType.Attack:
                    return new CritterBehaviorAttack();
            }
            return null;
        }
    }

    public class CritterBehaviorFlee : CritterBehavior {
        override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                input.movement = diff.normalized;
                input.look = input.movement;
            }
            if (c.canJump && c.activity == Pawn.Activity.OnGround && c.sprintTimer == 0) {
                input.inputs[(int)InputType.Jump] = InputState.JustPressed;
            }

        }
    }
    public class CritterBehaviorAttack : CritterBehavior {
        override public void Tick(Critter c, float dt, ref Pawn.Input_t input) {
            input.movement = Vector3.zero;
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.rigidBody.position - c.lastKnownPosition;

                if (diff.y <= -3) {
                    if (c.canJump && c.activity == Pawn.Activity.OnGround) {
                        input.inputs[(int)InputType.Jump] = InputState.JustPressed;
                    }
                }
                diff.y = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                var desiredPos = c.lastKnownPosition + diff.normalized * 5;
                var move = desiredPos - c.position;
                move.y = 0;

                if (move.magnitude > 0.5f) {
                    input.movement = move.normalized;
                }
                else {
                    if (c.canAttack && c.activity == Pawn.Activity.OnGround) {
                        input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
                    }
                }
				input.look = -diff;
            }

        }
    }
}
