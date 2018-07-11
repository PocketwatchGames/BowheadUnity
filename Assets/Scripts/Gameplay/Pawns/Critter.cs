using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {


    public class Critter : Pawn<Critter, CritterData> {

		public override System.Type serverType => typeof(Critter);
		public override System.Type clientType => typeof(Critter);

		public Critter() {
			SetReplicates(true);
		}

        #region State
        public float decayTime;
        public float wary;
        public float panic;
        public bool hasLastKnownPosition;
        public Vector3 lastKnownPosition;

        public Item[] loot = new Item[MaxInventorySize];
        public CritterBehavior behaviorPanic;

		#endregion

		private PawnHUD _hud;

		public float canSmell, canSee, canHear;

		#region core

		public override void Spawn(EntityData d, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(d, pos, instigator, owner, team);
			behaviorPanic = CritterBehavior.Create(data.panicBehavior);
            AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity));
            position = pos;
            spawnPosition = pos;
			maxHealth = data.maxHealth;
			health = maxHealth;
			maxWater = data.maxWater;
			water = maxWater;
			this.team = team;
            active = false;
            canClimb = false;
            canClimbWell = false;
            canMove = true;
            canStrafe = true;
            canJump = true;
            canRun = true;
            canTurn = true;
            canAttack = true;
            gameMode.CritterSpawned();
		}

        public void SetActive(Vector3 pos) {
            active = true;
			position = pos;
		}

		public override void PostNetConstruct() {
			base.PostNetConstruct();
			if (GameManager.instance.clientWorld.gameState != null) {
				// hack get server instance of this critter
				var critter = (Critter)GameManager.instance.serverWorld.GetObjectByNetID(netID);
				GameManager.instance.clientWorld.OnCritterActive(critter);
			}
		}

		protected override void OnDestroy() {
			base.OnDestroy();
			if (gameMode != null) {
				gameMode.CritterKilled();
			}
		}

        override protected void Die() {
            base.Die();
            decayTime = 5;
            foreach (var i in loot) {
                if (i != null) {
                    var worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, position, this, null, team);
					worldItem.item = i;
                    worldItem.velocity = new Vector3(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.Range(-10f, 10f), 18);
                }
            }

        }
		
        public override void PreSimulate(float dt) {

            canClimb = false;
            canClimbWell = false;
            canMove = true;
            canStrafe = true;
            canJump = true;
			canRun = true;
			canSprint = true;
			canTurn = true;
            canAttack = true;

            if (stunned) {
                canRun = false;
				canSprint = false;
				canJump = false;
                canClimb = false;
                canClimbWell = false;
                canAttack = false;
                canMove = false;
            }


			base.PreSimulate(dt);


		}

		// TODO: move cameraYaw into the PlayerCmd struct
		override public void Tick() {
            base.Tick();
			if (!hasAuthority) {
				return;
			}

			// TODO: this doesn't need to be done *every* frame
			CheckDespawn(500);

			if (pendingKill) {
				return;
			}

            // hacky spawn
            if (!active) {
                if (!WorldUtils.GetFirstSolidBlockDown(1000, ref spawnPosition)) {
                    return;
                }
                SetActive(spawnPosition);
            }

        }

		void CheckDespawn(float maxDist) {
			float closestDist = 1000;
			foreach (var p in world.GetActorIterator<Player>()) {
				if (p.active) {
					var diff = position - p.position;
					diff.y = 0;
					closestDist = Mathf.Min(closestDist, diff.magnitude);
				}
			}
			if (closestDist > maxDist) {
				Destroy();
			}
		}


        public override void Simulate(float dt, Input_t input) {
            base.Simulate(dt, input);


            if (!alive) {
                decayTime -= Time.deltaTime;
                if (decayTime <= 0) {
                    Destroy();
                }
                return;
            }

            if (canAttack) {
                foreach (var weapon in getInventory()) {
                    Weapon w = weapon as Weapon;
                    if (w != null && w.CanCast()) {
						
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


        }

        #endregion

        #region brain

        override public Input_t GetInput(float dt) {

            if (driver != null) {
                return driver.GetInput(dt);
            }

            return GetInputFromAI(dt);

        }

        private Input_t GetInputFromAI(float dt) {
            Input_t input = new Input_t();
            foreach (var p in world.GetActorIterator<Player>()) {

                if (p.team == team) {
                    continue;
                }

				canSmell = CanSmell(p);
				canHear = CanHear(p);
				canSee = CanSee(p);

				float awareness = (canSee * data.visionWeight + canSmell * data.smellWeight + canHear * data.hearingWeight) / (data.visionWeight + data.smellWeight + data.hearingWeight);

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

            input.look = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
            if (IsPanicked()) {
                if (behaviorPanic != null) {
                    behaviorPanic.Tick(this, dt, ref input);
                }
            }
            else {
                input.movement = Vector3.zero;
                if (hasLastKnownPosition) {
                    var diff = lastKnownPosition - position;
                    input.look = diff.normalized;
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
            return input;
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

            float playerSound = Mathf.Clamp(1f - (distance / (data.hearingDistance * playerSpeed)), 0f, 1f);
            if (playerSound <= 0)
                return 0;

            return playerSound;
        }
        public float CanSee(Player player) {
            var diff = player.position - position;
            float dist = diff.magnitude;

            float visionDistance = data.nightVisionDistance;
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
			visionDistance += (data.dayVisionDistance - data.nightVisionDistance);

            if (dist > visionDistance)
                return 0;

            float angleToPlayer = Mathf.Atan2(diff.x, diff.z);
            float angleDiff = Mathf.Abs(Mathf.Repeat(angleToPlayer - yaw, Mathf.PI * 2));
			float angleRange = data.visionAngleRange * Mathf.Deg2Rad;

			if (angleDiff > angleRange)
                return 0;

            float canSeeAngle = Mathf.Pow(1.0f - angleDiff / angleRange, data.visionAngleExponent);
            float canSeeDistance = Mathf.Pow(1.0f - dist / visionDistance, data.visionDistanceExponent);
            return canSeeAngle * canSeeDistance;
        }


        bool IsPanicked() {
            return panic > 0;
        }


        #endregion
    }
}
