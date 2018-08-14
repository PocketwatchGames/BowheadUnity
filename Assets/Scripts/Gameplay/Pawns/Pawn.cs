using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    public enum InputState {
        Released,
        JustReleased,
        JustPressed,
        Pressed,
    }
    public enum InputType {
        Jump,
		Interact,
        AttackLeft,
		AttackRight,
		AttackRangedLeft,
		AttackRangedRight,
		Count
	}

	public abstract class Pawn<T, D> : Pawn where T : Pawn<T, D> where D : PawnData {
		public override void Spawn(EntityData data, int index, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.Spawn(data, index, pos, yaw, instigator, owner, team);
			this.data = (D)data;
		}
		
		new public D data {
			get;
			private set;
		}
	}

    public abstract class Pawn : Entity {

        #region State

        [Header("Basic")]
        public bool active;
        public Team team;

        [Header("Input")]
        public PlayerCmd_t cur;
        public PlayerCmd_t last;

		[Header("Physics")]
        public Vector3 spawnPosition;
        public Rigidbody rigidBody;
        public Vector3 velocity;
        public float yaw;
        public Vector3 moveImpulse;
        public float moveImpulseTimer;
        public Vector3 groundNormal;

        [Header("Stats")]
        public bool alive = true;
        public float health;
        public float maxHealth;
		public float water;
		public float maxWater;
		public float stamina;
        public float maxStamina;
        public bool stunned;
        public float staminaRecoveryTimer;
		public float stunInvulnerabilityTimer;
        public float dodgeTimer;
		public float damageMultiplier;
		public float resistPoison;
		public float resistFire;
		public float resistCold;

		[Header("Gameplay")]
        public Activity activity;
		public Vector3 climbingNormal;
		public Vector3 climbingAttachPoint;
		public float climbingAttachCooldown;
        public float fallJumpTimer;
        public float maxHorizontalSpeed;
		public float sprintTimer;
		public float sprintGracePeriodTime;
		public bool skidding;
        public bool canMove;
        public bool canRun;
        public bool canJump;
		public bool canClimb;
		public bool canSprint;
        public bool canSwim;
        public bool canTurn;
        public bool canAttack;
		public bool canStrafe;
		public bool[] canClimbType;
		public float stealthBonusSound;
		public float stealthBonusSight;
		public float stealthBonusSmell;
		public Pawn mount;
        public Pawn driver;
		public List<StatusEffect> statusEffects = new List<StatusEffect>();

        [Header("Inventory")]
        [SerializeField]
        protected Item[] _inventory = new Item[Pawn.MaxInventorySize];

		protected event Action<Pawn> onHit;

		Client.UI.IMapMarker _marker;
		SilhouetteRenderer _silhouetteRenderer;
        #endregion


		public Vector3 position { get { return rigidBody.position; } set { rigidBody.position = value; } }
        
        public const int MaxInventorySize = 32;

		public enum Activity {
			Falling,
			Swimming,
			Climbing,
			OnGround,
		}

		public enum Stance {
			Combat,
			Explore,
		}


		public struct PlayerCmd_t {
            public int serverTime;
            public int buttons;
            public sbyte fwd, right, up;
			public sbyte lookFwd, lookRight;
        };

        public class Input_t {
            public Vector3 movement = Vector3.zero;
            public Vector3 look = Vector3.zero;
            public InputState[] inputs = new InputState[(int)InputType.Count];

			public bool IsPressed(InputType i) {
				return inputs[(int)i] == InputState.Pressed || inputs[(int)i] == InputState.JustPressed;
			}
			public bool JustPressed(InputType i) {
				return inputs[(int)i] == InputState.JustPressed;
			}
			public bool JustReleased(InputType i) {
				return inputs[(int)i] == InputState.JustReleased;
			}
			public bool inMotion;
            public Vector3 position;
            public Vector3 velocity;
        };

		// This field is a HACK and is null on clients
		public Server.BowheadGame gameMode {
			get;
			private set;
		}

		public virtual void Spawn(EntityData data, int index, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.ConstructEntity(data);
			this.data = (PawnData)data;
			gameMode = (Server.BowheadGame)((Server.ServerWorld)world).gameMode;
			damageMultiplier = 1;
			canClimbType = new bool[(int)WorldData.ClimbingType.Count];
		}
	
		#region getdata
		new public PawnData data {
			get;
			private set;
		}
        #endregion

        protected override void OnGameObjectAttached() {
            base.OnGameObjectAttached();

            rigidBody = go.GetComponent<Rigidbody>();
			_silhouetteRenderer = go.GetComponent<SilhouetteRenderer>();
			silhouetteMode = defaultSilhouetteMode;
        }

        public void UpdatePlayerCmd(PlayerCmd_t cmd) {
            last = cur;
            cur = cmd;
        }

        public bool CanMoveTo(Vector3 moveVector, bool allowFall, ref Vector3 position) {

            Vector3 move = moveVector;

            {
                Vector3 movePosition = position + moveVector;
                Vector3 footPoint = footPosition(movePosition);
                Vector3 headPoint = headPosition(movePosition);

                RaycastHit hit;
                if (Physics.Raycast(movePosition + new Vector3(0,0.25f,0),Vector3.down,out hit,activity == Activity.OnGround ? 0.5f : 0.25f, Layers.PawnCollisionMask)) {
                    move.y = hit.point.y - position.y;
                }
                else if (!allowFall) {
                    return false;
                }

                // Step forward
                if (!Physics.Raycast(footPosition() + new Vector3(0, 0.25f, 0), moveVector.normalized, out hit, moveVector.magnitude, Layers.PawnCollisionMask)
                    && !Physics.Raycast(waistPosition(), moveVector.normalized, out hit, moveVector.magnitude, Layers.PawnCollisionMask)
                    && !Physics.Raycast(headPosition(), moveVector.normalized, out hit, moveVector.magnitude, Layers.PawnCollisionMask)) {
                    position += move;
                    return true;
                }
            }

            return false;
        }

        bool IsClimbPosition(Vector3 p, Vector3 wallDirection) {
            var handHoldPos = p + wallDirection.normalized * data.climbWallRange;
            if (!IsOpen(p))
                return false;
            var handblock = gameMode.GetTerrainData(handPosition(handHoldPos));
            bool isClimbable = canClimbType[(int)handblock.climbType];
            if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
				if (isHangPosition && handblock.canHang && !WorldUtils.IsSolidBlock(world.GetBlock(p)) && !WorldUtils.IsSolidBlock(world.GetBlock(handPosition(p))) && !WorldUtils.IsSolidBlock(world.GetBlock(handPosition(handHoldPos) + Vector3.up))) {
                    isClimbable = true;
                }
            }
            if (velocity.y > data.climbGrabMinZVel && isClimbable) {
                return true;
            }
            return false;
        }

        bool CanClimb(Vector3 checkDir, Vector3 checkPos) {
            var p = checkPos;
            var handHoldPos = handPosition(p) + checkDir * data.climbWallRange;
            if (!IsOpen(p))
                return false;
			var handblock = gameMode.GetTerrainData(handHoldPos);
			bool isClimbable = canClimbType[(int)handblock.climbType];
			if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
                if (isHangPosition
                    && handblock.canHang
					&& !WorldUtils.IsSolidBlock(world.GetBlock(handHoldPos + Vector3.up))) {
                    isClimbable = true;
                }
            }

			if (isClimbable) {
				if (velocity.y > data.climbGrabMinZVel) {
					return true;
				}
			}
			return false;
        }

        Vector3 getClimbingVector(Vector3 i, Vector3 surfaceNormal) {
            var axis = Vector3.Cross(Vector3.up, surfaceNormal);            
            var climbingVector = Quaternion.AngleAxis(90, axis) * i;

            // If we're climbing nearly exaclty up, change it to up
            if (Math.Abs(climbingVector.y) > Math.Sqrt(Math.Pow(climbingVector.x, 2) + Math.Pow(climbingVector.z, 2))) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0, Math.Sign(climbingVector.y) * inputSpeed, 0);
            }
            else if (Math.Abs(climbingVector.x) > Math.Abs(climbingVector.z)) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(Utils.SignOrZero(climbingVector.x), 0f, 0f) * inputSpeed;
            }
            else {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0f, 0f, Utils.SignOrZero(climbingVector.z)) * inputSpeed;
            }
            return climbingVector;
        }

        bool CheckFloor(Vector3 position, out float floorHeight, out Vector3 groundNormal) {
            floorHeight = 0;

            float checkDist = activity == Activity.OnGround ? data.height + 0.25f : data.height;

            RaycastHit hit;
            if (Physics.Raycast(headPosition(), Vector3.down, out hit, checkDist, Layers.PawnCollisionMask)) {
                floorHeight = hit.point.y;
                groundNormal = hit.normal;
                return true;
            }

            groundNormal = Vector3.up;
            return false;
        }

        bool IsOpen(Vector3 position) {
            return !WorldUtils.IsSolidBlock(world.GetBlock(waistPosition()))
                && !WorldUtils.IsSolidBlock(world.GetBlock(headPosition()));
        }

        virtual public Input_t GetInput(float dt) { return new Input_t(); }

        public override void Tick() {
            base.Tick();

            if (!hasAuthority) {
                return;
            }

            if (active) {

				if (!world.IsBlockLoaded(footPosition())) {
					return;
				}

				float dt = world.deltaTime;

				PreSimulate(dt);

				Input_t input = GetInput(dt);

				Simulate(dt, input);

				if ((_marker == null) && ((data.mapMarker != null) && (data.mapMarker.Load() != null))) {

					// this is just horrible, normally we'd have the gamestate on the client from the World object
					// but this is all server code... so fuck it dude let's go bowling.
					if (GameManager.instance.clientWorld.gameState != null) {
						_marker = AddGC(GameManager.instance.clientWorld.gameState.hud.CreateMapMarker(data.mapMarker.Load(), data.mapMarkerStyle));
						// SetPosition will set the position
						_marker.worldPosition = new Vector2(position.x, position.z);
					}
				}
			}
        }

        virtual public void PreSimulate(float dt) {

			foreach (var e in statusEffects) {
				e.Tick(dt, this);
			}
			statusEffects.RemoveAll(e => e.time <= 0);

            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) != null) {
                    GetInventorySlot(i).Tick(dt, this);
                }
            }
        }

        virtual public void Simulate(float dt, Input_t input) {
            if (!alive) {
				go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 180));
                return;
            }

            if (staminaRecoveryTimer > 0) {
                staminaRecoveryTimer = Math.Max(0, staminaRecoveryTimer - dt);
            }
            else {
                if (stamina < maxStamina) {
					if (stunned) {
						stamina = Math.Min(maxStamina, stamina + dt * maxStamina / data.staminaRechargeTimeDuringStun);
					}
					else {
						stamina = Math.Min(maxStamina, stamina + dt * maxStamina / data.staminaRechargeTime);
					}
				}
                else {
					if (stunned) {
						stunInvulnerabilityTimer = data.postStunInvincibilityTime;
					}
                    stunned = false;
                }
            }

			if (dodgeTimer > 0) {
				dodgeTimer = Math.Max(0, dodgeTimer - dt);
			}

			if (stunInvulnerabilityTimer > 0) {
				stunInvulnerabilityTimer = Math.Max(0, stunInvulnerabilityTimer - dt);
			}


			if (climbingAttachCooldown > 0) {
				climbingAttachCooldown = Mathf.Max(0, climbingAttachCooldown-dt);
			}

            if (canTurn) {
				if (activity == Activity.Climbing) {
					yaw = Mathf.Atan2(-climbingNormal.x, -climbingNormal.z);
				} else if (sprintTimer > data.sprintTime) {
					yaw = Mathf.Atan2(input.movement.x, input.movement.z);
				} else if (input.look != Vector3.zero) {
					yaw = Mathf.Atan2(input.look.x, input.look.z);
				}
			}


            if (mount != null) {
                return;
            }
            else if (activity == Activity.Climbing) {
                UpdateClimbing(dt, input);
            }
            else if (activity == Activity.Swimming) {
                UpdateSwimming(dt, input);
            }
            else if (activity == Activity.OnGround) {
                UpdateGround(dt, input);
            }
            else if (activity == Activity.Falling) {
                UpdateFalling(dt, input);
            }


			if (activity != Activity.Climbing) {
				SetPosition(new Vector3(position.x, position.y + velocity.y * dt, position.z));
			}

            // Collide feet
            float floorPosition;
            bool onGround = CheckFloor(position, out floorPosition, out groundNormal);
            if (onGround) {
                if (velocity.y <= 0) {

                    if (activity != Activity.OnGround) {
                        LandOnGround();
                        if (velocity.y < 0) {
                            float slopeAccel = Vector3.Dot(velocity.normalized, -groundNormal);
                            if (slopeAccel > 0) {
								var blockData = gameMode.GetTerrainData(new Vector3(position.x, floorPosition, position.z));
								var slopeRight = Vector3.Cross(velocity.normalized, groundNormal);
								var slopeDown = Vector3.Cross(groundNormal, slopeRight);
								var slideVelocity = slopeDown * slopeAccel * velocity.magnitude;
								slideVelocity.y = Math.Max(0, velocity.y);

								velocity = slideVelocity;
								skidding = true;
                            }
                        }
                    }
                    velocity.y = Math.Max(0, velocity.y);
                }
                SetPosition(new Vector3(position.x, floorPosition, position.z));
            }
            // Collide head
            else if (WorldUtils.IsSolidBlock(world.GetBlock(headPosition()))) {
                // TODO: this is broken
                SetPosition(new Vector3(position.x, Math.Min(position.y, (int)headPosition().y - data.height), position.z));
                velocity.y = Math.Min(0, velocity.y);
            }


            if (activity != Activity.Climbing) {
                // Collide XY
                Vector3 moveXZ = new Vector3(velocity.x, 0, velocity.z) * dt;
                Move(moveXZ);
            }

            Vector3 firstCheck, secondCheck;
            if (Math.Abs(input.movement.x) > Math.Abs(input.movement.z)) {
                firstCheck = new Vector3(Utils.SignOrZero(input.movement.x), 0, 0);
                secondCheck = new Vector3(0, 0, Utils.SignOrZero(input.movement.z));
            }
            else {
                firstCheck = new Vector3(0, 0, Utils.SignOrZero(input.movement.z));
                secondCheck = new Vector3(Utils.SignOrZero(input.movement.x), 0, 0);
            }
            if (activity == Activity.Climbing) {
                if (!canClimb) {
					if (activity != Activity.Falling) {
						climbingAttachCooldown = data.climbAttachCooldown;
					}
					SetActivity(Activity.Falling);
				}
			}
            else if (world.GetBlock(position) == EVoxelBlockType.Water) {
				SetActivity(Activity.Swimming);
				if (maxWater > 0) {
					AddStatusEffect(StatusEffectData.Get("RefillWater"), 5);
				}
			}
			else if (onGround && velocity.y <= 0) {
				SetActivity(Activity.OnGround);

				//if (Crouched)
				//{
				//	bool interpolate = false;
				//	Vector3 climbDownPos = position + Vector3(0, 0, -2f);
				//	Vector3 wallNormal;
				//	if (canClimb(-firstCheck, climbDownPos + firstCheck * data.WallJumpRange / 2)) {
				//		interpolate = true;
				//		climbDownPos += firstCheck * data.WallJumpRange / 2;
				//		wallNormal = firstCheck;
				//	}
				//	else if (canClimb(-secondCheck, climbDownPos + secondCheck * data.WallJumpRange / 2)) {
				//		interpolate = true;
				//		climbDownPos += secondCheck * data.WallJumpRange / 2;
				//		wallNormal = firstCheck;
				//	}
				//	if (interpolate)
				//	{
				//		physics = PhysicsState::CLIMBING;
				//		setPosition(climbDownPos, 0.1f);
				//		climbingNormal = Vector3(Mathf.Sign(wallNormal.x), Mathf.Sign(wallNormal.y), 0);
				//		velocity = Vector3.zero;
				//		if (climbingNormal.x != 0)
				//			velocity.y = 0;
				//		if (climbingNormal.y != 0)
				//			velocity.x = 0;
				//	}
				//}

			}
			else {
				if (activity != Activity.Falling) {
					climbingAttachCooldown = data.climbAttachCooldown;
				}
				SetActivity(Activity.Falling);

				if (canClimb && climbingAttachCooldown <= 0) {
                    if (firstCheck.magnitude > 0 && CanClimb(firstCheck, position)) {
						climbingAttachPoint = position;
						climbingNormal = -firstCheck;
                        velocity = Vector3.zero;
						SetActivity(Activity.Climbing);
					}
					else if (secondCheck.magnitude > 0 && CanClimb(secondCheck, position)) {
						climbingAttachPoint = position;
						climbingNormal = -secondCheck;
                        velocity = Vector3.zero;
						SetActivity(Activity.Climbing);
					}
				}

            }

            if (position.y < -1000 || health <= 0) {
                Die();
            }

			if (mount == null) {
				go.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
				driver?.MountMoved();
			}

			if (!world.IsBlockLoaded(footPosition())) {
				velocity = Vector3.zero;
				SetPosition(spawnPosition);
			}
        }

		virtual protected void SetActivity(Activity a) {
			activity = a;
		}

		protected virtual void MountMoved() {
			if (_marker != null) {
				_marker.worldPosition = new Vector2(mount.position.x, mount.position.z);
			}
		}

        public bool Move(Vector3 moveXZ) {
            float moveXZLength = moveXZ.magnitude;
            if (moveXZLength > 0) {
                Vector3 newPosition = position;
                Vector3 firstCheck, secondCheck;
                if (Math.Abs(moveXZ.x) > Math.Abs(moveXZ.z)) {
                    firstCheck = new Vector3(moveXZ.x, 0, 0);
                    secondCheck = new Vector3(0, 0, moveXZ.z);
                }
                else {
                    firstCheck = new Vector3(0, 0, moveXZ.z);
                    secondCheck = new Vector3(moveXZ.x, 0, 0);
                }

                Vector3 firstClimbDownPos = newPosition + firstCheck + new Vector3(0, -1.05f, 0);
                Vector3 secondClimbDownPos = newPosition + secondCheck + new Vector3(0, -1.05f, 0);

                if (CanMoveTo(moveXZ, true, ref newPosition)) {
                    SetPosition(newPosition);
                    return true;
                }
                else if (CanMoveTo(firstCheck, true, ref newPosition)) {
                    SetPosition(newPosition);
                    return true;
                }
                else if (CanMoveTo(secondCheck, true, ref newPosition)) {
                    SetPosition(newPosition);
                    return true;
                }

            }
            return false;

        }

        private void UpdateFalling(float dt, Input_t input) {
			skidding = false;

			if (fallJumpTimer > 0) {
                fallJumpTimer = Math.Max(0, fallJumpTimer - dt);

				if (input.IsPressed(InputType.Jump)) {
					sprintTimer = sprintTimer += dt;
				} else if (sprintTimer > 0 && sprintTimer < data.sprintTime) {
                    if (canJump) {
                        var jumpDir = input.movement * data.jumpHorizontalSpeed;
                        jumpDir.y += getGroundJumpVelocity();
                        Jump(jumpDir);
                    }
                    fallJumpTimer = 0;
					sprintTimer = 0;
					sprintGracePeriodTime = 0;
				}
			} else {
				sprintTimer = 0;
				sprintGracePeriodTime = 0;
			}

			velocity.y += data.gravity * dt;

            if (input.movement != Vector3.zero) {
                float acceleration = data.fallAcceleration;
                float maxSpeed = Math.Max(maxHorizontalSpeed, data.fallMaxHorizontalSpeed);

                var normalizedVelocity = velocity / maxSpeed;
                normalizedVelocity.y = 0;
                var normalizedInput = input.movement.normalized;
                float dot = Vector3.Dot(normalizedInput, normalizedVelocity);
                float accelerationPotential = Math.Min(1.0f, 1.0f - dot);
                velocity += input.movement * acceleration * accelerationPotential * dt;

            }

            // For parachutes and such
            float airFriction = getFallingAirFriction();
            velocity.y -= velocity.y * dt * airFriction;

            var wind = gameMode.GetWind(position);
            if (wind.magnitude >= gameMode.data.windSpeedStormy) {
                var velDiff = (wind - new Vector3(velocity.x, 0, velocity.z));
                velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
                velocity += velDiff * dt * getHorizontalAirFriction();
            }

        }

		private void UpdateGround(float dt, Input_t input) {

			bool jumped = false;
			if (canJump) {

				if (input.IsPressed(InputType.Jump)) {
					sprintTimer = sprintTimer += dt;
					if (sprintTimer > data.sprintTime) {
						sprintGracePeriodTime = data.sprintGracePeriodTime;
					}
					if (input.JustPressed(InputType.Jump)) {
						dodgeTimer = dodgeTimer + data.dodgeTime;
					}
				} else {
					if (sprintTimer > 0 && sprintTimer < data.sprintTime) {
						if (canJump) {
							var jumpDir = input.movement * data.jumpHorizontalSpeed;
							jumpDir.y += getGroundJumpVelocity();
							Jump(jumpDir);
							jumped = true;
						}
						fallJumpTimer = 0;
					}
					sprintTimer = 0;
					sprintGracePeriodTime = Mathf.Max(0, sprintGracePeriodTime - dt);
				}
			} else {
				sprintTimer = 0;
				sprintGracePeriodTime = 0;
				dodgeTimer = 0;
			}

			if (!jumped) {
				fallJumpTimer = data.fallJumpTime;
			}


			var block = gameMode.GetTerrainData(position + new Vector3(0, -0.1f, 0));
			var midblock = gameMode.GetTerrainData(position + new Vector3(0, 0.9f, 0));

			//if (IsWading()) {
			//             drag += data.swimDragHorizontal;
			//         }

			float curVel = velocity.magnitude;

			// Stopping (only when not sliding)
			if (!canMove) {
				velocity = Vector3.zero;
				skidding = false;
			} else if (input.movement == Vector3.zero && !skidding && curVel < data.walkSpeed * block.speedModifier) {
				velocity = Vector3.zero;
				skidding = false;
			} else {
				Vector3 acceleration = Vector3.zero;

				float maxSpeed = getGroundMaxSpeed() * block.speedModifier;
				float slideThreshold = block.slideThreshold;
				if (input.movement != Vector3.zero) {
					var normalizedInput = input.movement.normalized;
					float slopeDot = Vector3.Dot(groundNormal, normalizedInput);
					maxSpeed *= 1f + slopeDot * Mathf.Abs(slopeDot);
					//slideThreshold *= Mathf.Min(1.0f, 1.0f - slopeDot);
				}


				// Sliding
				if (skidding) {
					// Slide down any hills
					Vector3 slideRight = Vector3.Cross(Vector3.up, groundNormal);
					Vector3 slideDir = Vector3.Cross(slideRight, Vector3.up);
					acceleration += -data.gravity * groundNormal.y * slideDir * (1f - block.slideFriction);
				}


				Vector3 desiredVel = input.movement * maxSpeed;

				// running acceleration scales with our difference from our maxspeed
				var desiredAcceleration = desiredVel - velocity;
				desiredAcceleration.y = 0;

				if (desiredAcceleration != Vector3.zero) {

					desiredAcceleration *= getGroundAcceleration() * block.accelerationModifier;

					if (desiredVel.magnitude <= data.walkSpeed && !skidding) {
						// walk acceleration is linear
						desiredAcceleration = desiredAcceleration.normalized * Mathf.Min(slideThreshold, data.walkSpeed * block.accelerationModifier / data.walkStartTime);
					} else if (desiredAcceleration.magnitude * data.slideModifier > slideThreshold) {
						// if we're sprinting and we change direction quickly, start sliding
						if (velocity.magnitude > data.groundMaxSpeed * block.speedModifier) {
							skidding = true;
						}
					} else {
						skidding = false;
					}
					if (skidding) {
						desiredAcceleration *= block.slideFriction;
					}


					acceleration += desiredAcceleration;
				}

				velocity += acceleration * dt;
			}

			if (moveImpulseTimer > 0) {
				var m = moveImpulse * (dt / moveImpulseTimer);
				velocity = m / dt * block.speedModifier;
				moveImpulse -= m;
				moveImpulseTimer = Math.Max(0, moveImpulseTimer - dt);
				if (moveImpulseTimer <= 0) {
					velocity = Vector3.zero;
				}
			}

			//{
			//    // Wind blowing you while running or skidding
			//    var wind = gameMode.GetWind(position);
			//    if (wind.magnitude >= gameMode.data.windSpeedStormy) {
			//        var velDiff = (wind - new Vector3(velocity.x, 0, velocity.z));
			//        velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
			//        velChange += velDiff * dt * getHorizontalAirFriction();
			//    }
			//}



			Vector2 horizontalVel = new Vector2(velocity.x, velocity.z);

			if (sprintGracePeriodTime > 0) {
				maxHorizontalSpeed = Mathf.Max(maxHorizontalSpeed, horizontalVel.magnitude);
			} else {
				maxHorizontalSpeed = horizontalVel.magnitude;
			}

			if (data.sprintTime > 0 && sprintTimer >= data.sprintTime && input.movement != Vector3.zero) {
				UseStamina(data.sprintStaminaUse * dt, false);
			}
		}

		private void UpdateSwimming(float dt, Input_t input) {

			sprintTimer = 0;
			sprintGracePeriodTime = 0;
			skidding = false;

			if (input.inputs[(int)InputType.Jump] == InputState.JustReleased) {
				if (canJump) {
					var jumpDir = input.movement * data.jumpHorizontalSpeed;
					jumpDir.y += data.swimJumpSpeed;
					Jump(jumpDir);
				}
			}
			velocity.y += data.gravity * dt;
            if (world.GetBlock(headPosition()) == EVoxelBlockType.Water) {
                velocity.y += -velocity.y * dt * data.swimDragVertical;
                velocity.y += data.bouyancy * dt;
            }
			if (world.GetBlock(waistPosition()) == EVoxelBlockType.Water) {
				velocity.y += -velocity.y * dt * data.swimDragVertical;
				velocity.y += data.bouyancy * dt;
			}
			if (world.GetBlock(footPosition()) == EVoxelBlockType.Water) {
				velocity.y += -velocity.y * dt * data.swimDragVertical;
				velocity.y += data.bouyancy * dt;
			}

			var current = gameMode.GetCurrent((int)position.x, (int)position.y, (int)position.z);
            var velDiff = (current - new Vector3(velocity.x, 0, velocity.z));
            velDiff *= velDiff.magnitude; // drag is exponential, thanks zeno!
            velocity += velDiff * dt * data.swimDragHorizontal;

            if (input.movement == Vector3.zero) {
            }
            else {

                var normalizedInput = input.movement.normalized;
                var normalizedVelocity = velocity / data.swimMaxSpeed;
                float dot = Vector3.Dot(normalizedInput, normalizedVelocity);
                float accelerationPotential = Math.Min(1.0f, 1.0f - dot);

                velocity += input.movement * data.swimAcceleration * accelerationPotential * dt;
            }

            maxHorizontalSpeed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);

        }

        private void UpdateClimbing(float dt, Input_t input) {

            maxHorizontalSpeed = data.jumpHorizontalSpeed;

			sprintTimer = 0;
			sprintGracePeriodTime = 0;
			skidding = false;


            var vertMovePosition = new Vector3(position.x, position.y + velocity.y * dt, position.z);

            // collide feet
            float floorPosition;
            Vector3 groundNormal;
            bool onGround = CheckFloor(vertMovePosition, out floorPosition, out groundNormal);
			if (onGround) {
				SetActivity(Activity.OnGround);
				velocity.y = Math.Max(0, velocity.y);
				SetPosition(new Vector3(position.x, floorPosition, position.z));

			} else {

				if (!input.IsPressed(InputType.Jump)) {

					if (input.JustReleased(InputType.Jump)) {
						if (activity != Activity.Falling) {
							climbingAttachCooldown = data.climbAttachCooldown;
						}
						SetActivity(Activity.Falling);

						Vector3 climbingInput = getClimbingVector(input.movement, climbingNormal);
						if (canJump) {
							Vector3 jumpDir = Vector3.zero;
							if (climbingInput.y < 0) {
								// Push away from wall
								jumpDir += input.movement * Mathf.Max(data.groundMaxSpeed, data.sprintSpeed);
								jumpDir.y += data.jumpSpeed;
								maxHorizontalSpeed = data.sprintSpeed;
							} else {
								if (climbingInput.y > 0) {
									// jumping up jumps away from the wall slightly so we don't reattach right away
									jumpDir += climbingInput * data.jumpSpeed;
									jumpDir += climbingNormal * data.jumpSpeed / 4;
								} else if (climbingInput.y >= 0) {
									// left right jumps get a vertical boost
									jumpDir += input.movement * data.groundMaxSpeed;
									jumpDir.y = data.jumpSpeed;
									maxHorizontalSpeed = data.sprintSpeed;
									sprintGracePeriodTime = 0.1f;
								}
							}
							Jump(jumpDir);
							return;
						}
					} else {
						Vector3 climbingInput = getClimbingVector(input.movement, climbingNormal);
						velocity = climbingInput * data.climbSpeed;
						Vector3 move = velocity * dt;
						Vector3 newPosition = position + move;

						if (move.magnitude > 0) {
							bool isOpen = CanMoveTo(move, true, ref newPosition);
							if (isOpen) {
								if (IsClimbPosition(newPosition, -climbingNormal * data.climbWallRange)) {
									climbingAttachPoint = newPosition;
									SetPosition(newPosition);
								}
								//else if (move.y > 0)
								//{
								//    move.y++;
								//    move += -climbingNormal*data.WallJumpRange;
								//    if (tryMoveTo(move, true, dt, newPosition, interpolate))
								//    {
								//        moved = true;
								//        interpolate = true;
								//    }
								//}
							} else {
								velocity = Vector3.zero;
								if (move.magnitude > 0 && (move.x != 0 || move.z != 0)) {
									Vector3 newWallNormal = move.normalized;
									move += -climbingNormal * data.climbWallRange;
									bool isWrapAroundOpen = CanMoveTo(move, true, ref newPosition);
									if (isWrapAroundOpen && IsClimbPosition(newPosition, -newWallNormal * data.climbWallRange)) {
										climbingNormal = newWallNormal;
										climbingAttachPoint = newPosition;
										SetPosition(newPosition);
									}
								}
							}
						} else {
							velocity = Vector3.zero;
							if (IsClimbPosition(newPosition, move.normalized * data.climbWallRange)) {
								climbingAttachPoint = newPosition;
								SetPosition(newPosition);
							}
						}
					}
				
				}
			}






			if (!IsClimbPosition(climbingAttachPoint, -climbingNormal * data.climbWallRange)) {
				if (activity != Activity.Falling) {
					climbingAttachCooldown = data.climbAttachCooldown;
				}
				SetActivity(Activity.Falling);
			}

		}

        public virtual void SetPosition(Vector3 p) {
            go.GetComponent<Rigidbody>().MovePosition(p);
			if (position != p) {
				position = p;
			}
			if (_marker != null) {
				_marker.worldPosition = new Vector2(p.x, p.z);
			}
		}

        public bool IsWading() {
            return activity == Activity.OnGround && world.GetBlock(waistPosition()) == EVoxelBlockType.Water && world.GetBlock(position) != EVoxelBlockType.Water;
        }

        public float getGroundJumpVelocity() {
            float v = data.jumpSpeed;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.data.JumpChange;
            //	v -= (getWeight() - data.BodyWeight) / 50f;
            return v;
        }
		public Weapon GetDefensiveWeapon() {
			Weapon weapon;
			if ((weapon = GetInventorySlot((int)Player.InventorySlot.LEFT_HAND) as Weapon) != null) {
				return weapon;
			}
			if (((weapon = GetInventorySlot((int)Player.InventorySlot.RIGHT_HAND) as Weapon) != null) && weapon.data.hand == WeaponData.Hand.BOTH) {
				return weapon;
			}
			return null;
		}
		public float getGroundMaxSpeed() {
			float maxSpeed = data.groundMaxSpeed;

			if (canRun) {
				if (canSprint) {
					if (sprintTimer > 0 && data.sprintSpeed > 0) {
						maxSpeed = data.sprintSpeed;
					}
					var weapon = GetDefensiveWeapon();
					if (weapon != null && weapon.chargeTime > 0 && weapon.attackHand == 0) {
						maxSpeed *= weapon.data.sprintSpeed;
					}
				}
            }
            else {
                maxSpeed = Mathf.Min(maxSpeed, data.crouchSpeed);
            }

			for (int i=0;i<Player.MaxInventorySize;i++) {
				var weapon = GetInventorySlot(i) as Weapon;
				if (weapon != null && weapon.data.attacks.Length > 0) {
					if (weapon.data.attacks[weapon.attackHand].maxCharge > 0 && weapon.GetMultiplier(this, weapon.chargeTime) >= weapon.data.attacks[weapon.attackHand].maxCharge) {
						maxSpeed *= weapon.data.attacks[weapon.attackHand].moveSpeedWhileFullyCharged;
					}
					else if (weapon.chargeTime > weapon.data.moveSpeedChargeDelay) {
						maxSpeed *= weapon.data.attacks[weapon.attackHand].moveSpeedWhileCharging;
					}
				}
			}
			return maxSpeed;
        }
        public float getGroundAcceleration() {
            float v = data.groundAcceleration;
            return Math.Max(0, v);
        }


        public float getFallingAirFriction() {
            float v = 0;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.data.AirFrictionVertical;
            //foreach(var i in Hands)
            //	if (i != null && i.Active)
            //		v += i.data.AirFrictionVertical;
            return v;
        }

        float getHorizontalAirFriction() {
            float v = data.fallDragHorizontal;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.data.AirFrictionHorizontal;
            //foreach(var i in Hands)
            //	if (i != null && i.Active)
            //		v += i.data.AirFrictionHorizontal;
            return v;
        }

        public Vector3 footPosition() {
            return footPosition(position);
        }
        public Vector3 waistPosition() {
            return waistPosition(position);
        }
        public Vector3 headPosition() {
            return headPosition(position);
        }

        public Vector3 handPosition() {
            return handPosition(position);
        }

        public Vector3 footPosition(Vector3 p) {
            return p + new Vector3(0, 0.05f, 0);
        }
        public Vector3 waistPosition(Vector3 p) {
            return p + new Vector3(0, data.height / 2, 0);
        }
        public Vector3 headPosition(Vector3 p) {
            return p + new Vector3(0, data.height - 0.05f, 0);
        }

        public Vector3 handPosition(Vector3 p) {
            return p + new Vector3(0, data.height - 0.05f, 0);
        }

        public Vector3 renderPosition() {
            return position;
            //Vector3 p = position;
            //if (interpolateTime <= 0)
            //    return p;
            //return p + (interpolateFrom - p) * interpolateTime / interpolateTimeTotal;
        }

		public void UseStamina(float s, bool allowStun) {
			if (stunned || s <= 0) {
				return;
			}

			stamina = Mathf.Max(data.minStamina, stamina - s);
			if (stamina <= 0 && allowStun) {
				stunned = true;
				staminaRecoveryTimer = 0;
			} else {
				staminaRecoveryTimer = data.staminaRecoveryTime;
			}
		}
		public void UseWater(float w) {
			if (water <= 0)
				return;
			water = Mathf.Max(0, water - w);
		}

		void Jump(Vector3 dir) {
			UseStamina(data.jumpStaminaUse, false);

			float curSpeedXZ = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
			float launchSpeedXZ;
			if (sprintGracePeriodTime > 0) {
				launchSpeedXZ = data.sprintSpeed;
			} else {
				launchSpeedXZ = curSpeedXZ;
			}

			float jumpSpeedXZ = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
			velocity += dir;
			float combinedSpeedXZ = curSpeedXZ + jumpSpeedXZ;


			float velY = velocity.y;
			float newSpeedXZ = Mathf.Min(launchSpeedXZ, combinedSpeedXZ);
			velocity = velocity.normalized * newSpeedXZ;
			velocity.y = velY;
		}
		public void Dodge(Vector3 dir, float time) {
			UseStamina(data.jumpStaminaUse, false);
			dodgeTimer = time;
			velocity = dir;
		}

		public void Stun(float s) {
            // Can't stun further if already stunned
            if (stunned || stunInvulnerabilityTimer > 0) {
                return;
            }

			UseStamina(s, true);
            if (stunned) {
				foreach (var i in getInventory()) {
					var w = i as Weapon;
					if (w != null) {
						if (w.castTime > 0 || w.activeTime > 0 || w.chargeTime > 0) {
							w.Interrupt(this);
						}
					}
					moveImpulseTimer = 0;
				}
			}
		}


        public void Damage(float d, PawnData.DamageType t) {
            health = health - d;

			if (t == PawnData.DamageType.Blade) {
				var blood = GameObject.Instantiate<ParticleSystem>(data.bloodParticle, go.transform);
				blood.transform.localPosition = waistPosition() - rigidBody.position;
			}

            GameManager.instance.clientWorld.OnDamage(this, d);
        }

		public void Hit(Projectile projectile, Actor owner) {

			if (dodgeTimer > 0) {
				return;
			}

			float remainingStun = projectile.data.stun;
			float remainingDamage = projectile.data.damage;


			//// Check if we're blocking with shield
			//if (projectile.data.canBlock) {
			//	foreach (var w in getInventory()) {
			//		var shield = w as Weapon;
			//		if (shield != null) {
			//			shield.Defend(this, attacker, weapon, ref remainingStun, ref remainingDamage);
			//		}
			//	}
			//}

			if (remainingDamage > 0) {
				Damage(remainingDamage, projectile.data.damageType);
			}

			if (remainingStun > 0) {
				Stun(remainingStun);
			}


			if (projectile.data.statusEffect != null) {
				AddStatusEffect(projectile.data.statusEffect, projectile.data.statusEffectTime);
			}

			onHit?.Invoke(owner as Pawn);
		}

		public bool Hit(Pawn attacker, Weapon weapon, WeaponData.AttackResult attackResult, float multiplier, bool canBlock) {
            float remainingStun;
            float remainingDamage;

            var dirToEnemy = (attacker.rigidBody.position - rigidBody.position);
            if (dirToEnemy == Vector3.zero) {
                dirToEnemy.x = 1;
            }
            else {
                dirToEnemy.Normalize();
            }
            remainingStun = attackResult.stun * multiplier;
            remainingDamage = attackResult.damage * multiplier;


            if (dodgeTimer > 0) {
                return false;
            }

			// Check if we're blocking with shield
			if (canBlock) {
				foreach (var w in getInventory()) {
					var shield = w as Weapon;
					if (shield != null) {
						shield.Defend(this, attacker, weapon, ref remainingStun, ref remainingDamage);
					}
				}
			}

            if (remainingDamage > 0) {
                Damage(remainingDamage, attackResult.damageType);
            }

            if (remainingStun > 0) {
                Stun(remainingStun);
            }

			if (attackResult.statusEffect != null) {
				AddStatusEffect(attackResult.statusEffect, attackResult.statusEffectTime);
			}


			if (attackResult.interrupt) {
				foreach (var i in getInventory()) {
					var w = i as Weapon;
					if (w != null) {
						if (w.castTime > 0 || w.activeTime > 0 || w.chargeTime > 0) {
							w.Interrupt(this);
						}
					}
					moveImpulseTimer = 0;
				}
			}

            if (attackResult.knockback != 0)
            {
                moveImpulseTimer = 0.1f;
                var kb = (rigidBody.position - attacker.rigidBody.position);
                kb.y = 0;
                kb.Normalize();
                moveImpulse = attackResult.knockback * kb;
            }

            onHit?.Invoke(attacker);

			gameMode.CreateAudioEvent(attacker, attackResult.loudness);

			return true;
		}


		virtual public void LandOnGround() {

        }


        public Item[] getInventory() {
            return _inventory;
        }

        virtual public void SetInventorySlot(int index, Item item) {
			int oldSlot = -1;
            if (item != null) {
                // remove the item from its current slot first
                for (int i = 0; i < MaxInventorySize; i++) {
                    if (GetInventorySlot(i) == item) {
						oldSlot = i;
						SetInventorySlot(i, null);
                        break;
                    }
                }
            }

            _inventory[index] = item;

            item?.OnSlotChange(index, oldSlot, this);
        }

        public Item GetInventorySlot(int index) {
            return _inventory[index];
        }

        virtual protected void Die() {
            alive = false;
			position += new Vector3(0, data.height, 0);

			for (int i = 0; i < MaxInventorySize; i++) {
				var w = GetInventorySlot(i) as Weapon;
				if (w != null) {
					w.Interrupt(this);
				}
			}
		}

		virtual protected bool SetMount(Pawn m) {

            if (m?.driver != null) {
                return false;
            }

			if (m == mount) {
				return true;
			}

            if (mount != null) {
				if (mount.driver != null) {
					mount.driver.silhouetteMode = mount.driver.defaultSilhouetteMode;
				}
				mount.driver = null;
				mount.silhouetteMode = mount.defaultSilhouetteMode;
				mount.SetSilhouetteDirty();
            }

            mount = m;

			skidding = false;

			if (mount != null) {
				mount.SetSilhouetteDirty();
				mount.silhouetteMode = SilhouetteRenderer.Mode.On;
				mount.driver = this;
				silhouetteMode = defaultSilhouetteMode;
				go.transform.parent = mount.go.transform;
                go.transform.localPosition = new Vector3(0,1.0f,0);
				go.transform.localRotation = Quaternion.identity;
            } else {
				silhouetteMode = defaultSilhouetteMode;
				go.transform.parent = null;
            }

			return true;
        }

		public void AddStatusEffect(StatusEffectData data, float time) {
			StatusEffect se = null;
			if (!data.canStack) {
				se = statusEffects.Find(e => e.data == data);
			}

			if (se == null) {
				se = StatusEffect.Create(data, time);
				statusEffects.Add(se);
				se.Apply(this);
				GameManager.instance.clientWorld.OnStatusEffectAdded(this, se);
			} else {
				se.totalTime = Mathf.Max(se.totalTime, time);
				se.time = Mathf.Max(se.time, time);
			}
		}

		protected bool IsValidAttackTarget(Pawn actor) {
			if ((actor == null) || actor.pendingKill || !actor.alive)
				return false;
			var diff = actor.position - position;
			if (diff.magnitude > 20) {
				return false;
			}
			return true;
		}

		public Pawn GetAttackTarget(float angle, float range, float maxAngle, Pawn exclude) {
			Pawn bestTarget = null;

			float bestTargetAngle = maxAngle;
			foreach (var c in world.GetActorIterator<Actors.Pawn>()) {
				if (c.active && c != exclude) {
					if (c.team != team) {
						var diff = c.position - position;
						float dist = diff.magnitude;
						if (dist < range) {
							float angleToEnemy = Mathf.Atan2(diff.x, diff.z);

							float yawDiff = Mathf.Abs(Utils.SignedMinAngleDelta(angleToEnemy * Mathf.Rad2Deg, angle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

							float collisionRadius = 0.5f;

							// take the target's radius into account based on how far away they are
							yawDiff = Mathf.Max(0.001f, yawDiff - Mathf.Atan2(collisionRadius, dist));

							if (yawDiff < bestTargetAngle) {
								bestTarget = c;
								bestTargetAngle = yawDiff;
							}
						}
					}
				}
			}
			return bestTarget;

		}


		public void SetSilhouetteDirty() {
			if (_silhouetteRenderer != null) {
				_silhouetteRenderer.SetDirty();
			}
		}

		public SilhouetteRenderer.Mode silhouetteMode {
			get {
				return (_silhouetteRenderer != null) ? _silhouetteRenderer.mode : SilhouetteRenderer.Mode.Off;
			}
			set {
				if (_silhouetteRenderer != null) {
					_silhouetteRenderer.mode = value;
				}
			}
		}

		protected virtual SilhouetteRenderer.Mode defaultSilhouetteMode {
			get {
				return data.defaultSilhouetteMode;
			}
		}
    }
}


