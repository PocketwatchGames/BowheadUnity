using System;
using UnityEngine;

namespace Bowhead.Actors {

    public abstract class PawnData : EntityData {
		[Header("Basic")]
        public float height;
        public float maxHealth;
		public float maxWater;
        public float maxStamina;
        public float recoveryTime;
        public float staminaRechargeTime;
        public float dodgeTime;
        public float stunLimit;
        public float stunRecoveryTime;
        public float backStabAngle;
        public ParticleSystem bloodParticle;

        [Header("Ground")]
        public float jumpSpeed;
        public float dodgeSpeed;
        public float jumpStaminaUse;
        public float groundAcceleration; // accel = veldiff * groundAccel * dt
        public float crouchSpeed;
        public float walkSpeed;
        public float walkStartTime;
        public float walkStopTime;
		public float sprintTime;
		public float sprintSpeed;
		public float sprintStaminaUse;
		public float sprintGracePeriodTime;
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

		new public static PawnData Get(string name) {
			return DataManager.GetData<PawnData>(name);
		}
    }

	public abstract class PawnData<T> : PawnData where T : PawnData<T> {

		new public static T Get(string name) {
			return DataManager.GetData<T>(name);
		}
	}
}