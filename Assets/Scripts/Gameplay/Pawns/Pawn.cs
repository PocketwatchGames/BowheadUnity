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
        Use,
        Swap,
        AttackLeft,
        AttackRight,
        SelectLeft,
        SelectRight,
        Map,
        Crouch,
		Look,
        Count
    }

	public abstract class Pawn<T, D> : Pawn where T : Pawn<T, D> where D : PawnData {
		public override void Spawn(EntityData data, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(data, pos, instigator, owner, team);
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
        public float stamina;
        public float maxStamina;
        public bool recovering;
        public float recoveryTimer;
        public float dodgeTimer;
        public bool stunned;
        public float stunTimer;
        public float stunAmount;

        [Header("Rendering")]
        public Vector3 interpolateFrom;
        public float interpolateTime;
        public float interpolateTimeTotal;

        [Header("Gameplay")]
        public Pawn.Activity activity;
        public Vector3 climbingNormal;
        public float fallJumpTimer;
        public float maxHorizontalSpeed;
        public bool sliding;
        public bool canMove;
        public bool canRun;
        public bool canJump;
        public bool canClimb;
        public bool canClimbWell;
        public bool canSwim;
        public bool canTurn;
        public bool canAttack;
        public Pawn mount;
        public Pawn driver;

        [Header("Combat")]
        public Pawn attackTarget;
        public bool lockedToTarget;

        [Header("Inventory")]
        [SerializeField]
        protected Item[] _inventory = new Item[Pawn.MaxInventorySize];

        #endregion


        public Vector3 position { get { return rigidBody.position; } set { rigidBody.position = value; } }
        
        public const int MaxInventorySize = 32;

        public enum Activity {
            Falling,
            Swimming,
            Climbing,
            OnGround,
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
            public bool inMotion;
            public Vector3 position;
            public Vector3 velocity;
        };
		        
        public delegate void OnInventoryChangeFn();
        public event OnInventoryChangeFn OnInventoryChange;

		// This field is a HACK and is null on clients
		public Server.BowheadGame gameMode {
			get;
			private set;
		}

		public override void Spawn(EntityData data, Vector3 pos, Actor instigator, Actor owner, Team team) {
			base.Spawn(data, pos, instigator, owner, team);
			this.data = (PawnData)data;
			gameMode = (Server.BowheadGame)((Server.ServerWorld)world).gameMode;
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
            var handblock = world.GetBlock(handPosition(handHoldPos));
            bool isClimbable = WorldUtils.IsClimbable(handblock, canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
                if (isHangPosition && WorldUtils.IsHangable(handblock, canClimbWell) && !WorldUtils.IsSolidBlock(world.GetBlock(p)) && !WorldUtils.IsSolidBlock(world.GetBlock(handPosition(p))) && !WorldUtils.IsSolidBlock(world.GetBlock(handPosition(handHoldPos) + Vector3.up))) {
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
            var handHoldPos = handPosition(p) + checkDir.normalized * data.climbWallRange;
            if (!IsOpen(p))
                return false;
            var handblock = world.GetBlock(handHoldPos);
            bool isClimbable = WorldUtils.IsClimbable(handblock, canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
                if (isHangPosition
                    && WorldUtils.IsHangable(handblock, canClimbWell)
                    && !WorldUtils.IsSolidBlock(world.GetBlock(handHoldPos + Vector3.up))) {
                    isClimbable = true;
                }
            }
            if (velocity.y > data.climbGrabMinZVel && isClimbable) {
                return true;
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
                float dt = world.deltaTime;

                PreSimulate(dt);

                Input_t input = GetInput(dt);

                Simulate(dt, input);
            }
        }

        virtual public void PreSimulate(float dt) {

            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) != null) {
                    GetInventorySlot(i).Tick(dt, this);
                }
            }
        }

        virtual public void Simulate(float dt, Input_t input) {
            if (!alive) {
                go.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
                return;
            }

            if (recoveryTimer > 0) {
                recoveryTimer = Math.Max(0, recoveryTimer - dt);
            }
            else {
                if (stamina < maxStamina) {
                    stamina = Math.Min(maxStamina, stamina + dt * maxStamina / data.staminaRechargeTime);
                }
                else {
                    recovering = false;
                }
            }

            if (stunned) {
                stunAmount = 0;
                stunTimer = Math.Max(0, stunTimer - dt);
                if (stunTimer <= 0) {
                    stunned = false;
                }
            }
            else if (stunAmount > 0) {
                stunAmount = Math.Max(0, stunAmount - dt * data.stunLimit / data.stunRecoveryTime);
            }
            else {
                stunned = false;
            }

            if (dodgeTimer > 0) {
                dodgeTimer = Math.Max(0, dodgeTimer - dt);
            }

            interpolateTime = Math.Max(0, interpolateTime - dt);
            if (canTurn) {
                if (activity == Activity.Climbing) {
                    yaw = Mathf.Atan2(-climbingNormal.x, -climbingNormal.z);
                }
                else {
					if (input.look != Vector3.zero) {
						yaw = Mathf.Atan2(input.look.x, input.look.z);
					}
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

            if (!canMove) {
                velocity = Vector3.zero;
            }
            if (moveImpulseTimer > 0) {
                var m = moveImpulse * (dt / moveImpulseTimer);
                velocity = m / dt;
                moveImpulse -= m;
                moveImpulseTimer = Math.Max(0, moveImpulseTimer - dt);
            }

            position = new Vector3(position.x, position.y + velocity.y * dt, position.z);

            // Collide feet
            float floorPosition;
            Vector3 groundNormal;
            bool onGround = CheckFloor(position, out floorPosition, out groundNormal);
            if (onGround) {
                if (velocity.y <= 0) {

                    if (activity != Activity.OnGround) {
                        LandOnGround();
                        if (velocity.y < 0) {
                            float slopeAccel = 1f + Vector3.Dot(velocity.normalized, -groundNormal);
                            if (slopeAccel < 1f) {
                                velocity += velocity * slopeAccel;
                            }
                        }
                    }
                    velocity.y = Math.Max(0, velocity.y);
                }
                position = new Vector3(position.x, floorPosition, position.z);
            }
            // Collide head
            else if (WorldUtils.IsSolidBlock(world.GetBlock(headPosition()))) {
                // TODO: this is broken
                position = new Vector3(position.x, Math.Min(position.y, (int)headPosition().y - data.height), position.z);
                velocity.y = Math.Min(0, velocity.y);
            }


            if (activity != Activity.Climbing) {
                // Collide XY
                Vector3 moveXZ = new Vector3(velocity.x, 0, velocity.z) * dt;
                Move(moveXZ);
            }

            Vector3 firstCheck, secondCheck;
            if (Math.Abs(input.movement.x) > Math.Abs(input.movement.z)) {
                firstCheck = new Vector3(input.movement.x, 0, 0);
                secondCheck = new Vector3(0, 0, input.movement.z);
            }
            else {
                firstCheck = new Vector3(0, 0, input.movement.z);
                secondCheck = new Vector3(input.movement.x, 0, 0);
            }
            if (activity == Activity.Climbing) {
                if (!canClimb) {
                    activity = Activity.Falling;
                }
            }
            else if (world.GetBlock(position) == EVoxelBlockType.WATER) {
                activity = Activity.Swimming;
            }
            else if (onGround && velocity.y <= 0) {
                activity = Activity.OnGround;

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
                activity = Activity.Falling;

                if (canClimb) {
                    if (firstCheck.magnitude > 0 && CanClimb(firstCheck, position)) {
                        climbingNormal = -new Vector3(Utils.SignOrZero(firstCheck.x), 0, Utils.SignOrZero(firstCheck.z));
                        velocity = Vector3.zero;
                        activity = Activity.Climbing;
                    }
                    else if (secondCheck.magnitude > 0 && CanClimb(secondCheck, position)) {
                        climbingNormal = -new Vector3(Utils.SignOrZero(secondCheck.x), 0, Utils.SignOrZero(secondCheck.z));
                        velocity = Vector3.zero;
                        activity = Activity.Climbing;
                    }
                }

            }

            if (position.y < -1000 || health <= 0) {
                Die();
            }

            if (mount == null) {
                go.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
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
            if (fallJumpTimer > 0) {
                fallJumpTimer = Math.Max(0, fallJumpTimer - dt);
                if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                    if (canJump) {
                        var jumpDir = input.movement * data.dodgeSpeed;
                        jumpDir.y += getGroundJumpVelocity();
                        jump(jumpDir);
                    }
                    fallJumpTimer = 0;
                }
            }
            if (input.IsPressed(InputType.Jump)) {
                if (velocity.y >= 0) {
                    velocity.y += data.jumpBoostAcceleration * dt;
                }
            }
            velocity.y += data.gravity * dt;

            if (input.movement != Vector3.zero) {
                float acceleration = data.fallAcceleration;
                float maxSpeed = getGroundMaxSpeed();
                maxSpeed = Mathf.Clamp(maxSpeed, data.fallMaxHorizontalSpeed, Math.Max(maxHorizontalSpeed, data.fallMaxHorizontalSpeed));

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
            if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                if (canJump) {
                    var jumpDir = input.movement * data.dodgeSpeed;
                    jumpDir.y += getGroundJumpVelocity();
                    jump(jumpDir);
                }
                fallJumpTimer = 0;
            }
            else {
                fallJumpTimer = data.fallJumpTime;
            }
            var block = world.GetBlock(footPosition());
            var midblock = world.GetBlock(waistPosition());
            var topblock = world.GetBlock(headPosition());
            float slideFriction, slideThreshold;
			WorldUtils.GetSlideThreshold(block, midblock, topblock, out slideFriction, out slideThreshold);
            float workModifier = WorldUtils.GetWorkModifier(block, midblock, topblock);
            if (IsWading()) {
                workModifier += data.swimDragHorizontal;
            }


            float curVel = velocity.magnitude;
            Vector3 velChange = Vector3.zero;

            if (input.movement == Vector3.zero && curVel < data.walkSpeed && !sliding) {
                if (curVel > 0) {
                    // Stopping (only when not sliding)
                    float stopAccel = dt * data.walkSpeed / data.walkStopTime;
                    if (curVel < stopAccel)
                        velChange = -velocity;
                    else
                        velChange = -velocity.normalized * stopAccel;
                }
            }
            else {
                float maxSpeed = getGroundMaxSpeed();
                float acceleration = getGroundAcceleration();

                if (input.movement != Vector3.zero) {
                    var normalizedInput = input.movement.normalized;
                    float slopeDot = Vector3.Dot(groundNormal, normalizedInput);
                    maxSpeed *= 1f + slopeDot;

                    slideThreshold *= Vector3.Dot(groundNormal, normalizedInput) + 1f;

                }

                {
                    var desiredVel = input.movement * maxSpeed;
                    var velDiff = desiredVel - velocity;
                    velDiff.y = 0;
                    if (velDiff != Vector3.zero) {
                        acceleration *= velDiff.magnitude;
                        velDiff = velDiff.normalized;

                        var walkChange = data.walkSpeed / data.walkStartTime;
                        if (walkChange > acceleration) {
                            acceleration = Math.Max(acceleration, Math.Min(walkChange, slideThreshold));
                        }
                        velChange = velDiff * acceleration * dt;
                    }
                    else {
                        acceleration = 0;
                    }

                    if (acceleration > slideThreshold) {
                        sliding = true;
                    }
                    else {
                        sliding = false;
                    }
                }
                {
                    // Wind blowing you while running or skidding
                    var wind = gameMode.GetWind(position);
                    if (wind.magnitude >= gameMode.data.windSpeedStormy) {
                        var velDiff = (wind - new Vector3(velocity.x, 0, velocity.z));
                        velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
                        velChange += velDiff * dt * getHorizontalAirFriction();
                    }
                }
            }

            // Sliding
            if (sliding) {
                // Reduce our movement impulse (reduce control while sliding)
                velChange *= slideFriction;

                // Slide down any hills
                float slopeDot = Vector3.Dot(groundNormal, Vector3.up);
                Vector3 slideDir = groundNormal;
                slideDir.y = 0;
                if (slideDir != Vector3.zero) {
                    slideDir = slideDir.normalized;
                    velChange += (1.0f - slopeDot) * dt * -data.gravity * slideDir * (1f - slideFriction);
                }
            }

            // Apply friction for travelling through snow/sand/water
            velChange += -velocity * dt * workModifier;

            velocity += velChange;

            Vector2 horizontalVel = new Vector2(velocity.x, velocity.z);
            maxHorizontalSpeed = horizontalVel.magnitude;


        }

        private void UpdateSwimming(float dt, Input_t input) {
            if (input.inputs[(int)InputType.Jump] == InputState.Pressed) {
                if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                    if (canJump) {
                        var jumpDir = input.movement * data.dodgeSpeed;
                        jumpDir.y += data.swimJumpBoostAcceleration;
                        jump(jumpDir);
                    }
                }
                velocity.y = Math.Min(velocity.y + data.swimMaxSpeed * dt, data.swimJumpSpeed);
            }
            if (input.inputs[(int)InputType.Crouch] == InputState.Pressed) {
                velocity.y = velocity.y - data.swimSinkAcceleration * dt;
            }
            velocity.y += data.gravity * dt;
            if (world.GetBlock(headPosition()) == EVoxelBlockType.WATER) {
                velocity.y += -velocity.y * dt * data.swimDragVertical;
                velocity.y += data.bouyancy * dt;
            }
            if (world.GetBlock(position) == EVoxelBlockType.WATER) {
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

            maxHorizontalSpeed = data.groundMaxSpeed;

            if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                activity = Activity.Falling;


                Vector3 climbingInput = getClimbingVector(input.movement, climbingNormal);
                if (canJump) {
                    Vector3 jumpDir = Vector3.zero;
                    if (climbingInput.y < 0) {
                        // Push away from wall
                        jumpDir += input.movement * data.groundMaxSpeed;
                        jumpDir.y += data.jumpSpeed;
                    }
                    else {
                        if (climbingInput.y > 0) {
                            // jumping up jumps away from the wall slightly so we don't reattach right away
                            jumpDir += climbingInput * data.jumpSpeed;
                            jumpDir += climbingNormal * data.jumpSpeed / 2;
                        }
                        else if (climbingInput.y >= 0) {
                            // left right jumps get a vertical boost
                            jumpDir.y = data.jumpSpeed;
                        }
                    }
                    jump(jumpDir);
                    return;
                }
            }


            if (!input.IsPressed(InputType.Crouch) || input.IsPressed(InputType.Jump)) {
                var climbingInput = getClimbingVector(input.movement, climbingNormal);
                velocity = climbingInput * data.climbSpeed;
            }

            var vertMovePosition = new Vector3(position.x, position.y + velocity.y * dt, position.z);

            // collide feet
            float floorPosition;
            Vector3 groundNormal;
            bool onGround = CheckFloor(vertMovePosition, out floorPosition, out groundNormal);
            if (onGround) {
                activity = Activity.OnGround;
                velocity.y = Math.Max(0, velocity.y);
                position = new Vector3(position.x, floorPosition, position.z);
            }
            else {
                Vector3 move = velocity * dt;
                Vector3 newPosition = position + move;

                if (move.magnitude > 0) {

                    bool isOpen = CanMoveTo(move, true, ref newPosition);
                    if (isOpen) {
                        if (IsClimbPosition(newPosition, -climbingNormal * data.climbWallRange)) {
                            SetPosition(newPosition);
                        }
                        //else if (move.y > 0)
                        //{
                        //	move.y++;
                        //	move += -climbingNormal*data.WallJumpRange;
                        //	if (tryMoveTo(move, true, dt, newPosition, interpolate))
                        //	{
                        //		moved = true;
                        //		interpolate = true;
                        //	}
                        //}
                        else if (move.magnitude > 0 && (move.x != 0 || move.z != 0)) {
                            Vector3 newWallNormal = move.normalized;
                            move += -climbingNormal * data.climbWallRange;
                            bool isWrapAroundOpen = CanMoveTo(move, true, ref newPosition);
                            if (isWrapAroundOpen && IsClimbPosition(newPosition, -newWallNormal * data.climbWallRange)) {
                                climbingNormal = newWallNormal;
                                SetPosition(newPosition, 0.1f);
                            }
                        }
                        else {
                            velocity = Vector3.zero;
                        }
                    }
                    else if (IsClimbPosition(newPosition, move.normalized * data.climbWallRange)) {
                        SetPosition(newPosition, 0.1f);
                    }
                }
            }

            if (!IsClimbPosition(position, -climbingNormal * data.climbWallRange)) {
                activity = Activity.Falling;
            }

        }

        public virtual void SetPosition(Vector3 p, float interpolateTime = 0) {
            if (interpolateTime > 0) {
                interpolateFrom = renderPosition();
                interpolateTime = interpolateTimeTotal = Math.Max(interpolateTime, interpolateTime);
            }
            go.GetComponent<Rigidbody>().MovePosition(p);
            position = p;
        }

        public bool IsWading() {
            return activity == Activity.OnGround && world.GetBlock(waistPosition()) == EVoxelBlockType.WATER && world.GetBlock(position) != EVoxelBlockType.WATER;
        }

        public float getGroundJumpVelocity() {
            float v = data.jumpSpeed;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.data.JumpChange;
            //	v -= (getWeight() - data.BodyWeight) / 50f;
            return v;
        }
        public float getJumpBoostAcceleration() {
            float v = data.jumpBoostAcceleration;
            return v;
        }
        public float getGroundMaxSpeed() {
			float modifier = 1;
			for (int i=0;i<MaxInventorySize;i++) {
				var item = GetInventorySlot(i) as Weapon;
				if (item!= null) {
					if (item.cooldown > 0) {
						modifier = Mathf.Min(modifier, item.data.attacks[item.attackType].moveSpeedDuringCooldown);
					}
					else if (item.castTime >= item.data.moveSpeedChargeDelay) {
						modifier = Mathf.Min(modifier, item.data.attacks[item.attackType].moveSpeedDuringCast);
					}
					else if (item.chargeTime >= item.data.moveSpeedChargeDelay) {
						modifier = Mathf.Min(modifier, item.data.moveSpeedWhileCharging);
					}
				}
			}
			float maxSpeed = data.groundMaxSpeed * modifier;

			if (canRun) {
                return maxSpeed;
            }
            else {
                return Mathf.Min(maxSpeed, data.crouchSpeed);
            }
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

        public void useStamina(float s) {
            if (stamina <= 0)
                return;
            stamina -= s;
            recoveryTimer = data.recoveryTime;
            if (stamina <= 0) {
                recovering = true;
            }
        }

        void jump(Vector3 dir) {
            useStamina(data.jumpStaminaUse);
            dodgeTimer = dodgeTimer + data.dodgeTime;

            float curSpeedXZ = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);

            velocity += dir;

            float velY = velocity.y;
            float newSpeedXZ = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            if (newSpeedXZ > data.groundMaxSpeed) {
                newSpeedXZ = Mathf.Min(curSpeedXZ, data.groundMaxSpeed);
                velocity = velocity.normalized * newSpeedXZ;
                velocity.y = velY;
            }
        }

        public void stun(float s) {
            // Can't stun further if already stunned
            if (stunned || s <= 0) {
                return;
            }

            stunAmount += s;
            if (stunAmount >= data.stunLimit) {
                stunned = true;
                stunTimer = data.stunRecoveryTime;
                foreach (var w in getInventory()) {
                    var weapon = w as Weapon;
                    if (weapon != null) {
                        weapon.interrupt(this);
                    }
                }
            }
        }


        public void damage(float d) {
            health = health - d;

            var blood = GameObject.Instantiate<ParticleSystem>(data.bloodParticle, go.transform);
            blood.transform.localPosition = waistPosition() - rigidBody.position;
        }

        public void hit(Pawn attacker, Weapon weapon, WeaponData.AttackData attackData) {
            float remainingStun;
            float remainingDamage;

            var dirToEnemy = (attacker.rigidBody.position - rigidBody.position);
            if (dirToEnemy == Vector3.zero) {
                dirToEnemy.x = 1;
            }
            else {
                dirToEnemy.Normalize();
            }
            float angleToEnemy = Mathf.Repeat(Mathf.Atan2(dirToEnemy.x, dirToEnemy.z) - yaw, Mathf.PI * 2);
            if (attackData.attackDamageBackstab > 0 && Math.Abs(angleToEnemy) < data.backStabAngle) {
                remainingStun = attackData.stunPowerBackstab;
                remainingDamage = attackData.attackDamageBackstab;
            }
            else {
                remainingStun = attackData.stunPower;
                remainingDamage = attackData.attackDamage;
            }


            if (dodgeTimer > 0) {
                return;
            }

			// Check if we're blocking with shield
			if (!attackData.unblockable) {
				foreach (var w in getInventory()) {
					var shield = w as Weapon;
					if (shield != null) {
						shield.defend(this, attacker, weapon, attackData, ref remainingStun, ref remainingDamage);
					}
				}
			}

            if (remainingDamage > 0) {
                damage(remainingDamage);
            }

            if (remainingStun > 0) {

                if (attackData.knockback != 0) {
                    moveImpulseTimer = 0.1f;
                    var kb = (rigidBody.position - attacker.rigidBody.position);
                    kb.y = 0;
                    kb.Normalize();
                    moveImpulse = attackData.knockback * kb;
                }

                useStamina(attackData.staminaDrain);
                stun(remainingStun);
            }

			if (attackData.interrupt) {
				foreach (var i in getInventory()) {
					var w = i as Weapon;
					if (w != null) {
						if (w.castTime > 0 || w.activeTime > 0 || w.chargeTime > 0) {
							w.interrupt(this);
						}
					}
					moveImpulseTimer = 0;
				}
			}
		}


        virtual public void LandOnGround() {

        }


        public Item[] getInventory() {
            return _inventory;
        }

        virtual public void SetInventorySlot(int index, Item item) {

            if (item != null) {
                // remove the item from its current slot first
                for (int i = 0; i < MaxInventorySize; i++) {
                    if (GetInventorySlot(i) == item) {
                        SetInventorySlot(i, null);
                        break;
                    }
                }
            }

            _inventory[index] = item;

            item?.OnSlotChange(index, this);

            OnInventoryChange?.Invoke();
        }

        public Item GetInventorySlot(int index) {
            return _inventory[index];
        }

        virtual protected void Die() {
            alive = false;
        }

        virtual protected bool SetMount(Pawn m) {

            if (m?.driver != null) {
                return false;
            }
            if (mount != null) {
                mount.driver = null;
            }

            mount = m;

            if (mount != null) {
                mount.driver = this;
            }

            if (mount != null) {
                go.transform.parent = mount.go.transform;
                go.transform.localPosition = mount.headPosition()-mount.position;
				go.transform.localRotation = Quaternion.identity;
            }
            else {
                go.transform.parent = null;
            }


            return true;
        }
    }
}


