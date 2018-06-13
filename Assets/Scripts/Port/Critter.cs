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


        #region State
        public float wary;
        public float panic;
        public bool hasLastKnownPosition;
        public Vector3 lastKnownPosition;

        public Item[] loot = new Item[Actor.MAX_INVENTORY_SIZE];
        #endregion



        new public CritterData Data { get { return GetData<CritterData>(); } }
        new public static CritterData GetData(string dataName) {
            return DataManager.GetData<CritterData>(dataName);
        }




        #region core


        public void init(CritterData data, World world) {
            base.init(data, world);
        }

        // TODO: move cameraYaw into the PlayerCmd struct
        public void Update(float dt) {

            canClimb = false;
            canClimbWell = false;
            canMove = true;
            canJump = true;
            canRun = true;
            canTurn = true;
            canAttack = true;

            if (stunned) {
                canRun = false;
                canJump = false;
                canClimb = false;
                canClimbWell = false;
                canAttack = false;
                canMove = false;
            }

            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] != null) {
                    inventory[i].updateCast(dt, this);
                }
            }

            Input_t input;
            updateBrain(dt, out input);

            base.Update(dt, input);

            if (canAttack) {
                foreach (var weapon in inventory) {
                    if (weapon != null) {
                        if (input.IsPressed(InputType.ATTACK_RIGHT)) {
                            weapon.charge(dt);
                        }
                        else {
                            if (input.inputs[(int)InputType.ATTACK_RIGHT] == InputState.JUST_RELEASED) {
                                weapon.attack(this);
                            }
                            weapon.chargeTime = 0;
                        }
                    }
                }
            }
            else {
                foreach (var weapon in inventory) {
                    if (weapon != null) {
                        weapon.chargeTime = 0;
                    }
                }
            }



            if (health <= 0) {
                removeFlag = true;

                foreach (var i in loot) {
                    if (i != null) {
                        i.position = position;
                        i.velocity = new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f), 18);
                        i.transform.parent = world.items.transform;
                    }
                }
            }
        }

        public void init() {
            removeFlag = false;

            canClimb = false;
            canClimbWell = false;
            canMove = true;
            canJump = true;
            canRun = true;
            canTurn = true;
            canAttack = true;
        }

        public void spawn(Vector3 pos) {
            spawned = true;
            position = pos;
            maxHealth = Data.maxHealth;
            health = maxHealth;
        }

        #endregion

        #region brain

        void updateBrain(float dt, out Input_t input) {
            input = new Input_t();

            //	foreach(var p in player.world.Players)
            {
                Player p = world.player;

                float awareness = (canSee(p) * Data.visionWeight + canSmell(p) * Data.smellWeight + canHear(p) * Data.hearingWeight) / (Data.visionWeight + Data.smellWeight + Data.hearingWeight);

                float waryIncrease = isPanicked() ? Data.waryIncreaseAtMaxAwarenessWhilePanicked : Data.waryIncreaseAtMaxAwareness;
                waryIncrease *= awareness;
                if (awareness > 0) {
                    float maxWary = 2f;
                    wary = Math.Min(maxWary, wary + dt * waryIncrease);
                    if (wary > 1.0f) {
                        hasLastKnownPosition = true;
                        lastKnownPosition = p.position;
                        panic = 1f;
                    }
                }
                else {
                    panic = Math.Max(0, panic - dt / Data.panicCooldownTime);
                    wary = Math.Max(0, wary - dt / Data.waryCooldownTime);

                }

            }

            input.yaw = yaw;
            if (isPanicked()) {
                if (Data.updatePanicked != null) {
                    Data.updatePanicked(this, dt, ref input);
                }
            }
            else {
                input.movement = Vector3.zero;
                if (hasLastKnownPosition) {
                    var diff = lastKnownPosition - position;
                    input.yaw = Mathf.Atan2(diff.x, diff.z);
                }
            }

            //float yawDiff = constrainAngle(input.yaw - yaw);
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
            //input.yaw = constrainAngle(yaw + turn);

        }

        public static void bounceAndFlee(Critter c, float dt, ref Input_t input) {

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
            if (c.canJump && c.activity == Activity.ONGROUND) {
                input.inputs[(int)InputType.JUMP] = InputState.JUST_PRESSED;
            }
        }

        public static void approachAndAttack(Critter c, float dt, ref Input_t input) {
            input.movement = Vector3.zero;
            input.inputs[(int)InputType.JUMP] = InputState.RELEASED;
            input.inputs[(int)InputType.ATTACK_RIGHT] = InputState.RELEASED;
            if (c.hasLastKnownPosition) {
                var diff = c.position - c.lastKnownPosition;

                if (diff.z <= -3) {
                    if (c.canJump && c.activity == Activity.ONGROUND) {
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
                    if (c.canAttack && c.activity == Activity.ONGROUND) {
                        input.inputs[(int)InputType.ATTACK_RIGHT] = InputState.JUST_RELEASED;
                    }
                }
                input.yaw = Mathf.Atan2(-diff.x, -diff.z);
            }
        }


        float canSmell(Player player) {
            float basicSmellDist = 1;

            var diff = position - player.position;
            float dist = diff.magnitude;
            if (dist == 0)
                return 1;

            float smell = 0;
            if (dist < basicSmellDist) {
                smell = Math.Max(0f, Mathf.Pow(1f - dist / basicSmellDist, 2));
            }

            float windCarryTime = 5f;
            var wind = world.getWind(player.position);
            float maxWindCarryDist = Mathf.Sqrt(wind.magnitude) * windCarryTime;
            float windCarrySmell = 0;
            if (dist < maxWindCarryDist && wind != Vector3.zero) {
                windCarrySmell = 1f - dist / maxWindCarryDist;
                float windCarryAngleDot = Vector3.Dot(wind.normalized, diff.normalized);
                windCarrySmell *= windCarryAngleDot;
            }

            return Math.Max(smell, windCarrySmell);
        }
        float canHear(Player player) {
            if (player.activity != Activity.ONGROUND)
                return 0;
            float playerSpeed = player.velocity.magnitude / player.Data.groundMaxSpeed;
            if (playerSpeed == 0)
                return 0;

            var diff = player.position - position;
            float distance = diff.magnitude;

            if (distance == 0)
                return 1f;

            float fullSpeedAudibleDistance = 30f;
            float playerSound = Mathf.Clamp(1f - (distance / (fullSpeedAudibleDistance * playerSpeed)), 0f, 1f);
            if (playerSound <= 0)
                return 0;

            return Mathf.Pow(playerSound, 0.25f);
        }
        float canSee(Player player) {
            var diff = player.position - position;
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

            float angleToPlayer = Mathf.Atan2(diff.x, diff.z);
            float angleDiff = Mathf.Abs(Mathf.Repeat(angleToPlayer - yaw, Mathf.PI * 2));
            float maxVisionAngle = Mathf.PI / 2;
            if (angleDiff > maxVisionAngle)
                return 0;

            float canSeeAngle = Mathf.Pow(1.0f - angleDiff / maxVisionAngle, 0.333f);
            float canSeeDistance = Mathf.Pow(1.0f - dist / visionDistance, 0.333f);
            return canSeeAngle * canSeeDistance;
        }


        bool isPanicked() {
            return panic > 0;
        }


        #endregion
    }
}
