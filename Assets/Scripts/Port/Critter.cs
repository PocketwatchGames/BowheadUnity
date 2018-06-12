using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {
    public class Critter : Actor {

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }







         public class CState : Actor.CState {

            public float wary;
            public float panic;
            public bool hasLastKnownPosition;
            public Vector3 lastKnownPosition;

            public Item[] loot = new Item[MAX_INVENTORY_SIZE];
        };

        public class CData : Actor.CData {
            public delegate void updateFn(float dt, Critter c, ref Input_t input);
            public float fallDamageVelocity;

            public float visionWeight;
            public float smellWeight;
            public float hearingWeight;
            public float waryCooldownTime;
            public float panicCooldownTime;
            public float waryIncreaseAtMaxAwareness;
            public float waryIncreaseAtMaxAwarenessWhilePanicked;
            public updateFn updatePanicked;
        };





        new public CData Data { get { return GetData<CData>(); } }
        new public CState State { get { return GetState<CState>(); } }
        new public static CData GetData(string dataName) {
            return GetData<CData>(dataName);
        }




        #region core



        // TODO: move cameraYaw into the PlayerCmd struct
        public void Update(float dt) {

            State.canClimb = false;
            State.canClimbWell = false;
            State.canMove = true;
            State.canJump = true;
            State.canRun = true;
            State.canTurn = true;
            State.canAttack = true;

            if (State.stunned) {
                State.canRun = false;
                State.canJump = false;
                State.canClimb = false;
                State.canClimbWell = false;
                State.canAttack = false;
                State.canMove = false;
            }

            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (State.inventory[i] != null) {
                    State.inventory[i].updateCast(dt, this);
                }
            }

            Input_t input;
            updateBrain(dt, input);

            base.Update(dt, input);

            if (State.canAttack) {
                foreach(var weapon in State.inventory)
        
                {
                    if (weapon != null) {
                        if (input.IsPressed(InputType::ATTACK_RIGHT)) {
                            weapon.charge(dt);
                        }
                        else {
                            if (input.inputs[(int)InputType::ATTACK_RIGHT] == InputState::JUST_RELEASED) {
                                weapon.attack(this);
                            }
                            weapon.State.chargeTime = 0;
                        }
                    }
                }
            }
            else {
                foreach(var weapon in State.inventory)
        
                {
                    if (weapon != null) {
                        weapon.State.chargeTime = 0;
                    }
                }
            }



            if (State.health <= 0) {
                removeFlag = true;

                foreach (var i in State.loot) {
                    if (i != null) {
                        i.State.position = State.position;
                        i.State.velocity = new Vector3((float)(std::rand() % 21 - 10), (float)(std::rand() % 21 - 10), 18);
                        world.items.push_back(i);
                    }
                }
            }
        }

        void init() {
            memset(&State, 0, sizeof(State));
            removeFlag = false;

            State.canClimb = false;
            State.canClimbWell = false;
            State.canMove = true;
            State.canJump = true;
            State.canRun = true;
            State.canTurn = true;
            State.canAttack = true;
        }

        void spawn(Vector3 pos) {
            State.spawned = true;
            State.position = pos;
            State.maxHealth = Data.maxHealth;
            State.health = State.maxHealth;
        }

        #endregion

        #region brain

        void updateBrain(float dt, ref Input_t input) {
            //	foreach(var p in player.world.Players)
            {
                Player p = world.player;

                float awareness = (canSee(p) * Data.visionWeight + canSmell(p) * Data.smellWeight + canHear(p) * Data.hearingWeight) / (Data.visionWeight + Data.smellWeight + Data.hearingWeight);

                float waryIncrease = isPanicked() ? Data.waryIncreaseAtMaxAwarenessWhilePanicked : Data.waryIncreaseAtMaxAwareness;
                waryIncrease *= awareness;
                if (awareness > 0) {
                    float maxWary = 2f;
                    State.wary = Math.Min(maxWary, State.wary + dt * waryIncrease);
                    if (State.wary > 1.0f) {
                        State.hasLastKnownPosition = true;
                        State.lastKnownPosition = p.State.position;
                        State.panic = 1f;
                    }
                }
                else {
                    State.panic = Math.Max(0, State.panic - dt / Data.panicCooldownTime);
                    State.wary = Math.Max(0, State.wary - dt / Data.waryCooldownTime);

                }

            }

            input.yaw = State.yaw;
            if (isPanicked()) {
                if (Data.updatePanicked != null) {
                    Data.updatePanicked(dt, this, input);
                }
            }
            else {
                input.movement = Vector3.zero;
                if (State.hasLastKnownPosition) {
                    var diff = State.lastKnownPosition - State.position;
                    input.yaw = Math.Atan2(diff.y, diff.x);
                }
            }

            //float yawDiff = constrainAngle(input.yaw - State.yaw);
            //float turnSpeed = 4;
            //float turn = 0;
            //if (yawDiff > 0)
            //{
            //	turn = dt * turnSpeed;
            //}
            //else if (yawDiff < 0)
            //{
            //	turn = -dt * turnSpeed;
            //}
            //input.yaw = constrainAngle(State.yaw + turn);

        }

        public void bounceAndFlee(float dt, ref Input_t input) {

            input.inputs[(int)InputType::JUMP] = InputState::RELEASED;
            input.inputs[(int)InputType::ATTACK_RIGHT] = InputState::RELEASED;
            if (State.hasLastKnownPosition) {
                var diff = State.position - State.lastKnownPosition;
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                input.movement = diff.normalized;
                input.yaw = Math.Atan2(input.movement.y, input.movement.x);
            }
            if (State.canJump && State.activity == Activity.ONGROUND) {
                input.inputs[(int)InputType::JUMP] = InputState::JUST_PRESSED;
            }
        }

        public void approachAndAttack(float dt, ref Input_t input) {
            input.movement = Vector3.zero;
            input.inputs[(int)InputType::JUMP] = InputState::RELEASED;
            input.inputs[(int)InputType::ATTACK_RIGHT] = InputState::RELEASED;
            if (State.hasLastKnownPosition) {
                var diff = State.position - State.lastKnownPosition;

                if (diff.z <= -3) {
                    if (State.canJump && State.activity == Activity.ONGROUND) {
                        input.inputs[(int)InputType::JUMP] = InputState::JUST_PRESSED;
                    }
                }
                diff.z = 0;
                if (diff == Vector3.zero) {
                    diff.x = 1;
                }
                var desiredPos = State.lastKnownPosition + diff.normalized * 5;
                var move = desiredPos - State.position;
                move.z = 0;

                if (move.magnitude > 0.5f) {
                    input.movement = move.normalized;
                }
                else {
                    if (State.canAttack && State.activity == Activity.ONGROUND) {
                        input.inputs[(int)InputType::ATTACK_RIGHT] = InputState::JUST_RELEASED;
                    }
                }
                input.yaw = Math.Atan2(-diff.y, -diff.x);
            }
        }


