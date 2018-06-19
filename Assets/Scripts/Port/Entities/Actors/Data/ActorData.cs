﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    public class ActorData : EntityData {
        [Header("Basic")]
        public float height;
        public float collisionRadius;
        public float maxHealth;
        public float maxStamina;
        public float recoveryTime;
        public float staminaRechargeTime;
        public float dodgeTime;
        public float stunLimit;
        public float stunRecoveryTime;
        public float backStabAngle;

        [Header("Ground")]
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

        [Header("Falling")]
        public float gravity;
        public float fallJumpTime;
        public float fallAcceleration;
        public float fallDragHorizontal;
        public float fallMaxHorizontalSpeed;

        [Header("Climbing")]
        public float climbWallRange;
        public float climbGrabMinZVel;
        public float climbSpeed;

        [Header("Swimming")]
        public float bouyancy;
        public float swimJumpSpeed;
        public float swimSinkAcceleration;
        public float swimJumpBoostAcceleration;
        public float swimAcceleration;
        public float swimMaxSpeed;
        public float swimDragVertical;
        public float swimDragHorizontal;

    }


}