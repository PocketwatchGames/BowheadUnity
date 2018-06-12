using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {
    public class Actor : Entity {



        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }








        public const int MAX_INVENTORY_SIZE = 32;

        public enum Activity {
            FALLING,
            SWIMMING,
            CLIMBING,
            ONGROUND,
        }


        new public class CData : Entity.CData {
            public float height;
            public float recoveryTime;
            public float staminaRechargeTime;
            public float maxHealth;
            public float maxStamina;
            public float dodgeTime;
            public float collisionRadius;
            public float stunLimit;
            public float stunRecoveryTime;
            public float backStabAngle;

            public float jumpSpeed;
            public float dodgeSpeed;
            public float jumpBoostAcceleration;
            public float jumpStaminaUse;
            public float groundAcceleration; // accel = veldiff * groundAccel * dt
            public float crouchSpeed;
            public float walkSpeed;
            public float walkStartTime;
            public float walkStopTime;
            public float groundMaxSpeed;
            public float groundWindDrag;
            public float slideThresholdSlope;
            public float slideThresholdFlat;

            public float gravity;
            public float fallJumpTime;
            public float fallAcceleration;
            public float fallDragHorizontal;
            public float fallMaxHorizontalSpeed;

            public float climbWallRange;
            public float climbGrabMinZVel;
            public float climbSpeed;

            public float bouyancy;
            public float swimJumpSpeed;
            public float swimSinkAcceleration;
            public float swimJumpBoostAcceleration;
            public float swimAcceleration;
            public float swimMaxSpeed;
            public float swimDragVertical;
            public float swimDragHorizontal;

        }


        public class CState : Entity.CState {
            PlayerCmd_t last;

            public bool spawned;
            public Activity activity;
            public Vector3 position;
            public Vector3 velocity;
            public float yaw;
            public int team;

            public Item[] inventory = new Item[MAX_INVENTORY_SIZE];

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

            public Actor attackTarget;

            public Vector3 moveImpulse;
            public float moveImpulseTimer;

            public Vector3 interpolateFrom;
            public float interpolateTime;
            public float interpolateTimeTotal;

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
            public bool lockedToTarget;
        };


        public PlayerCmd_t cur;


        new public CData Data { get { return GetData<CData>(); } }
        new public CState State { get { return GetState<CState>(); } }
        public static CData GetData(string dataName) { return GetData<CData>(dataName); }


        protected Actor(CData d, CState s) : base(d,s) {

        }

        public void UpdatePlayerCmd(PlayerCmd_t cmd) {
            State.last = cur;
            cur = cmd;
        }

        public bool CanMoveTo(Vector3 moveVector, bool allowFall, float gameTime, ref Vector3 position, ref bool interpolate) {

            Vector3 move = moveVector;

            {
                Vector3 movePosition = waistPosition(position) + moveVector;
                Vector3 footPoint = footPosition(position) + moveVector;
                Vector3 stepDownPoint = headPosition(position) + moveVector + new Vector3(0, 0, -0.5f);

                // Step forward
                if (IsOpen(position + moveVector)) {
                    if (State.activity == Activity.ONGROUND) {
                        // Step down
                        Vector3 fp = footPoint + new Vector3(0, 0, -0.5f);
                        if (!World::isSolidBlock(world.getBlock(fp))) {
                            if (World::isSolidBlock(world.getBlock(fp + new Vector3(0, 0, -1)))) {
                                if (world.getBlock(movePosition) != EBlockType::BLOCK_TYPE_WATER) {
                                    move.z -= 1;
                                    interpolate = true;
                                }
                            }
                            else if (!allowFall) {
                                return false;
                            }
                        }
                    }


                    position += move;
                    return true;
                }
            }

            // Step up
            {
                Vector3 movePosition = position + move + new Vector3(0, 0, 1.0f);
                Vector3 footPoint = footPosition(position) + move;
                Vector3 headPoint = headPosition(position) + move + new Vector3(0, 0, 1.0f);

                if (World::isSolidBlock(world.getBlock(footPoint)) && !World::isSolidBlock(world.getBlock(movePosition)) && !World::isSolidBlock(world.getBlock(headPoint))) {
                    move += new Vector3(0, 0, 1);

                    position += move;

                    interpolate = true;
                    return true;
                }
            }

            return false;
        }

        bool IsClimbPosition(Vector3 p, Vector3 wallDirection) {
            var handHoldPos = p + wallDirection.normalized * Data.climbWallRange;
            if (!IsOpen(p))
                return false;
            var handblock = world.getBlock(handPosition(handHoldPos));
            bool isClimbable = World::isClimbable(handblock, State.canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = fmodf(p.z, 1f) > 0.9f;
                if (isHangPosition && World::isHangable(handblock, State.canClimbWell) && !World::isSolidBlock(world.getBlock(p)) && !World::isSolidBlock(world.getBlock(handPosition(p))) && !World::isSolidBlock(world.getBlock(handPosition(handHoldPos) + Vector3(0, 0, 1)))) {
                    isClimbable = true;
                }
            }
            if (State.velocity.z > Data.climbGrabMinZVel && isClimbable) {
                return true;
            }
            return false;
        }

        bool CanClimb(Vector3 checkDir, Vector3 checkPos) {
            var p = checkPos;
            var handHoldPos = p + checkDir.normalized * Data.climbWallRange;
            if (!isOpen(p))
                return false;
            var handblock = world.getBlock(handPosition(handHoldPos));
            bool isClimbable = World::isClimbable(handblock, State.canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = fmodf(p.z, 1f) > 0.9f;
                if (isHangPosition
                    && World::isHangable(handblock, State.canClimbWell)
                    && !World::isSolidBlock(world.getBlock(handPosition(handHoldPos)))
                    && !World::isSolidBlock(world.getBlock(handHoldPos + Vector3(0, 0, 1)))) {
                    isClimbable = true;
                }
            }
            if (State.velocity.z > Data.climbGrabMinZVel && isClimbable) {
                return true;
            }
            return false;
        }

        Vector3 getClimbingVector(Vector3 i, Vector3 surfaceNormal) {
            var axis = Vector3.Cross(-Vector3.up, surfaceNormal);
            var rotation = Quaternion.AxisAngle(axis, pi_over_2<float>());
            Matrix4x4 climbMatrix = Matrix4x4.Rotate(rotation);
            var climbingVector = climbMatrix.multiply(i);

            // If we're climbing nearly exaclty up, change it to up
            if (Math.Abs(climbingVector.z) > Math.Sqrt(Math.Pow(climbingVector.x, 2) + Math.Pow(climbingVector.y, 2))) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0, 0, Math.Sign(climbingVector.z) * inputSpeed);
            }
            else if (Math.Abs(climbingVector.x) > Math.Abs(climbingVector.y)) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3((float)Math.Sign(climbingVector.x), 0f, 0f) * inputSpeed;
            }
            else {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0f, (float)Math.Sign(climbingVector.y), 0f) * inputSpeed;
            }
            return climbingVector;
        }

        bool CheckFloor(Vector3 position, out float floorHeight) {
            floorHeight = 0;
            Vector3 floorPosition = footPosition(position) + new Vector3(0, 0, -0.1f);

            if (!World::isSolidBlock(world.getBlock(floorPosition))) {
                return false;
            }

            while (World::isSolidBlock(world.getBlock(floorPosition))) {
                floorPosition.z = (float)Math.Floor(floorPosition.z) + 1;
            }
            floorHeight = floorPosition.z;
            return true;
        }

        bool IsOpen(Vector3 position) {
            return !World::isSolidBlock(world.getBlock(footPosition(position)))
                && !World::isSolidBlock(world.getBlock(waistPosition(position)))
                && !World::isSolidBlock(world.getBlock(headPosition(position)));
        }


        protected void Update(float dt, Input_t input) {
            if (State.recoveryTimer > 0) {
                State.recoveryTimer = Math.Max(0, State.recoveryTimer - dt);
            }
            else {
                if (State.stamina < State.maxStamina) {
                    State.stamina = Math.Min(State.maxStamina, State.stamina + dt * State.maxStamina / Data.staminaRechargeTime);
                }
                else {
                    State.recovering = false;
                }
            }

            if (State.stunned) {
                State.stunAmount = 0;
                State.stunTimer = Math.Max(0, State.stunTimer - dt);
                if (State.stunTimer <= 0) {
                    State.stunned = false;
                }
            }
            else if (State.stunAmount > 0) {
                State.stunAmount = Math.Max(0, State.stunAmount - dt * Data.stunLimit / Data.stunRecoveryTime);
            }
            else {
                State.stunned = false;
            }

            if (State.dodgeTimer > 0) {
                State.dodgeTimer = Math.Max(0, State.dodgeTimer - dt);
            }

            State.interpolateTime = Math.Max(0, State.interpolateTime - dt);
            if (State.canTurn) {
                if (State.activity == Activity.CLIMBING) {
                    State.yaw = (float)Math.Atan2(-State.climbingNormal.y, -State.climbingNormal.x);
                }
                else {
                    if (State.lockedToTarget) {
                        var diff = State.attackTarget.State.position - State.position;
                        State.yaw = (float)Math.Atan2(diff.y, diff.x);
                    }
                    else if (input.movement != Vector3.zero) {
                        State.yaw = input.yaw;
                    }
                }
            }


            if (State.activity == Activity.CLIMBING) {
                UpdateClimbing(dt, input);
            }
            else if (State.activity == Activity.SWIMMING) {
                UpdateSwimming(dt, input);
            }
            else if (State.activity == Activity.ONGROUND) {
                UpdateGround(dt, input);
            }
            else if (State.activity == Activity.FALLING) {
                UpdateFalling(dt, input);
            }

            if (!State.canMove) {
                State.velocity = Vector3.zero;
            }
            if (State.moveImpulseTimer > 0) {
                var m = State.moveImpulse * (dt / State.moveImpulseTimer);
                State.velocity = m / dt;
                State.moveImpulse -= m;
                State.moveImpulseTimer = Math.Max(0, State.moveImpulseTimer - dt);
            }

            State.position.z += State.velocity.z * dt;

            // Collide feet
            float floorPosition;
            bool onGround = CheckFloor(State.position, out floorPosition);
            if (onGround) {
                if (State.velocity.z <= 0) {

                    if (State.activity != Activity.ONGROUND) {
                        onLand();
                        if (State.velocity.z < 0) {
                            Vector3 groundNormal = world.getGroundNormal(footPosition(State.position));
                            float slopeAccel = 1f + Vector3.Dot(State.velocity.normalized,-groundNormal);
                            if (slopeAccel < 1f) {
                                State.velocity += State.velocity * slopeAccel;
                            }
                        }
                    }
                    State.velocity.z = Math.Max(0, State.velocity.z);
                }
                State.position.z = floorPosition + Data.height / 2;
            }

            // Collide head
            if (World::isSolidBlock(world.getBlock(headPosition(State.position)))) {
                // TODO: this is broken
                State.position.z = Math.Min(State.position.z, (int)headPosition(State.position).z - Data.height);
                State.velocity.z = Math.Min(0, State.velocity.z);
            }


            if (State.activity != Activity.CLIMBING) {
                // Collide XY
                Vector3 moveXY = new Vector3(State.velocity.x, State.velocity.y, 0) * dt;
                Move(moveXY, dt);
            }

            Vector3 firstCheck, secondCheck;
            if (Math.Abs(input.movement.x) > Math.Abs(input.movement.y)) {
                firstCheck = new Vector3(input.movement.x, 0, 0);
                secondCheck = new Vector3(0, input.movement.y, 0);
            }
            else {
                firstCheck = new Vector3(0, input.movement.y, 0);
                secondCheck = new Vector3(input.movement.x, 0, 0);
            }
            if (State.activity == Activity.CLIMBING) {
                if (!State.canClimb) {
                    State.activity = Activity.FALLING;
                }
            }
            else if (world.getBlock(State.position) == EBlockType::BLOCK_TYPE_WATER) {
                State.activity = Activity.SWIMMING;
            }
            else if (onGround && State.velocity.z <= 0) {
                State.activity = Activity.ONGROUND;

                //if (State.Crouched)
                //{
                //	bool interpolate = false;
                //	Vector3 climbDownPos = State.position + Vector3(0, 0, -2f);
                //	Vector3 wallNormal;
                //	if (canClimb(-firstCheck, climbDownPos + firstCheck * Data.WallJumpRange / 2)) {
                //		interpolate = true;
                //		climbDownPos += firstCheck * Data.WallJumpRange / 2;
                //		wallNormal = firstCheck;
                //	}
                //	else if (canClimb(-secondCheck, climbDownPos + secondCheck * Data.WallJumpRange / 2)) {
                //		interpolate = true;
                //		climbDownPos += secondCheck * Data.WallJumpRange / 2;
                //		wallNormal = firstCheck;
                //	}
                //	if (interpolate)
                //	{
                //		State.physics = PhysicsState::CLIMBING;
                //		setPosition(climbDownPos, 0.1f);
                //		State.climbingNormal = Vector3((float)Math.Sign(wallNormal.x), (float)Math.Sign(wallNormal.y), 0);
                //		State.velocity = Vector3.zero;
                //		if (State.climbingNormal.x != 0)
                //			State.velocity.y = 0;
                //		if (State.climbingNormal.y != 0)
                //			State.velocity.x = 0;
                //	}
                //}

            }
            else {
                State.activity = Activity.FALLING;

                if (State.canClimb) {
                    if (firstCheck.magnitude > 0 && CanClimb(firstCheck, State.position)) {
                        State.climbingNormal = -new Vector3((float)Math.Sign(firstCheck.x), (float)Math.Sign(firstCheck.y), 0);
                        State.velocity = Vector3.zero;
                        State.activity = Activity.CLIMBING;
                    }
                    else if (secondCheck.magnitude > 0 && CanClimb(secondCheck, State.position)) {
                        State.climbingNormal = -new Vector3((float)Math.Sign(secondCheck.x), (float)Math.Sign(secondCheck.y), 0);
                        State.velocity = Vector3.zero;
                        State.activity = Activity.CLIMBING;
                    }
                }

            }


        }

        bool Move(Vector3 moveXY, float dt) {
            float moveXYLength = moveXY.magnitude;
            if (moveXYLength > 0) {
                bool interpolate = false;
                Vector3 newPosition = State.position;
                Vector3 firstCheck, secondCheck;
                if (Math.Abs(moveXY.x) > Math.Abs(moveXY.y)) {
                    firstCheck = new Vector3(moveXY.x, 0, 0);
                    secondCheck = new Vector3(0, moveXY.y, 0);
                }
                else {
                    firstCheck = new Vector3(0, moveXY.y, 0);
                    secondCheck = new Vector3(moveXY.x, 0, 0);
                }

                Vector3 firstClimbDownPos = newPosition + firstCheck + new Vector3(0, 0, -1.05f);
                Vector3 secondClimbDownPos = newPosition + secondCheck + new Vector3(0, 0, -1.05f);

                if (CanMoveTo(moveXY, true, dt, ref newPosition, ref interpolate)) {
                    return true;
                }
                else if (CanMoveTo(firstCheck, true, dt, ref newPosition, ref interpolate)) {
                    SetPosition(newPosition, interpolate ? 0.1f : 0);
                    return true;
                }
                else if (CanMoveTo(secondCheck, true, dt, ref newPosition, ref interpolate)) {
                    SetPosition(newPosition, interpolate ? 0.1f : 0);
                    return true;
                }

            }
            return false;

        }

        private void UpdateFalling(float dt, Input_t input) {
            if (State.fallJumpTimer > 0) {
                State.fallJumpTimer = Math.Max(0, State.fallJumpTimer - dt);
                if (input.inputs[(int)InputType::JUMP] == InputState::JUST_PRESSED) {
                    if (State.canJump) {
                        var jumpDir = input.movement * Data.dodgeSpeed;
                        jumpDir.z += getGroundJumpVelocity();
                        jump(jumpDir);
                    }
                    State.fallJumpTimer = 0;
                }
            }
            if (input.IsPressed(InputType::JUMP)) {
                if (State.velocity.z >= 0) {
                    State.velocity.z += Data.jumpBoostAcceleration * dt;
                }
            }
            State.velocity.z += Data.gravity * dt;

            if (input.movement != Vector3.zero) {
                float acceleration = Data.fallAcceleration;
                float maxSpeed = getGroundMaxSpeed();
                maxSpeed = MathHelper.Clamp(maxSpeed, Data.fallMaxHorizontalSpeed, Math.Max(State.maxHorizontalSpeed, Data.fallMaxHorizontalSpeed));

                var normalizedVelocity = State.velocity / maxSpeed;
                normalizedVelocity.z = 0;
                var normalizedInput = input.movement.normalized;
                float dot = normalizedInput.dot(normalizedVelocity);
                float accelerationPotential = Math.Min(1.0f, 1.0f - dot);
                State.velocity += input.movement * acceleration * accelerationPotential * dt;

            }

            // For parachutes and such
            float airFriction = getFallingAirFriction();
            State.velocity.z -= State.velocity.z * dt * airFriction;

            var wind = world.getWind(State.position);
            var velDiff = (wind - new Vector3(State.velocity.x, State.velocity.y, 0));
            velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
            State.velocity += velDiff * dt * getHorizontalAirFriction();

        }

        private void UpdateGround(float dt, Input_t input) {
            if (input.inputs[(int)InputType::JUMP] == InputState::JUST_PRESSED) {
                if (State.canJump) {
                    var jumpDir = input.movement * Data.dodgeSpeed;
                    jumpDir.z += getGroundJumpVelocity();
                    jump(jumpDir);
                }
                State.fallJumpTimer = 0;
            }
            else {
                State.fallJumpTimer = Data.fallJumpTime;
            }
            var block = world.getBlock(footPosition(State.position));
            var midblock = world.getBlock(waistPosition(State.position));
            var topblock = world.getBlock(headPosition(State.position));
            float slideFriction, slideThreshold;
            World::getSlideThreshold(block, midblock, topblock, out slideFriction, out slideThreshold);
            float workModifier = World::getWorkModifier(block, midblock, topblock);
            if (IsWading()) {
                workModifier += Data.swimDragHorizontal;
            }

            Vector3 groundNormal = world.getGroundNormal(footPosition(State.position));


            float curVel = State.velocity.magnitude;
            Vector3 velChange = Vector3.zero;

            if (input.movement == Vector3.zero && curVel < Data.walkSpeed && !State.sliding) {
                if (curVel > 0) {
                    // Stopping (only when not sliding)
                    float stopAccel = dt * Data.walkSpeed / Data.walkStopTime;
                    if (curVel < stopAccel)
                        velChange = -State.velocity;
                    else
                        velChange = -State.velocity.normalized * stopAccel;
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
                    var velDiff = desiredVel - State.velocity;
                    velDiff.z = 0;
                    if (velDiff != Vector3.zero) {
                        acceleration *= velDiff.magnitude;
                        velDiff = velDiff.normalized;

                        var walkChange = Data.walkSpeed / Data.walkStartTime;
                        if (walkChange > acceleration) {
                            acceleration = Math.Max(acceleration, Math.Min(walkChange, slideThreshold));
                        }
                        velChange = velDiff * acceleration * dt;
                    }
                    else {
                        acceleration = 0;
                    }

                    if (acceleration > slideThreshold) {
                        State.sliding = true;
                    }
                    else {
                        State.sliding = false;
                    }
                }
                {
                    // Wind blowing you while running or skidding
                    var wind = world.getWind(State.position);
                    var velDiff = (wind - new Vector3(State.velocity.x, State.velocity.y, 0));
                    velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
                    velChange += velDiff * dt * getHorizontalAirFriction();
                }
            }

            // Sliding
            if (State.sliding) {
                // Reduce our movement impulse (reduce control while sliding)
                velChange *= slideFriction;

                // Slide down any hills
                float slopeDot = Vector3.Dot(groundNormal, Vector3.up);
                Vector3 slideDir = groundNormal;
                slideDir.z = 0;
                if (slideDir != Vector3.zero) {
                    slideDir = slideDir.normalized;
                    velChange += (1.0f - slopeDot) * dt * -Data.gravity * slideDir * (1f - slideFriction);
                }
            }

            // Apply friction for travelling through snow/sand/water
            velChange += -State.velocity * dt * workModifier;

            State.velocity += velChange;

            Vector2 horizontalVel = new Vector2(State.velocity.x, State.velocity.y);
            State.maxHorizontalSpeed = horizontalVel.magnitude;


        }

        private void UpdateSwimming(float dt, Input_t input) {
            if (input.inputs[(int)InputType::JUMP] == InputState::PRESSED) {
                if (input.inputs[(int)InputType::JUMP] == InputState::JUST_PRESSED) {
                    if (State.canJump) {
                        var jumpDir = input.movement * Data.dodgeSpeed;
                        jumpDir.z += Data.swimJumpBoostAcceleration;
                        jump(jumpDir);
                    }
                }
                State.velocity.z = Math.Min(State.velocity.z + Data.swimMaxSpeed * dt, Data.swimJumpSpeed);
            }
            if (input.inputs[(int)InputType::CROUCH] == InputState::PRESSED) {
                State.velocity.z = State.velocity.z - Data.swimSinkAcceleration * dt;
            }
            State.velocity.z += Data.gravity * dt;
            if (world.getBlock(headPosition(State.position)) == EBlockType::BLOCK_TYPE_WATER) {
                State.velocity.z += -State.velocity.z * dt * Data.swimDragVertical;
                State.velocity.z += Data.bouyancy * dt;
            }
            if (world.getBlock(State.position) == EBlockType::BLOCK_TYPE_WATER) {
                State.velocity.z += -State.velocity.z * dt * Data.swimDragVertical;
                State.velocity.z += Data.bouyancy * dt;
            }

            var current = world.getCurrent((int)State.position.x, (int)State.position.y, (int)State.position.z);
            var velDiff = (current - new Vector3(State.velocity.x, State.velocity.y, 0));
            velDiff *= velDiff.magnitude; // drag is exponential, thanks zeno!
            State.velocity += velDiff * dt * Data.swimDragHorizontal;

            if (input.movement == Vector3.zero) {
            }
            else {

                var normalizedInput = input.movement.normalized;
                var normalizedVelocity = State.velocity / Data.swimMaxSpeed;
                float dot = normalizedInput.dot(normalizedVelocity);
                float accelerationPotential = Math.Min(1.0f, 1.0f - dot);

                State.velocity += input.movement * Data.swimAcceleration * accelerationPotential * dt;
            }

            State.maxHorizontalSpeed = (float)Math.Sqrt(State.velocity.x * State.velocity.x + State.velocity.y * State.velocity.y);

        }

        private void UpdateClimbing(float dt, Input_t input) {

            State.maxHorizontalSpeed = Data.groundMaxSpeed;

            if (input.inputs[(int)InputType::JUMP] == InputState::JUST_PRESSED) {
                State.activity = Activity.FALLING;


                Vector3 climbingInput = getClimbingVector(input.movement, State.climbingNormal);
                if (State.canJump) {
                    Vector3 jumpDir = Vector3.zero;
                    if (climbingInput.z < 0) {
                        // Push away from wall
                        jumpDir += input.movement * Data.groundMaxSpeed;
                        jumpDir.z += Data.jumpSpeed;
                    }
                    else {
                        if (climbingInput.z > 0) {
                            // jumping up jumps away from the wall slightly so we don't reattach right away
                            jumpDir += climbingInput * Data.jumpSpeed;
                            jumpDir += State.climbingNormal * Data.jumpSpeed / 2;
                        }
                        else if (climbingInput.z >= 0) {
                            // left right jumps get a vertical boost
                            jumpDir.z = Data.jumpSpeed;
                        }
                    }
                    Jump(jumpDir);
                    return;
                }
            }


            if (!input.IsPressed(InputType::CROUCH) || input.IsPressed(InputType::JUMP)) {
                var climbingInput = getClimbingVector(input.movement, State.climbingNormal);
                State.velocity = climbingInput * Data.climbSpeed;
            }

            var vertMovePosition = new Vector3(State.position.x, State.position.y, State.position.z + State.velocity.z * dt);

            // collide feet
            float floorPosition;
            bool onGround = CheckFloor(vertMovePosition, out floorPosition);
            if (onGround) {
                State.activity = Activity.ONGROUND;
                State.velocity.z = Math.Max(0, State.velocity.z);
                State.position.z = floorPosition + 1;
            }
            else {
                bool interpolate = false;
                Vector3 move = State.velocity * dt;
                Vector3 newPosition = State.position + move;

                if (move.magnitude > 0) {

                    bool isOpen = CanMoveTo(move, true, dt, ref newPosition, ref interpolate);
                    if (isOpen) {
                        if (IsClimbPosition(newPosition, -State.climbingNormal * Data.climbWallRange)) {
                            SetPosition(newPosition);
                        }
                        //else if (move.z > 0)
                        //{
                        //	move.z++;
                        //	move += -State.climbingNormal*Data.WallJumpRange;
                        //	if (tryMoveTo(move, true, dt, newPosition, interpolate))
                        //	{
                        //		moved = true;
                        //		interpolate = true;
                        //	}
                        //}
                        else if (move.magnitude > 0 && (move.x != 0 || move.y != 0)) {
                            Vector3 newWallNormal = move.normalized;
                            move += -State.climbingNormal * Data.climbWallRange;
                            bool isWrapAroundOpen = CanMoveTo(move, true, dt, ref newPosition, ref interpolate);
                            if (isWrapAroundOpen && IsClimbPosition(newPosition, -newWallNormal * Data.climbWallRange)) {
                                State.climbingNormal = newWallNormal;
                                SetPosition(newPosition, 0.1f);
                            }
                        }
                        else {
                            State.velocity = Vector3.zero;
                        }
                    }
                    else if (IsClimbPosition(newPosition, move.normalized * Data.climbWallRange)) {
                        SetPosition(newPosition, 0.1f);
                    }
                }
            }

            if (!IsClimbPosition(State.position, -State.climbingNormal * Data.climbWallRange)) {
                State.activity = Activity.FALLING;
            }

        }

        public void SetPosition(Vector3 p, float interpolateTime=0) {
            if (interpolateTime > 0) {
                State.interpolateFrom = renderPosition();
                State.interpolateTime = State.interpolateTimeTotal = Math.Max(interpolateTime, State.interpolateTime);
            }
            State.position = p;
        }

        public bool IsWading() {
            return State.activity == Activity.ONGROUND && world.getBlock(waistPosition(State.position)) == EBlockType::BLOCK_TYPE_WATER && world.getBlock(State.position) != EBlockType::BLOCK_TYPE_WATER;
        }

        public float getGroundJumpVelocity() {
            float v = Data.jumpSpeed;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.Data.JumpChange;
            //	v -= (getWeight() - Data.BodyWeight) / 50f;
            return v;
        }
        public float getJumpBoostAcceleration() {
            float v = Data.jumpBoostAcceleration;
            return v;
        }
        public float getGroundMaxSpeed() {
            if (State.canRun) {
                return Data.groundMaxSpeed;
            }
            else {
                return Data.crouchSpeed;
            }
        }
        public float getGroundAcceleration() {
            float v = Data.groundAcceleration;
            return Math.Max(0, v);
        }


        public float getFallingAirFriction() {
            float v = 0;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.Data.AirFrictionVertical;
            //foreach(var i in Hands)
            //	if (i != null && i.Active)
            //		v += i.Data.AirFrictionVertical;
            return v;
        }

        float getHorizontalAirFriction() {
            float v = Data.fallDragHorizontal;
            //foreach(var i in Inventory)
            //	if (i != null && i.Active)
            //		v += i.Data.AirFrictionHorizontal;
            //foreach(var i in Hands)
            //	if (i != null && i.Active)
            //		v += i.Data.AirFrictionHorizontal;
            return v;
        }

        public Vector3 footPosition(Vector3 p) {
            return p + new Vector3(0, 0, -Data.height / 2 + 0.05f);
        }
        public Vector3 waistPosition(Vector3 p) {
            return p + new Vector3(0, 0, -0.05f);
        }
        public Vector3 headPosition(Vector3 p) {
            return p + new Vector3(0, 0, Data.height / 2 - 0.05f);
        }

        public Vector3 handPosition(Vector3 p) {
            return p + new Vector3(0, 0, Data.height / 2 - 0.05f);
        }

        public Vector3 renderPosition() {
            Vector3 p = State.position;
            if (State.interpolateTime <= 0)
                return p;
            return p + (State.interpolateFrom - p) * State.interpolateTime / State.interpolateTimeTotal;
        }

        public void useStamina(float s) {
            if (State.stamina <= 0)
                return;
            State.stamina -= s;
            State.recoveryTimer = Data.recoveryTime;
            if (State.stamina <= 0) {
                State.recovering = true;
            }
        }

        void jump(Vector3 dir) {
            useStamina(Data.jumpStaminaUse);
            State.dodgeTimer = State.dodgeTimer + Data.dodgeTime;
            State.velocity += dir;
        }

        void stun(float s) {
            // Can't stun further if already stunned
            if (State.stunned || s <= 0) {
                return;
            }

            State.stunAmount += s;
            if (State.stunAmount >= Data.stunLimit) {
                State.stunned = true;
                State.stunTimer = Data.stunRecoveryTime;
                foreach(var w in State.inventory)
                {
                    if (w != null) {
                        w->interrupt(this);
                    }
                }
            }
        }


        public void damage(float d) {
            State.health = State.health - d;
        }

        public void hit(Actor attacker, Item weapon, Item.CData.AttackData attackData) {
            float remainingStun;
            float remainingDamage;

            Vector3 dirToEnemy = (attacker.State.position - State.position).normalized;
            const float angleToEnemy = constrainAngle(Math.Atan2(dirToEnemy.y, dirToEnemy.x) - State.yaw);
            if (attackData.attackDamageBackstab > 0 && Math.Abs(angleToEnemy) < Data.backStabAngle) {
                remainingStun = attackData.stunPowerBackstab;
                remainingDamage = attackData.attackDamageBackstab;
            }
            else {
                remainingStun = attackData.stunPower;
                remainingDamage = attackData.attackDamage;
            }


            if (State.dodgeTimer > 0) {
                return;
            }

            // Check if we're blocking with shield
            foreach (var w in State.inventory) {
                if (w != null) {
                    w.defend(this, attacker, weapon, attackData, ref remainingStun, ref remainingDamage);
                }
            }

            if (remainingDamage > 0) {
                damage(remainingDamage);
            }

            if (remainingStun > 0) {

                if (attackData.knockback != 0) {
                    State.moveImpulseTimer = 0.1f;
                    var kb = (State.position - attacker.State.position);
                    kb.z = 0;
                    kb.Normalize();
                    State.moveImpulse = attackData.knockback * kb;
                }

                useStamina(attackData.staminaDrain);
                stun(remainingStun);
            }
        }
    }
}


