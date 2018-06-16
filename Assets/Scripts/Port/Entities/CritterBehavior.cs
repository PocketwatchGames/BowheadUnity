using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    public enum CritterBehaviorType {
        Flee,
        Attack
    }

    abstract public class CritterBehavior {
        abstract public void Tick(Critter c, float dt, ref Actor.Input_t input);

        public static CritterBehavior Create(CritterBehaviorType t) {
            switch (t) {
                case CritterBehaviorType.Flee:
                    return new CritterBehaviorFlee();
                case CritterBehaviorType.Attack:
                    return new CritterBehaviorAttack();
            }
            return null;
        }
    }

    public class CritterBehaviorFlee : CritterBehavior {
        override public void Tick(Critter c, float dt, ref Actor.Input_t input) {
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                input.movement = diff.normalized;
                input.yaw = Mathf.Atan2(input.movement.x, input.movement.z);
            }
            if (c.canJump && c.activity == Actor.Activity.OnGround) {
                input.inputs[(int)InputType.Jump] = InputState.JustPressed;
            }

        }
    }
    public class CritterBehaviorAttack : CritterBehavior {
        override public void Tick(Critter c, float dt, ref Actor.Input_t input) {
            input.movement = Vector3.zero;
            input.inputs[(int)InputType.Jump] = InputState.Released;
            input.inputs[(int)InputType.AttackRight] = InputState.Released;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;

                if (diff.y <= -3) {
                    if (c.canJump && c.activity == Actor.Activity.OnGround) {
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
                    if (c.canAttack && c.activity == Actor.Activity.OnGround) {
                        input.inputs[(int)InputType.AttackRight] = InputState.JustReleased;
                    }
                }
                input.yaw = Mathf.Atan2(-diff.x, -diff.z);
            }

        }
    }
}