float canSmell(Player player) {
    float basicSmellDist = 1;

    var diff = State.position - player.State.position;
    float dist = diff.magnitude;
    if (dist == 0)
        return 1;

    float smell = 0;
    if (dist < basicSmellDist) {
        smell = Math.Max(0f, (float)Math.Pow(1f - dist / basicSmellDist, 2));
    }

    float windCarryTime = 5f;
    var wind = world.getWind(player.State.position);
    float maxWindCarryDist = Math.Sqrt(wind.magnitude) * windCarryTime;
    float windCarrySmell = 0;
    if (dist < maxWindCarryDist && wind != Vector3.zero) {
        windCarrySmell = 1f - dist / maxWindCarryDist;
        float windCarryAngleDot = wind.normalized.dot(diff.normalized);
        windCarrySmell *= windCarryAngleDot;
    }

    return Math.Max(smell, windCarrySmell);
}
float canHear(Player player) {
    if (player.State.activity != Activity.ONGROUND)
        return 0;
    float playerSpeed = player.State.velocity.magnitude / player.Data.groundMaxSpeed;
    if (playerSpeed == 0)
        return 0;

    var diff = player.State.position - State.position;
    float distance = diff.magnitude;

    if (distance == 0)
        return 1f;

    float fullSpeedAudibleDistance = 30f;
    float playerSound = std::clamp(1f - (distance / (fullSpeedAudibleDistance * playerSpeed)), 0f, 1f);
    if (playerSound <= 0)
        return 0;

    return (float)Math.Pow(playerSound, 0.25f);
}
float canSee(Player player) {
    var diff = player.State.position - State.position;
    float dist = diff.magnitude;

    float visionDistance = 10;
    //float sunriseTime = 2;
    //float sunsetTime = 22;
    //float sunChangeTime = 2;
    //if (world.Weather.tod >= sunriseTime + sunChangeTime && world.Weather.tod < sunriseTime - sunChangeTime)
    //{
    //	visionDistance += 15;
    //}
    //else if (world.Weather.tod >= sunriseTime && world.Weather.tod < sunriseTime + sunChangeTime)
    //{
    //	visionDistance += 15f*(world.Weather.tod - sunriseTime) / sunChangeTime;
    //}
    //else if (world.Weather.tod >= sunsetTime - sunChangeTime && world.Weather.tod < sunsetTime)
    //{
    //	visionDistance += 15f*(sunsetTime - world.Weather.tod) / sunChangeTime;
    //}
    //else
    //{
    //	visionDistance += 0;
    //}

    if (dist > visionDistance)
        return 0;

    float angleToPlayer = Math.Atan2(diff.y, diff.x);
    float angleDiff = Math.Abs(constrainAngle(angleToPlayer - State.yaw));
    float maxVisionAngle = pi_over_2<float>();
    if (angleDiff > maxVisionAngle)
        return 0;

    float canSeeAngle = (float)Math.Pow(1.0f - angleDiff / maxVisionAngle, 0.333f);
    float canSeeDistance = (float)Math.Pow(1.0f - dist / visionDistance, 0.333f);
    return canSeeAngle * canSeeDistance;
}


bool isPanicked() {
    return State.panic > 0;
}


        #endregion
    }
}
