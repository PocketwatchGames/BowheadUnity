using System;
using UnityEngine;

namespace Bowhead.Actors {

    public abstract class PawnData : EntityData {

		public enum DamageType {
			Blade,
			Bludgeon,
			Falling,
			Poison,
			Fire
		}

		[Header("Basic")]
		public GameObject_WRef mapMarker;
		public Client.UI.EMapMarkerStyle mapMarkerStyle;
		public SilhouetteRenderer.Mode defaultSilhouetteMode;
		public float height;
        public float maxHealth;
		public float maxWater;
        public float maxStamina;
		public float minStamina;
        public float recoveryTime;
        public float staminaRechargeTime;
		public float staminaRechargeTimeDuringRecovery;
		public float dodgeTime;
		public float postStunInvincibilityTime;
		public float backStabAngle;
		public float runSound;
		public float sprintSound;
		public ParticleSystem bloodParticle;

        [Header("Ground")]
        public float jumpSpeed;
        public float jumpStaminaUse;
		public float jumpHorizontalSpeed;
        public float groundAcceleration; // accel = veldiff * groundAccel * dt
        public float crouchSpeed;
        public float walkSpeed;
        public float walkStartTime;
        public float walkStopTime;
		public float groundMaxSpeed;
        public float groundWindDrag;

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
		public float climbAttachCooldown;

        [Header("Swimming")]
        public float bouyancy;
        public float swimJumpSpeed;
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