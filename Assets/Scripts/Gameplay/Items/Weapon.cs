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
		public float activeTime;
		public float chargeTime;
		public float cooldown;
		public bool attackWhenCooldownComplete;
		public List<Pawn> hitTargets = new List<Pawn>();

		#endregion

		DamageIndicator _damageIndicator;
        private GameObject _mesh;

		public override void Init(ItemData data) {
			base.Init(data);
		}

		public bool CanCast() {
			return castTime == 0 && activeTime == 0 && data.attacks.Length > 0 && cooldown <= data.attacks[attackHand].cooldownNextAttackQueueTime;
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
            if (data.prefab != null && owner != null) {
                if (newSlot == (int)Player.InventorySlot.LEFT_HAND || newSlot == (int)Player.InventorySlot.RIGHT_HAND) {
                    var prefab = data.prefab.Load();
                    _mesh = GameObject.Instantiate(prefab, owner.go.transform, false);
					UpdateAnimation(owner);
					owner.SetSilhouetteDirty();
                }
            }

			if (oldSlot >= 0 && oldSlot < (int)Player.InventorySlot.PACK) {
				if (data.maxStaminaBonus > 0) {
					owner.maxStamina -= data.maxStaminaBonus;
					owner.stamina = Mathf.Min(owner.stamina, owner.maxStamina);
				}
				if (data.maxHealthBonus > 0) {
					owner.maxHealth -= data.maxHealthBonus;
					owner.health = Mathf.Min(owner.health, owner.maxHealth);
				}
			}
			if (newSlot < (int)Player.InventorySlot.PACK) {
				if (data.maxStaminaBonus > 0) {
					owner.maxStamina += data.maxStaminaBonus;
				}
				if (data.maxHealthBonus > 0) {
					owner.maxHealth += data.maxHealthBonus;
				}
			}

		}
		public void Charge(float dt, int hand) {

			if (data.attacks.Length == 0) {
				return;
			}

			if (hand >= data.attacks.Length) {
				hand = 0;
			}
			if (attackHand != hand) {
				chargeTime = 0;
			}
			attackHand = hand;

			attackWhenCooldownComplete = false;
			if (CanCast()) {
				if (cooldown <= 0) {
					chargeTime += dt;
				}
			}
		}


		public bool Attack(Pawn owner) {

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

			hitTargets.Clear();


			if (data.waterUse > owner.water) {
				return false;
			}

            castTime = data.attacks[attackHand].castTime;
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
            var diff = enemy.waistPosition() - attackPos;
            float dist = diff.magnitude;
            if (dist <= data.attacks[attackHand].radius + critterRadius) {

				WeaponData.AttackResult attackType;
				float angleToEnemysBack = Mathf.Abs(Utils.SignedMinAngleDelta(Mathf.Atan2(diff.x, diff.z)*Mathf.Rad2Deg, enemy.yaw * Mathf.Rad2Deg));
				if (data.attacks[attackHand].canBackstab && angleToEnemysBack < enemy.data.backStabAngle) {
					attackType = data.attacks[attackHand].backstabResult;
				}
				else {
					attackType = data.attacks[attackHand].attackResult;
				}

				if (enemy.Hit(owner, this, attackType, GetMultiplier(owner, attackCharge), !data.attacks[attackHand].unblockable)) {
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

			castTime = 0;
            cooldown = data.attacks[attackHand].cooldown;

			if (data.attacks[attackHand].radius > 0) {
				_damageIndicator = GameObject.Instantiate(GameManager.instance.clientData.damageIndicatorPrefab);
				_damageIndicator.Init(data.attacks[attackHand].activeTime > 0 ? data.attacks[attackHand].activeTime : 0.05f, data.attacks[attackHand].radius*2);
			}

			if (data.attacks[attackHand].activeTime == 0) {
				DoActiveTick(owner);
			}
			else {
				activeTime = data.attacks[attackHand].activeTime;
			}
			
			if (data.projectile != null) {
				Pawn target = null;
				if (data.canTarget) {
					target = owner.GetAttackTarget(owner.yaw, data.projectile.lifetime * data.projectileSpeed, 60 * Mathf.Deg2Rad, null);
				}
				Vector3 dir;
				if (target != null) {
					dir = (target.waistPosition() - owner.headPosition()).normalized;
				} else {
					dir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
				}

				data.projectile.SpawnAndFireProjectile<Actors.Projectile>(owner.world, owner.headPosition(), dir * data.projectileSpeed, target, null, owner, owner.team);
			}

			if (data.spell != WeaponData.Spell.None) {
				ActivateSpell(owner);
			}

			owner.UseStamina(data.attacks[attackHand].staminaUse, false);
			owner.UseWater(data.waterUse);

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

		private void ActivateSpell(Pawn owner) {
			if (data.spell == WeaponData.Spell.Heal) {
				owner.health = Mathf.Min(owner.maxHealth, owner.health + data.spellPower);
			}
		}

        override public void Tick(float dt, Pawn owner) {

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
						Attack(owner);
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
			float angleToEnemy = Mathf.Abs(Utils.SignedMinAngleDelta(Mathf.Atan2(dirToEnemy.x, dirToEnemy.z) * Mathf.Rad2Deg, owner.yaw * Mathf.Rad2Deg))*Mathf.Rad2Deg;
			if (angleToEnemy > data.blockAngleRange*Mathf.Deg2Rad && Mathf.PI * 2 - angleToEnemy > data.blockAngleRange * Mathf.Deg2Rad) {
				return;
			}

			owner.UseStamina(data.blockResult.staminaUse, false);
			remainingDamage = Mathf.Max(0, remainingDamage - data.blockResult.damageAbsorb);
			remainingStun = Mathf.Max(0, remainingStun - data.blockResult.stunAbsorb);
			attacker.Hit(owner, this, data.blockResult, 1, false);

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
