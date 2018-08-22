using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {
	
    public partial class Critter : Pawn<Critter, CritterData> {

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
		public float behaviorUpdateTimer;
		public CritterBehavior curBehavior;
		public List<CritterBehavior> behaviors = new List<CritterBehavior>();

		#endregion


		#region core

		SilhouetteRenderer.Mode _defaultSilhouetteMode;

		public override void Spawn(EntityData d, int index, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.Spawn(d, index, pos, yaw, instigator, owner, team);
			foreach (var b in data.behaviors) {
				behaviors.Add(b.Create(this));
			}
			_defaultSilhouetteMode = SilhouetteRenderer.Mode.Off;
            AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity));
            position = pos;
			this.yaw = yaw;
			spawnPosition = pos;
			maxHealth = data.maxHealth;
			health = maxHealth;
			this.team = team;
            active = false;
            canClimb = false;
            canMove = true;
            canJump = true;
            canRun = true;
            canTurn = true;
            canAttack = true;
			canStrafe = true;
			onHit += OnHit;
            gameMode.CritterSpawned();

			gameMode.onAudioEvent += OnAudioEvent;
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
				gameMode.onAudioEvent -= OnAudioEvent;
			}
		}

        override protected void Die() {
            base.Die();
            decayTime = 5;
            foreach (var i in loot) {
                if (i != null) {
                    var worldItem = WorldItemData.Get("Chest").Spawn<WorldItem>(world, position, yaw, this, null, team);
					worldItem.item = i;
                    worldItem.velocity = new Vector3(UnityEngine.Random.Range(-5f, 5f), 12, UnityEngine.Random.Range(-5f, 5f));
                }
            }

        }
		
        public override void PreSimulate(float dt) {

            canClimb = false;
            canMove = true;
            canJump = true;
			canRun = true;
			canSprint = true;
			canTurn = true;
            canAttack = true;
			canStrafe = true;

			if (stunned) {
				canRun = false;
				canSprint = false;
				canJump = false;
				canClimb = false;
				canAttack = false;
				canMove = false;
				canTurn = false;
			}

			if (moveImpulseTimer > 0)
            {
                canAttack = false;
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
			CheckDespawn(300);

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

			UpdateSilhouette();

			var head = go.GetChildComponent<MeshRenderer>("Head");
			if (head != null) {
				if (stunned) {
					head.material.color = Color.yellow;
				} else if (stunInvincibilityTimer > 0) {
					head.material.color = Color.gray;
				} else if (dodgeTimer > 0) {
					head.material.color = Color.black;
				} else if (skidding) {
					head.material.color = Color.cyan;
				} else {
					head.material.color = Color.white;
				}
			}


		}

		void UpdateSilhouette() {
			if (driver != null) {
				return;
			}
			if (data.defaultSilhouetteMode == SilhouetteRenderer.Mode.Off) {
				return;
			}

			if (wary > 0 || IsPanicked()) {
				if (_defaultSilhouetteMode != SilhouetteRenderer.Mode.On) {
					_defaultSilhouetteMode = SilhouetteRenderer.Mode.On;
					silhouetteMode = SilhouetteRenderer.Mode.On;
				}
				return;
			}

			var checkDist = data.silhouetteDistanceThreadholdSq;
			if (checkDist <= 0) {
				return;
			}

			float closest = float.MaxValue;

			foreach (var player in world.GetActorIterator<Player>()) {
				var d = GetSqDistanceTo(player);
				closest = Mathf.Min(d, closest);
			}

			var mode = SilhouetteRenderer.Mode.Off;

			if (closest < data.silhouetteDistanceThreadholdSq) {
				mode = data.defaultSilhouetteMode;
			}

			if (mode != _defaultSilhouetteMode) {
				_defaultSilhouetteMode = mode;
				silhouetteMode = mode;
			}
		}

		protected override SilhouetteRenderer.Mode defaultSilhouetteMode => _defaultSilhouetteMode;

		void CheckDespawn(float maxDist) {
			float closestDist = maxDist;
			foreach (var p in world.GetActorIterator<Player>()) {
				if (p.active) {
					var diff = position - p.position;
					diff.y = 0;
					closestDist = Mathf.Min(closestDist, diff.magnitude);
				}
			}
			if (closestDist >= maxDist) {
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
                            w.Charge(dt, 1);
                        }
                        else {
                            if (input.JustReleased(InputType.AttackRight)) {
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

		private void SetWary(float w, Pawn player) {
			wary = Math.Min(data.investigateLimit, w);
			if (wary > data.waryLimit) {
				hasLastKnownPosition = true;
				lastKnownPosition = player.position;
			}
			if (wary >= data.investigateLimit) {
				panic = 1f;
			}
		}

		private Input_t GetInputFromAI(float dt) {

            Input_t input = new Input_t();
			input.look = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));

			if (team == gameMode.teams[0]) {
				return input;
			}

			UpdateAggro(dt);

			if (curBehavior != null && !curBehavior.IsValid()) {
				behaviorUpdateTimer = 0;
			}

			behaviorUpdateTimer -= dt;
			if (behaviorUpdateTimer <= 0) {
				behaviorUpdateTimer += data.behaviorUpdateTime;

				// Evaluate behaviors
				List<CritterBehavior.EvaluationScore> possibleBehaviors = new List<CritterBehavior.EvaluationScore>();
				float totalScore = 0;
				foreach (var b in behaviors) {
					var score = b.Evaluate();
					if (score.score > 0) {
						possibleBehaviors.Add(score);
						totalScore += score.score;
					}
				}

				// Choose a behavior
				if (possibleBehaviors.Count > 0) {
					float chooseScore = GameManager.instance.randomNumber * totalScore;
					foreach (var b in possibleBehaviors) {
						chooseScore -= b.score;
						if (chooseScore <= 0) {
							if (curBehavior != b.behavior) {
								b.behavior.Start();
								curBehavior = b.behavior;
							}
							break;
						}
					}
				}
			}

			// Execute the behavior
			if (curBehavior != null) {
				curBehavior.Tick(dt, ref input);
			}

            return input;
        }

		protected virtual void UpdateAggro(float dt) {
			UpdateAggro<Player>(dt);
		}

		protected void UpdateAggro<T>(float dt) where T: Pawn {
			foreach (var p in world.GetActorIterator<T>()) {

				if (p.team == team) {
					continue;
				}

				float awareness = CanSee(p);

				float waryIncrease = IsPanicked() ? data.waryIncreaseAtMaxAwarenessWhilePanicked : data.waryIncreaseAtMaxAwareness;
				waryIncrease *= awareness;
				if (awareness > 0) {
					SetWary(wary + dt * waryIncrease, p);
				} else {
					panic = Math.Max(0, panic - dt / data.panicCooldownTime);
					wary = Math.Max(0, wary - dt / data.waryCooldownTime);

				}

			}
		}

		protected virtual void OnAudioEvent(Pawn origin, float loudness) {

			Player player = origin as Player;
			if (player == null) {
				return;
			}

			var playerBlock = gameMode.GetTerrainData(player.position + new Vector3(0, -0.1f, 0));

			var diff = origin.position - position;
			float distance = diff.magnitude;

			float playerSound = loudness * Mathf.Pow(Mathf.Clamp01(1f - (distance / data.hearingDistance)), data.hearingDistanceExponent);

			SetWary(wary + playerSound, origin);
		}

		protected virtual void OnHit(Pawn attacker) {
			SetWary(data.investigateLimit, attacker);
			panic = 1;
		}

        public float CanSee(Pawn player) {
            var diff = player.position - position;
            float dist = diff.magnitude;

			float light = 0.5f;
            float visionDistance = (data.dayVisionDistance - data.nightVisionDistance) * light + data.nightVisionDistance;

            if (dist > visionDistance)
                return 0;

            float angleToPlayer = Mathf.Atan2(diff.x, diff.z);
            float angleDiffXZ = Mathf.Abs(Utils.SignedMinAngleDelta(angleToPlayer * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg));

			if (angleDiffXZ > data.visionAngleRange)
                return 0;

			float angleDeltaXZ = angleDiffXZ / data.visionAngleRange;

			float angleDiffY = Mathf.Atan2(diff.y, Mathf.Sqrt(diff.x*diff.x+diff.z*diff.z));
			float angleDeltaY = 0;
			if (angleDiffY > 0) {
				if (angleDiffY > data.visionAngleRangeUp * Mathf.Deg2Rad) {
					return 0;
				}
				angleDeltaY = angleDiffY / (data.visionAngleRangeUp * Mathf.Deg2Rad);
			}
			else if (angleDiffY < 0) {
				if (angleDiffY < -data.visionAngleRangeDown * Mathf.Deg2Rad) {
					return 0;
				}
				angleDeltaY = -angleDiffY / (data.visionAngleRangeDown * Mathf.Deg2Rad);
			}
			bool hasClearLine = !Physics.Raycast(new Ray(headPosition(), (player.headPosition()-headPosition()).normalized), dist, Layers.PawnCollisionMask) || !Physics.Raycast(new Ray(headPosition(), (player.footPosition() - headPosition()).normalized), dist, Layers.PawnCollisionMask);
			if (!hasClearLine) {
				return 0;
			}

			float angleDelta = Mathf.Sqrt(angleDeltaXZ * angleDeltaXZ + angleDeltaY * angleDeltaY);
			float canSeeAngle = Mathf.Pow(1.0f - angleDelta, data.visionAngleExponent);
			float canSeeDistance = Mathf.Pow(1.0f - dist / visionDistance, data.visionDistanceExponent);
			float playerSpeed = (player.velocity != Vector3.zero) ? 1 : 0;

			return canSeeAngle * canSeeDistance * (1.0f - player.stealthBonusSight) * (playerSpeed * data.visionMotionWeight + (1.0f-data.visionMotionWeight));
        }


        bool IsPanicked() {
            return panic > 0;
        }


        #endregion
    }
}
