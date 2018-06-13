using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    public enum CritterBehaviorType {
        Flee,
        Attack
    }

    abstract public class CritterBehavior {
        abstract public void update(Critter c, float dt, ref Actor.Input_t input);

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
        override public void update(Critter c, float dt, ref Actor.Input_t input) {
            input.inputs[(int)InputType.JUMP] = InputState.RELEASED;
            input.inputs[(int)InputType.ATTACK_RIGHT] = InputState.RELEASED;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                input.movement = diff.normalized;
                input.yaw = Mathf.Atan2(input.movement.x, input.movement.z);
            }
            if (c.canJump && c.activity == Actor.Activity.ONGROUND) {
                input.inputs[(int)InputType.JUMP] = InputState.JUST_PRESSED;
            }

        }
    }
    public class CritterBehaviorAttack : CritterBehavior {
        override public void update(Critter c, float dt, ref Actor.Input_t input) {
            input.movement = Vector3.zero;
            input.inputs[(int)InputType.JUMP] = InputState.RELEASED;
            input.inputs[(int)InputType.ATTACK_RIGHT] = InputState.RELEASED;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;

                if (diff.z <= -3) {
                    if (c.canJump && c.activity == Actor.Activity.ONGROUND) {
                        input.inputs[(int)InputType.JUMP] = InputState.JUST_PRESSED;
                    }
                }
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                var desiredPos = c.lastKnownPosition + diff.normalized * 5;
                var move = desiredPos - c.position;
                move.z = 0;

                if (move.magnitude > 0.5f) {
                    input.movement = move.normalized;
                }
                else {
                    if (c.canAttack && c.activity == Actor.Activity.ONGROUND) {
                        input.inputs[(int)InputType.ATTACK_RIGHT] = InputState.JUST_RELEASED;
                    }
                }
                input.yaw = Mathf.Atan2(-diff.x, -diff.z);
            }

        }
    }
}
