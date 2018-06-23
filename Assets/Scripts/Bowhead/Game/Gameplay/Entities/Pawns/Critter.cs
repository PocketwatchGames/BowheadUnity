using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {


    public class Critter : Pawn<Critter, CritterData> {

		public override System.Type serverType => typeof(Critter);
		public override System.Type clientType => typeof(Critter);

        #region State
        public float wary;
        public float panic;
        public bool hasLastKnownPosition;
        public Vector3 lastKnownPosition;

        public Item[] loot = new Item[MaxInventorySize];
        public CritterBehavior behaviorPanic;

		#endregion

		#region core

		public override void ServerSpawn(Vector3 pos, EntityData baseData) {
			base.ServerSpawn(pos, baseData);
			AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), position, Quaternion.identity));
		    behaviorPanic = CritterBehavior.Create(data.panicBehavior);
			position = pos;
			maxHealth = data.maxHealth;
			health = maxHealth;
			team = gameMode.monsterTeam;
            Init();
			gameMode.CritterSpawned();
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			if (gameMode != null) {
				gameMode.CritterKilled();
			}
		}

		// TODO: move cameraYaw into the PlayerCmd struct
		public override void Tick() {
			base.Tick();
			if (!hasAuthority) {
				return;
			}

			var dt = world.deltaTime;
	
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

            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) != null) {
                    GetInventorySlot(i).UpdateCast(dt, this);
                }
            }

            Input_t input;
            UpdateBrain(dt, out input);

            base.Tick(dt, input);

            if (canAttack) {
                foreach (var weapon in getInventory()) {
                    Weapon w = weapon as Weapon;
                    if (w != null) {
                        if (input.IsPressed(InputType.AttackRight)) {
                            w.Charge(dt);
                        }
                        else {
                            if (input.inputs[(int)InputType.AttackRight] == InputState.JustReleased) {
                                w.Attack(this);
                            }
                            w.chargeTime = 0;
                        }
                    }
                }
            }
            else {
                foreach (var weapon in getInventory()) {
                    Weapon w = weapon as Weapon;
                    if (w != null) {
                        w.chargeTime = 0;
                    }
                }
            }

            if (health <= 0) {
                Die();
            }
        }

        public void Init() {
            canClimb = false;
            canClimbWell = false;
            canMove = true;
            canJump = true;
            canRun = true;
            canTurn = true;
            canAttack = true;
        }


        override protected void Die() {
            foreach (var i in loot) {
                if (i != null) {
                    var worldItem = gameMode.SpawnWorldItem(i, position);
                    worldItem.velocity = new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f), 18);
                }
            }
            Destroy();
        }

        #endregion

        #region brain

        void UpdateBrain(float dt, out Input_t input) {
            input = new Input_t();

			foreach (var p in world.GetActorIterator<Player>()) {

                float awareness = (CanSee(p) * data.visionWeight + CanSmell(p) * data.smellWeight + CanHear(p) * data.hearingWeight) / (data.visionWeight + data.smellWeight + data.hearingWeight);

                float waryIncrease = IsPanicked() ? data.waryIncreaseAtMaxAwarenessWhilePanicked : data.waryIncreaseAtMaxAwareness;
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
                    panic = Math.Max(0, panic - dt / data.panicCooldownTime);
                    wary = Math.Max(0, wary - dt / data.waryCooldownTime);

                }

            }

            input.yaw = yaw;
            if (IsPanicked()) {
                if (behaviorPanic != null) {
                    behaviorPanic.Tick(this, dt, ref input);
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

        float CanSmell(Player player) {
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
            var wind = gameMode.GetWind(player.position);
            float maxWindCarryDist = Mathf.Sqrt(wind.magnitude) * windCarryTime;
            float windCarrySmell = 0;
            if (dist < maxWindCarryDist && wind != Vector3.zero) {
                windCarrySmell = 1f - dist / maxWindCarryDist;
                float windCarryAngleDot = Vector3.Dot(wind.normalized, diff.normalized);
                windCarrySmell *= windCarryAngleDot;
            }

            return Math.Max(smell, windCarrySmell);
        }
        float CanHear(Player player) {
            if (player.activity != Activity.OnGround)
                return 0;
            float playerSpeed = player.velocity.magnitude / player.data.groundMaxSpeed;
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
        float CanSee(Player player) {
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


        bool IsPanicked() {
            return panic > 0;
        }


        #endregion
    }
}
