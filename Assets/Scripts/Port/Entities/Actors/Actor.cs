using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {

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
        Count
    }

    public class Actor : Entity {

        #region State

        [Header("Basic")]
        public bool removeFlag;
        public bool spawned;
        public int team;

        [Header("Input")]
        public PlayerCmd_t cur;
        public PlayerCmd_t last;

        [Header("Physics")]
        public Vector3 position;
        public Vector3 velocity;
        public float yaw;
        public Vector3 moveImpulse;
        public float moveImpulseTimer;

        [Header("Stats")]
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
        public Actor.Activity activity;
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

        [Header("Combat")]
        public Actor attackTarget;
        public bool lockedToTarget;

        [Header("Inventory")]
        [SerializeField]
        protected Item[] _inventory = new Item[Actor.MaxInventorySize];

        #endregion


        public const int MaxInventorySize = 32;

        public enum Activity {
            Falling,
            Swimming,
            Climbing,
            OnGround,
        }





        public struct PlayerCmd_t {
            public int serverTime;
            public float[] angles;
            public int buttons;
            public sbyte fwd, right, up;
        };

        public class Input_t {
            public Vector3 movement = Vector3.zero;
            public float yaw = 0;
            public InputState[] inputs = new InputState[(int)InputType.Count];

            public bool IsPressed(InputType i) {
                return inputs[(int)i] == InputState.Pressed || inputs[(int)i] == InputState.JustPressed;
            }
            public bool inMotion;
            public Vector3 position;
            public Vector3 velocity;
        };



        new public ActorData Data { get { return GetData<ActorData>(); } }
        public static ActorData GetData(string dataName) { return DataManager.GetData<ActorData>(dataName); }

        public delegate void OnInventoryChangeFn();
        public event OnInventoryChangeFn OnInventoryChange;


        public void UpdatePlayerCmd(PlayerCmd_t cmd) {
            last = cur;
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
                    if (activity == Activity.OnGround) {
                        // Step down
                        Vector3 fp = footPoint + new Vector3(0, 0, -0.5f);
                        if (!World.IsSolidBlock(world.GetBlock(fp))) {
                            if (World.IsSolidBlock(world.GetBlock(fp + new Vector3(0, 0, -1)))) {
                                if (world.GetBlock(movePosition) != EBlockType.BLOCK_TYPE_WATER) {
                                    move.y -= 1;
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

                if (World.IsSolidBlock(world.GetBlock(footPoint)) && !World.IsSolidBlock(world.GetBlock(movePosition)) && !World.IsSolidBlock(world.GetBlock(headPoint))) {
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
            var handblock = world.GetBlock(handPosition(handHoldPos));
            bool isClimbable = World.IsClimbable(handblock, canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
                if (isHangPosition && World.IsHangable(handblock, canClimbWell) && !World.IsSolidBlock(world.GetBlock(p)) && !World.IsSolidBlock(world.GetBlock(handPosition(p))) && !World.IsSolidBlock(world.GetBlock(handPosition(handHoldPos) + Vector3.up))) {
                    isClimbable = true;
                }
            }
            if (velocity.y > Data.climbGrabMinZVel && isClimbable) {
                return true;
            }
            return false;
        }

        bool CanClimb(Vector3 checkDir, Vector3 checkPos) {
            var p = checkPos;
            var handHoldPos = p + checkDir.normalized * Data.climbWallRange;
            if (!IsOpen(p))
                return false;
            var handblock = world.GetBlock(handPosition(handHoldPos));
            bool isClimbable = World.IsClimbable(handblock, canClimbWell);
            if (!isClimbable) {
                bool isHangPosition = Mathf.Repeat(p.y, 1f) > 0.9f;
                if (isHangPosition
                    && World.IsHangable(handblock, canClimbWell)
                    && !World.IsSolidBlock(world.GetBlock(handPosition(handHoldPos)))
                    && !World.IsSolidBlock(world.GetBlock(handHoldPos + Vector3.up))) {
                    isClimbable = true;
                }
            }
            if (velocity.y > Data.climbGrabMinZVel && isClimbable) {
                return true;
            }
            return false;
        }

        Vector3 getClimbingVector(Vector3 i, Vector3 surfaceNormal) {
            var axis = Vector3.Cross(-Vector3.up, surfaceNormal);
            var rotation = Quaternion.AngleAxis(Mathf.PI / 2, axis);
            Matrix4x4 climbMatrix = Matrix4x4.Rotate(rotation);
            var climbingVector = climbMatrix.MultiplyVector(i);

            // If we're climbing nearly exaclty up, change it to up
            if (Math.Abs(climbingVector.y) > Math.Sqrt(Math.Pow(climbingVector.x, 2) + Math.Pow(climbingVector.z, 2))) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0, Math.Sign(climbingVector.y) * inputSpeed, 0);
            }
            else if (Math.Abs(climbingVector.x) > Math.Abs(climbingVector.z)) {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(Mathf.Sign(climbingVector.x), 0f, 0f) * inputSpeed;
            }
            else {
                float inputSpeed = climbingVector.magnitude;
                climbingVector = new Vector3(0f, 0f, Mathf.Sign(climbingVector.z)) * inputSpeed;
            }
            return climbingVector;
        }

        bool CheckFloor(Vector3 position, out float floorHeight) {
            floorHeight = 0;
            Vector3 floorPosition = footPosition(position) + new Vector3(0, -0.1f, 0);

            if (!World.IsSolidBlock(world.GetBlock(floorPosition))) {
                return false;
            }

            while (World.IsSolidBlock(world.GetBlock(floorPosition))) {
                floorPosition.y = Mathf.Floor(floorPosition.y) + 1;
            }
            floorHeight = floorPosition.y;
            return true;
        }

        bool IsOpen(Vector3 position) {
            return !World.IsSolidBlock(world.GetBlock(footPosition(position)))
                && !World.IsSolidBlock(world.GetBlock(waistPosition(position)))
                && !World.IsSolidBlock(world.GetBlock(headPosition(position)));
        }


        protected void Tick(float dt, Input_t input) {
            if (recoveryTimer > 0) {
                recoveryTimer = Math.Max(0, recoveryTimer - dt);
            }
            else {
                if (stamina < maxStamina) {
                    stamina = Math.Min(maxStamina, stamina + dt * maxStamina / Data.staminaRechargeTime);
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
                stunAmount = Math.Max(0, stunAmount - dt * Data.stunLimit / Data.stunRecoveryTime);
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
                    if (lockedToTarget) {
                        var diff = attackTarget.position - position;
                        yaw = Mathf.Atan2(diff.x, diff.z);
                    }
                    else if (input.movement != Vector3.zero) {
                        yaw = input.yaw;
                    }
                }
            }


            if (activity == Activity.Climbing) {
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

            position.y += velocity.y * dt;

            // Collide feet
            float floorPosition;
            bool onGround = CheckFloor(position, out floorPosition);
            if (onGround) {
                if (velocity.y <= 0) {

                    if (activity != Activity.OnGround) {
                        LandOnGround();
                        if (velocity.y < 0) {
                            Vector3 groundNormal = world.GetGroundNormal(footPosition(position));
                            float slopeAccel = 1f + Vector3.Dot(velocity.normalized, -groundNormal);
                            if (slopeAccel < 1f) {
                                velocity += velocity * slopeAccel;
                            }
                        }
                    }
                    velocity.y = Math.Max(0, velocity.y);
                }
                position.y = floorPosition + Data.height / 2;
            }

            // Collide head
            if (World.IsSolidBlock(world.GetBlock(headPosition(position)))) {
                // TODO: this is broken
                position.y = Math.Min(position.y, (int)headPosition(position).y - Data.height);
                velocity.y = Math.Min(0, velocity.y);
            }


            if (activity != Activity.Climbing) {
                // Collide XY
                Vector3 moveXZ = new Vector3(velocity.x, 0, velocity.z) * dt;
                Move(moveXZ, dt);
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
            else if (world.GetBlock(position) == EBlockType.BLOCK_TYPE_WATER) {
                activity = Activity.Swimming;
            }
            else if (onGround && velocity.y <= 0) {
                activity = Activity.OnGround;

                //if (Crouched)
                //{
                //	bool interpolate = false;
                //	Vector3 climbDownPos = position + Vector3(0, 0, -2f);
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
                        climbingNormal = -new Vector3(Mathf.Sign(firstCheck.x), 0, Mathf.Sign(firstCheck.z));
                        velocity = Vector3.zero;
                        activity = Activity.Climbing;
                    }
                    else if (secondCheck.magnitude > 0 && CanClimb(secondCheck, position)) {
                        climbingNormal = -new Vector3(Mathf.Sign(secondCheck.x), 0, Mathf.Sign(secondCheck.z));
                        velocity = Vector3.zero;
                        activity = Activity.Climbing;
                    }
                }

            }

            transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
        }

        public bool Move(Vector3 moveXZ, float dt) {
            float moveXZLength = moveXZ.magnitude;
            if (moveXZLength > 0) {
                bool interpolate = false;
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

                if (CanMoveTo(moveXZ, true, dt, ref newPosition, ref interpolate)) {
                    SetPosition(newPosition, interpolate ? 0.1f : 0);
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
            if (fallJumpTimer > 0) {
                fallJumpTimer = Math.Max(0, fallJumpTimer - dt);
                if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                    if (canJump) {
                        var jumpDir = input.movement * Data.dodgeSpeed;
                        jumpDir.y += getGroundJumpVelocity();
                        jump(jumpDir);
                    }
                    fallJumpTimer = 0;
                }
            }
            if (input.IsPressed(InputType.Jump)) {
                if (velocity.y >= 0) {
                    velocity.y += Data.jumpBoostAcceleration * dt;
                }
            }
            velocity.y += Data.gravity * dt;

            if (input.movement != Vector3.zero) {
                float acceleration = Data.fallAcceleration;
                float maxSpeed = getGroundMaxSpeed();
                maxSpeed = Mathf.Clamp(maxSpeed, Data.fallMaxHorizontalSpeed, Math.Max(maxHorizontalSpeed, Data.fallMaxHorizontalSpeed));

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

            var wind = world.GetWind(position);
            var velDiff = (wind - new Vector3(velocity.x, 0, velocity.z));
            velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
            velocity += velDiff * dt * getHorizontalAirFriction();

        }

        private void UpdateGround(float dt, Input_t input) {
            if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                if (canJump) {
                    var jumpDir = input.movement * Data.dodgeSpeed;
                    jumpDir.y += getGroundJumpVelocity();
                    jump(jumpDir);
                }
                fallJumpTimer = 0;
            }
            else {
                fallJumpTimer = Data.fallJumpTime;
            }
            var block = world.GetBlock(footPosition(position));
            var midblock = world.GetBlock(waistPosition(position));
            var topblock = world.GetBlock(headPosition(position));
            float slideFriction, slideThreshold;
            World.GetSlideThreshold(block, midblock, topblock, out slideFriction, out slideThreshold);
            float workModifier = World.GetWorkModifier(block, midblock, topblock);
            if (IsWading()) {
                workModifier += Data.swimDragHorizontal;
            }

            Vector3 groundNormal = world.GetGroundNormal(footPosition(position));


            float curVel = velocity.magnitude;
            Vector3 velChange = Vector3.zero;

            if (input.movement == Vector3.zero && curVel < Data.walkSpeed && !sliding) {
                if (curVel > 0) {
                    // Stopping (only when not sliding)
                    float stopAccel = dt * Data.walkSpeed / Data.walkStopTime;
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
                        sliding = true;
                    }
                    else {
                        sliding = false;
                    }
                }
                {
                    // Wind blowing you while running or skidding
                    var wind = world.GetWind(position);
                    var velDiff = (wind - new Vector3(velocity.x, 0, velocity.z));
                    velDiff *= velDiff.magnitude; // drag is exponential -- thanks zeno!!!
                    velChange += velDiff * dt * getHorizontalAirFriction();
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
                    velChange += (1.0f - slopeDot) * dt * -Data.gravity * slideDir * (1f - slideFriction);
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
                        var jumpDir = input.movement * Data.dodgeSpeed;
                        jumpDir.y += Data.swimJumpBoostAcceleration;
                        jump(jumpDir);
                    }
                }
                velocity.y = Math.Min(velocity.y + Data.swimMaxSpeed * dt, Data.swimJumpSpeed);
            }
            if (input.inputs[(int)InputType.Crouch] == InputState.Pressed) {
                velocity.y = velocity.y - Data.swimSinkAcceleration * dt;
            }
            velocity.y += Data.gravity * dt;
            if (world.GetBlock(headPosition(position)) == EBlockType.BLOCK_TYPE_WATER) {
                velocity.y += -velocity.y * dt * Data.swimDragVertical;
                velocity.y += Data.bouyancy * dt;
            }
            if (world.GetBlock(position) == EBlockType.BLOCK_TYPE_WATER) {
                velocity.y += -velocity.y * dt * Data.swimDragVertical;
                velocity.y += Data.bouyancy * dt;
            }

            var current = world.GetCurrent((int)position.x, (int)position.y, (int)position.z);
            var velDiff = (current - new Vector3(velocity.x, 0, velocity.z));
            velDiff *= velDiff.magnitude; // drag is exponential, thanks zeno!
            velocity += velDiff * dt * Data.swimDragHorizontal;

            if (input.movement == Vector3.zero) {
            }
            else {

                var normalizedInput = input.movement.normalized;
                var normalizedVelocity = velocity / Data.swimMaxSpeed;
                float dot = Vector3.Dot(normalizedInput, normalizedVelocity);
                float accelerationPotential = Math.Min(1.0f, 1.0f - dot);

                velocity += input.movement * Data.swimAcceleration * accelerationPotential * dt;
            }

            maxHorizontalSpeed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);

        }

        private void UpdateClimbing(float dt, Input_t input) {

            maxHorizontalSpeed = Data.groundMaxSpeed;

            if (input.inputs[(int)InputType.Jump] == InputState.JustPressed) {
                activity = Activity.Falling;


                Vector3 climbingInput = getClimbingVector(input.movement, climbingNormal);
                if (canJump) {
                    Vector3 jumpDir = Vector3.zero;
                    if (climbingInput.y < 0) {
                        // Push away from wall
                        jumpDir += input.movement * Data.groundMaxSpeed;
                        jumpDir.y += Data.jumpSpeed;
                    }
                    else {
                        if (climbingInput.y > 0) {
                            // jumping up jumps away from the wall slightly so we don't reattach right away
                            jumpDir += climbingInput * Data.jumpSpeed;
                            jumpDir += climbingNormal * Data.jumpSpeed / 2;
                        }
                        else if (climbingInput.y >= 0) {
                            // left right jumps get a vertical boost
                            jumpDir.y = Data.jumpSpeed;
                        }
                    }
                    jump(jumpDir);
                    return;
                }
            }


            if (!input.IsPressed(InputType.Crouch) || input.IsPressed(InputType.Jump)) {
                var climbingInput = getClimbingVector(input.movement, climbingNormal);
                velocity = climbingInput * Data.climbSpeed;
            }

            var vertMovePosition = new Vector3(position.x, position.y + velocity.y * dt, position.z);

            // collide feet
            float floorPosition;
            bool onGround = CheckFloor(vertMovePosition, out floorPosition);
            if (onGround) {
                activity = Activity.OnGround;
                velocity.y = Math.Max(0, velocity.y);
                position.y = floorPosition + 1;
            }
            else {
                bool interpolate = false;
                Vector3 move = velocity * dt;
                Vector3 newPosition = position + move;

                if (move.magnitude > 0) {

                    bool isOpen = CanMoveTo(move, true, dt, ref newPosition, ref interpolate);
                    if (isOpen) {
                        if (IsClimbPosition(newPosition, -climbingNormal * Data.climbWallRange)) {
                            SetPosition(newPosition);
                        }
                        //else if (move.y > 0)
                        //{
                        //	move.y++;
                        //	move += -climbingNormal*Data.WallJumpRange;
                        //	if (tryMoveTo(move, true, dt, newPosition, interpolate))
                        //	{
                        //		moved = true;
                        //		interpolate = true;
                        //	}
                        //}
                        else if (move.magnitude > 0 && (move.x != 0 || move.z != 0)) {
                            Vector3 newWallNormal = move.normalized;
                            move += -climbingNormal * Data.climbWallRange;
                            bool isWrapAroundOpen = CanMoveTo(move, true, dt, ref newPosition, ref interpolate);
                            if (isWrapAroundOpen && IsClimbPosition(newPosition, -newWallNormal * Data.climbWallRange)) {
                                climbingNormal = newWallNormal;
                                SetPosition(newPosition, 0.1f);
                            }
                        }
                        else {
                            velocity = Vector3.zero;
                        }
                    }
                    else if (IsClimbPosition(newPosition, move.normalized * Data.climbWallRange)) {
                        SetPosition(newPosition, 0.1f);
                    }
                }
            }

            if (!IsClimbPosition(position, -climbingNormal * Data.climbWallRange)) {
                activity = Activity.Falling;
            }

        }

        public void SetPosition(Vector3 p, float interpolateTime = 0) {
            if (interpolateTime > 0) {
                interpolateFrom = renderPosition();
                interpolateTime = interpolateTimeTotal = Math.Max(interpolateTime, interpolateTime);
            }
            position = p;
        }

        public bool IsWading() {
            return activity == Activity.OnGround && world.GetBlock(waistPosition(position)) == EBlockType.BLOCK_TYPE_WATER && world.GetBlock(position) != EBlockType.BLOCK_TYPE_WATER;
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
            if (canRun) {
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
            return p + new Vector3(0, -Data.height / 2 + 0.05f, 0);
        }
        public Vector3 waistPosition(Vector3 p) {
            return p + new Vector3(0, -0.05f, 0);
        }
        public Vector3 headPosition(Vector3 p) {
            return p + new Vector3(0, Data.height / 2 - 0.05f, 0);
        }

        public Vector3 handPosition(Vector3 p) {
            return p + new Vector3(0, Data.height / 2 - 0.05f, 0);
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
            recoveryTimer = Data.recoveryTime;
            if (stamina <= 0) {
                recovering = true;
            }
        }

        void jump(Vector3 dir) {
            useStamina(Data.jumpStaminaUse);
            dodgeTimer = dodgeTimer + Data.dodgeTime;
            velocity += dir;
        }

        public void stun(float s) {
            // Can't stun further if already stunned
            if (stunned || s <= 0) {
                return;
            }

            stunAmount += s;
            if (stunAmount >= Data.stunLimit) {
                stunned = true;
                stunTimer = Data.stunRecoveryTime;
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
        }

        public void hit(Actor attacker, Item weapon, WeaponData.AttackData attackData) {
            float remainingStun;
            float remainingDamage;

            Vector3 dirToEnemy = (attacker.position - position).normalized;
            float angleToEnemy = Mathf.Repeat(Mathf.Atan2(dirToEnemy.x, dirToEnemy.z) - yaw, Mathf.PI * 2);
            if (attackData.attackDamageBackstab > 0 && Math.Abs(angleToEnemy) < Data.backStabAngle) {
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
            foreach (var w in getInventory()) {
                var shield = w as Weapon;
                if (shield != null) {
                    shield.defend(this, attacker, weapon, attackData, ref remainingStun, ref remainingDamage);
                }
            }

            if (remainingDamage > 0) {
                damage(remainingDamage);
            }

            if (remainingStun > 0) {

                if (attackData.knockback != 0) {
                    moveImpulseTimer = 0.1f;
                    var kb = (position - attacker.position);
                    kb.y = 0;
                    kb.Normalize();
                    moveImpulse = attackData.knockback * kb;
                }

                useStamina(attackData.staminaDrain);
                stun(remainingStun);
            }
        }


        virtual public void LandOnGround() {

        }


        public Item[] getInventory() {
            return _inventory;
        }

        public void SetInventorySlot(int index, Item item) {
            _inventory[index] = item;
            OnInventoryChange?.Invoke();
        }

        public Item GetInventorySlot(int index) {
            return _inventory[index];
        }
    }
}


