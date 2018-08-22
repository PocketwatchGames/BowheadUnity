using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
	using Player = Actors.Player;
	using Pawn = Actors.Pawn;
    public class Weapon : Item<Weapon, WeaponData> {

        #region State

        public int attackHand;
		public float attackCharge;
        public float castTime;
		public Pawn target;
		public float activeTime;
		public float chargeTime;
		public float cooldown;
		public bool attackWhenCooldownComplete;
		public float stamina;
		public float staminaRechargeTimer;
		public bool stunned;

		public List<Pawn> hitTargets = new List<Pawn>();

		#endregion

		DamageIndicator _damageIndicator;
        private GameObject _mesh;

		public override void Init(ItemData data) {
			base.Init(data);
		}

		public bool CanCast() {
			return stamina > 0 && !stunned && castTime == 0 && activeTime == 0 && data.attacks.Length > 0 && cooldown <= data.attacks[attackHand].cooldownNextAttackQueueTime;
		}

		public override void OnSlotChange(int newSlot, int oldSlot, Pawn owner) {
            castTime = 0;
            chargeTime = 0;
			activeTime = 0;
			attackHand = 0;

            if (_mesh != null) {
                GameObject.Destroy(_mesh);
				_mesh = null;
            }
            if (data.prefab != null && owner != null && owner.go != null) {
                if (newSlot == (int)Player.InventorySlot.LEFT_HAND || newSlot == (int)Player.InventorySlot.RIGHT_HAND) {
                    var prefab = data.prefab.Load();
					if (prefab != null) {
						_mesh = GameObject.Instantiate(prefab, owner.go.transform, false);
						UpdateAnimation(owner);
						owner.SetSilhouetteDirty();
					}
                }
            }

			if (oldSlot >= 0 && oldSlot < (int)Player.InventorySlot.PACK) {
				foreach (var t in data.traits) {
					t.Remove(owner);
				}
			}
			if (newSlot < (int)Player.InventorySlot.PACK) {
				foreach (var t in data.traits) {
					t.Add(owner);
				}
			}

		}
		public void Charge(float dt, int attackIndex) {

			if (data.attacks.Length == 0) {
				return;
			}

			if (attackIndex >= data.attacks.Length) {
				return;
			}
			if (attackHand != attackIndex) {
				chargeTime = 0;
			}
			attackHand = attackIndex;

			if (data.staminaUseDuringCharge > 0) {
				staminaRechargeTimer = data.staminaRechargePause;
				stamina -= data.staminaUseDuringCharge * dt;
			}

			attackWhenCooldownComplete = false;
			if (CanCast()) {
				if (cooldown <= 0) {
					chargeTime += dt;
				}
			}
		}


		public bool Attack(Pawn owner, int attackIndex) {

			if (data.attacks.Length == 0) {
				return false;
			}

            if (!CanCast()) {
                return false;
            }

			if (cooldown > 0) {
				attackWhenCooldownComplete = true;
				return true;
			}

			if (attackIndex >= data.attacks.Length) {
				return false;
			}
			if (attackHand != attackIndex) {
				chargeTime = 0;
			}
			attackHand = attackIndex;
			var attack = data.attacks[attackHand];

			hitTargets.Clear();


			target = null;
			if (attack.canTarget) {
				target = owner.GetAttackTarget(owner.yaw, attack.range, 60 * Mathf.Deg2Rad, null);
			}
			staminaRechargeTimer = data.staminaRechargePause;
			stamina -= attack.staminaUse;
			if (stamina <= 0) {
				stunned = true;
			}
			castTime = attack.castTime;
			attackCharge = chargeTime;
			chargeTime = 0;
            if (castTime <= 0) {
                Activate(owner);
            }
            return true;
        }

        bool CheckIfHit(Pawn owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, Pawn enemy) {

            Debug.Assert(owner != enemy);

            if (!enemy.active) {
                return false;
            }

            float critterRadius = 1.0f;
            float dist = (enemy.waistPosition() - attackPos).magnitude;
            if (dist <= data.attacks[attackHand].radius + critterRadius) {

				bool directHit = true;
				WeaponData.AttackResult attackType;
				Vector2 diffXZ = new Vector2(enemy.position.x - owner.position.x, enemy.position.z - owner.position.z);
				float angleToEnemysBack = Mathf.Abs(Utils.SignedMinAngleDelta(Mathf.Atan2(diffXZ.x, diffXZ.y)*Mathf.Rad2Deg, enemy.yaw * Mathf.Rad2Deg));
				if (data.attacks[attackHand].canBackstab && angleToEnemysBack < enemy.data.backStabAngle) {
					attackType = data.attacks[attackHand].attackResultBackstab;
				}
				else {

					Vector2 attackDirXZ = new Vector2(attackPos.x-owner.position.x, attackPos.z-owner.position.z);
					float angleDelta = Mathf.DeltaAngle(Mathf.Rad2Deg * Mathf.Atan2(diffXZ.y, diffXZ.x), Mathf.Rad2Deg * Mathf.Atan2(attackDirXZ.y, attackDirXZ.x));
					float enemyAngleWidth = Mathf.Rad2Deg * Mathf.Atan2(critterRadius,diffXZ.magnitude);
					if (Mathf.Abs(angleDelta) > enemyAngleWidth*enemy.data.directHitWidth) {
						attackType = data.attacks[attackHand].attackResultGlancingBlow;
						directHit = false;
					} else {
						attackType = data.attacks[attackHand].attackResultDirectHit;
					}
				}

				if (enemy.Hit(owner, this, attackType, GetMultiplier(owner, attackCharge), !attackType.unblockable, directHit)) {
					Client.Actors.ClientPlayerController.localPlayer.cameraController.Shake(0.15f, 0.05f, 0.01f);
					if (data.attacks[attackHand].interruptOnHit) {
						Interrupt(owner);
					}
					return true;
				}
            }
            return false;
        }
        void Activate(Pawn owner) {

			Vector3 attackDir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
			float stepAmt = data.attacks[attackHand].stepDistance;
			if (stepAmt != 0 && owner.activity == Pawn.Activity.OnGround) {
				owner.moveImpulse = attackDir * stepAmt;
				owner.moveImpulseTimer = data.attacks[attackHand].activeTime;
				owner.velocity = Vector3.zero;
			}

			var attack = data.attacks[attackHand];

			castTime = 0;
            cooldown = attack.cooldown;

			if (attack.radius > 0) {
				_damageIndicator = GameObject.Instantiate(GameManager.instance.clientData.damageIndicatorPrefab);
				_damageIndicator.Init(attack.activeTime > 0 ? attack.activeTime : 0.05f, attack.radius*2);
			}

			if (attack.activeTime == 0) {
				DoActiveTick(owner);
			}
			else {
				activeTime = attack.activeTime;
			}

			if (attack.projectiles != null) {
				foreach (var p in attack.projectiles) {
					Vector3 dir;
					if (target == null) {
						dir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
					} else if (p.autoAimPitch && p.autoAimYaw) {
						dir = (target.waistPosition() - owner.headPosition()).normalized;
					} else if (p.autoAimPitch) {
						var diff = target.waistPosition() - owner.headPosition();
						float pitch = Mathf.Atan2(diff.y, Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z));
						var cosPitch = Mathf.Cos(pitch);
						dir = new Vector3(cosPitch * Mathf.Sin(owner.yaw), Mathf.Sin(pitch), cosPitch * Mathf.Cos(owner.yaw));
					} else {
						dir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
					}

					var pEnt = (Actors.Projectile)owner.world.Spawn(typeof(Actors.Projectile), null, default(SpawnParameters));
					pEnt.Spawn(p, owner.headPosition(), dir * p.speed, target, null, owner, owner.team);
				}
			}

        }

		public Pawn GetProjectileTarget(Pawn owner, float yaw, float range) {
			Pawn bestTarget = null;
			float maxDist = range;
			float maxTargetAngle = 30 * Mathf.Deg2Rad;

			float bestTargetAngle = maxTargetAngle;
			foreach (var c in owner.world.GetActorIterator<Actors.Pawn>()) {
				if (c.active) {
					if (c.team != owner.team) {
						var diff = c.position - owner.position;
						float dist = diff.magnitude;
						if (dist < maxDist) {
							float angleToEnemy = Mathf.Atan2(diff.x, diff.z);

							float yawDiff = Mathf.Abs(Utils.SignedMinAngleDelta(angleToEnemy * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg)) * Mathf.Deg2Rad;

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

        override public void Tick(float dt, Pawn owner) {

			if (castTime == 0 && activeTime == 0 && cooldown == 0) {
				if (staminaRechargeTimer > 0) {
					staminaRechargeTimer = Mathf.Max(0, staminaRechargeTimer - dt / Mathf.Max(0.0001f, data.staminaRechargePause));
				} else {
					stamina = Mathf.Min(1.0f, stamina + dt / Mathf.Max(0.0001f, data.staminaRechargeTime));
					if (stunned && stamina == 1) {
						stunned = false;
					}
				}
			}


			if (castTime > 0) {
				castTime = Mathf.Max(0, castTime - dt);
                if (castTime <= 0) {
                    Activate(owner);
                }
				if (!data.attacks[attackHand].canMoveDuringCast) {
					owner.canMove = false;
					owner.canTurn = false;
				}
			}

			if (activeTime > 0) {
				activeTime = Mathf.Max(0, activeTime - dt);
				DoActiveTick(owner);
				if (!data.attacks[attackHand].canMoveDuringActive) {
					owner.canMove = false;
					owner.canTurn = false;
				}
			}

			if (activeTime <= 0) {
				if (cooldown > 0) {
					cooldown = Mathf.Max(0, cooldown - dt);
					if (cooldown > 0 && !data.attacks[attackHand].canMoveDuringCooldown) {
						owner.canMove = false;
						owner.canTurn = false;
					}
				}
				else {
					if (attackWhenCooldownComplete) {
						attackWhenCooldownComplete = false;
						Attack(owner, attackHand);
					}
				}
			}


			UpdateAnimation(owner);

			
			var mesh = _mesh?.GetChildComponent<MeshRenderer>("Mesh");
			if (mesh != null) {
				mesh.material.color = Color.white;
			}

		}

		private void DoActiveTick(Pawn owner) {
			Vector3 attackDir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
			Vector3 attackPos = owner.waistPosition() + attackDir * data.attacks[attackHand].range;
			bool hit = false;

			foreach (var c in owner.world.GetActorIterator<Pawn>()) {
				if (c.team != owner.team && c.active && !hitTargets.Contains(c)) {
					if (CheckIfHit(owner, attackPos, attackDir, owner.position, c)) {
						hit = true;
						hitTargets.Add(c);
					}
				}
			}

			if (_damageIndicator != null) {
				_damageIndicator.Tick(attackPos, hit);
			}
		}

		private void UpdateAnimation(Pawn owner) {
			if (_mesh == null) {
				return;
			}

			if (owner.activity == Pawn.Activity.Climbing || owner.activity == Pawn.Activity.Swimming) {
				_mesh.transform.localRotation = Quaternion.Euler(0, 0, 45);
				_mesh.transform.localPosition = new Vector3(0,1f,-0.75f);
				return;
			}

			Vector3 pos = new Vector3((data.hand == WeaponData.Hand.RIGHT || attackHand==1) ? 0.35f : -0.35f, 0, 0);

			if (chargeTime > 0) {
				_mesh.transform.localRotation = Quaternion.Euler(-45, 0, 0);
				pos += new Vector3(0, 1.5f, 0.25f);
			}
			else if (castTime > 0) {
				_mesh.transform.localRotation = Quaternion.Euler(-90, 0, 0);
				pos += new Vector3(0, 2, 0.25f);
			}
			else if (activeTime > 0) {
				_mesh.transform.localRotation = Quaternion.Euler(90, 0, 0);
				pos += new Vector3(0, 1, 0.25f);

			}
			else if (cooldown > 0) {
				_mesh.transform.localRotation = Quaternion.Euler(135, 0, 0);
				pos += new Vector3(0, 0.75f, 0.25f);
			}
			else {
				_mesh.transform.localRotation = Quaternion.Euler(0, -90, 0);
				pos += new Vector3(0, 1, 0.25f);
			}

			_mesh.transform.localPosition = pos;
		}

		public void Interrupt(Pawn owner) {

            if (activeTime > 0) {
                owner.moveImpulseTimer = 0;
            }
            chargeTime = 0;
            castTime = 0;

        }
        public void Defend(Pawn owner, Pawn attacker, Weapon attackerWeapon, ref float remainingStun, ref float remainingDamage) {

			bool isDefend = (data.hand == WeaponData.Hand.LEFT || (data.hand==WeaponData.Hand.BOTH && attackHand == 0)) && chargeTime > 0;
			if (!isDefend) {
				return;
			}

			var dirToEnemy = (attacker.rigidBody.position - owner.rigidBody.position);
			if (dirToEnemy == Vector3.zero) {
				dirToEnemy.x = 1;
			}
			else {
				dirToEnemy.Normalize();
			}
			float angleToEnemy = Mathf.Abs(Utils.SignedMinAngleDelta(Mathf.Atan2(dirToEnemy.x, dirToEnemy.z) * Mathf.Rad2Deg, owner.yaw * Mathf.Rad2Deg));
			if (angleToEnemy > data.blockAngleRange) {
				return;
			}

			WeaponData.DefendResult blockResult = data.blockResultDirectHit;

			remainingDamage = Mathf.Max(0, remainingDamage - blockResult.damageAbsorb);

			float stunAbsorb = Mathf.Min(blockResult.stunAbsorb, remainingStun);
			remainingStun -= stunAbsorb;

			stamina -= stunAbsorb;
			stamina -= blockResult.staminaUse;
			staminaRechargeTimer = data.staminaRechargePause;
			if (stamina <= 0) {
				stunned = true;
			}


			attacker.Hit(owner, this, blockResult, 1, false, true);

        }

		public float GetChargeMultiplier(Pawn owner, float charge) {
			return 1;
		}

		public float GetMultiplier(Pawn owner, float charge) {
			float m = owner.damageMultiplier;

			float chargeMultiplier = 1;

			if (Vector2.Dot(owner.velocity, new Vector2(Mathf.Sin(owner.yaw),Mathf.Cos(owner.yaw))) > owner.data.sprintDamageMultiplierSpeed) {
				chargeMultiplier = 2;
			}
			else if (owner.activity == Pawn.Activity.Falling) {
				chargeMultiplier = 2;
			}
			else if (data.attacks[attackHand].chargeTime > 0 && attackCharge > 0) {
				chargeMultiplier = Mathf.Clamp(Mathf.FloorToInt(charge / data.attacks[attackHand].chargeTime) * 2, 1, data.attacks[attackHand].maxCharge);
			}
		
			if (chargeMultiplier > 1) {
				return m * chargeMultiplier;
			}
			return m;
		}

    }
}
