using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead {
	using Player = Actors.Player;
	using Pawn = Actors.Pawn;
    public class Weapon : Item<Weapon, WeaponData> {

        #region State

        public int attackType;
        public float castTime;
		public float activeTime;
        public float cooldown;
        public float chargeTime;
		public List<Pawn> hitTargets = new List<Pawn>();

		#endregion

		private DamageIndicator _damageIndicator;
        private GameObject _mesh;

		public override void Init(ItemData data) {
			base.Init(data);

			_damageIndicator = GameObject.Instantiate<DamageIndicator>(GameManager.instance.clientData.damageIndicatorPrefab);

		}

		public bool CanCast() {
			return castTime == 0 && activeTime == 0 && cooldown == 0;
		}

        public override void OnSlotChange(int newSlot, Pawn owner) {
            castTime = 0;
            chargeTime = 0;
			activeTime = 0;

            if (_mesh != null) {
                GameObject.Destroy(_mesh);
            }
            if (data.prefab != null && owner != null) {
                if (newSlot == (int)Player.InventorySlot.LEFT_HAND || newSlot == (int)Player.InventorySlot.RIGHT_HAND) {
                    var prefab = data.prefab.Load();
                    _mesh = GameObject.Instantiate(prefab, owner.go.transform, false);
					UpdateAnimation();
                }
            }
        }
        public void Charge(float dt) {
            if (CanCast()) {
				chargeTime += dt;
			}
		}

		public bool Attack(Pawn owner) {
            if (!CanCast()) {
                return false;
            }

			hitTargets.Clear();
			attackType = getCurCharge();
            var attackData = getCurAttackData();

			if (attackData.waterUse > owner.water) {
				return false;
			}

            castTime = attackData.castTime;
            chargeTime = 0;
            if (castTime <= 0) {
                Activate(owner);
            }
            return true;
        }

        bool CheckIfHit(Pawn owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, WeaponData.AttackData attackData, Pawn enemy) {

            Debug.Assert(owner != enemy);

            if (!enemy.active) {
                return false;
            }

            float critterRadius = 1.0f;
            var diff = enemy.waistPosition() - attackPos;
            float dist = diff.magnitude;
            if (dist <= attackData.attackRadius + critterRadius) {

                enemy.hit(owner, this, attackData);

                if (attackType == 0) {
                    Client.Actors.ClientPlayerController.localPlayer.cameraController.Shake(0.15f, 0.05f, 0.01f);
                }
                else {
                    Client.Actors.ClientPlayerController.localPlayer.cameraController.Shake(0.2f, 0.2f, 0.05f);
                }
                return true;
            }
            return false;
        }
        void Activate(Pawn owner) {
            var d = getCurAttackData();

			Vector3 attackDir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
			float stepAmt = d.stepDistance;
			if (stepAmt != 0 && owner.activity == Pawn.Activity.OnGround) {
				owner.moveImpulse = attackDir * stepAmt;
				owner.moveImpulseTimer = d.activeTime;
				owner.velocity = Vector3.zero;
			}

			castTime = 0;
            cooldown = d.cooldown;
			_damageIndicator.Init(d.activeTime > 0 ? d.activeTime : 0.05f, d.attackRadius);

			if (d.activeTime == 0) {
				DoActiveTick(owner);
			}
			else {
				activeTime = d.activeTime;
			}
			
			if (d.projectile != null) {
				d.projectile.SpawnAndFireProjectile<Actors.Projectile>(owner.world, owner.position + new Vector3(0, 0.5f, 0), new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw)) * d.projectileSpeed, null, null, owner.team);
			}

			if (d.spell != WeaponData.Spell.None) {
				ActivateSpell(d, owner);
			}

			owner.useStamina(d.staminaUse);
			owner.useWater(d.waterUse);

        }

		private void ActivateSpell(WeaponData.AttackData d, Pawn owner) {
			if (d.spell == WeaponData.Spell.Heal) {
				owner.health = Mathf.Min(owner.maxHealth, owner.health + d.spellPower);
			}
		}

        override public void Tick(float dt, Pawn owner) {

            if (cooldown > 0) {
                cooldown = Mathf.Max(0, cooldown - dt);
                if (!getCurAttackData().canMove) {
					owner.canMove = false;
					owner.canTurn = false;
                }
            }
            if (castTime > 0) {
				castTime = Mathf.Max(0, castTime - dt);
                if (castTime <= 0) {
                    Activate(owner);
                }
				if (!getCurAttackData().canMove) {
					owner.canMove = false;
					owner.canTurn = false;
				}
			}
			if (activeTime > 0) {
				activeTime = Mathf.Max(0, activeTime - dt);
				DoActiveTick(owner);
				if (!getCurAttackData().canMove) {
					owner.canMove = false;
					owner.canTurn = false;
				}
			}

			if (chargeTime > 0 && data.chargeTime > 0) {
				owner.canRun = false;
            }

			UpdateAnimation();

        }

		private void DoActiveTick(Pawn owner) {
			var d = getCurAttackData();
			Vector3 attackDir = new Vector3(Mathf.Sin(owner.yaw), 0, Mathf.Cos(owner.yaw));
			Vector3 attackPos = owner.waistPosition() + attackDir * d.attackRange;
			bool hit = false;

			foreach (var c in owner.world.GetActorIterator<Pawn>()) {
				if (c.team != owner.team && c.active && !hitTargets.Contains(c)) {
					if (CheckIfHit(owner, attackPos, attackDir, owner.position, d, c)) {
						hit = true;
						hitTargets.Add(c);
					}
				}
			}

			_damageIndicator.Tick(attackPos, hit);

		}

		private void UpdateAnimation() {
			if (_mesh == null) {
				return;
			}

			Vector3 pos = new Vector3(data.hand == WeaponData.Hand.LEFT ? -0.35f : 0.35f, 0, 0);

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

		public void interrupt(Pawn owner) {

            if (activeTime > 0) {
                owner.moveImpulseTimer = 0;
            }
            chargeTime = 0;
            castTime = 0;

        }
        int getCurCharge() {
            if (data.chargeTime > 0 && chargeTime >= data.chargeTime) {
                return 1;
            }
            else {
                return 0;
            }
        }
        WeaponData.AttackData getCurAttackData() {
            return data.attacks[attackType];
        }
        WeaponData.AttackData getCurChargeData() {
            int chargeIndex = getCurCharge();
            return data.attacks[chargeIndex];
        }

        public void defend(Pawn owner, Pawn attacker, Weapon attackerWeapon, WeaponData.AttackData attackData, ref float remainingStun, ref float remainingDamage) {
            if (chargeTime > 0) {
                var defense = getCurChargeData();
                if (defense == null) {
                    return;
                }

				var dirToEnemy = (attacker.rigidBody.position - owner.rigidBody.position);
				if (dirToEnemy == Vector3.zero) {
					dirToEnemy.x = 1;
				}
				else {
					dirToEnemy.Normalize();
				}
				float angleToEnemy = Mathf.Repeat(Mathf.Atan2(dirToEnemy.x, dirToEnemy.z) - owner.yaw, Mathf.PI * 2);
				if (angleToEnemy > defense.defendAngleRange*Mathf.Deg2Rad && Mathf.PI * 2 - angleToEnemy > defense.defendAngleRange * Mathf.Deg2Rad) {
					return;
				}

				remainingDamage = Mathf.Max(0, remainingDamage - defense.defendDamageAbsorb);
                remainingStun = Mathf.Max(0, remainingStun - defense.defendPower);

                owner.useStamina(defense.defendStaminaUse);

				if (defense.defendInterrupt) {
					attackerWeapon.interrupt(attacker);
				}

                int chargeLevel = getCurCharge();
                if (chargeTime < data.parryTime) {
                    if (data.parry != null) {
						attacker.hit(owner, this, data.parry);
                    }
                }
            }

        }


    }
}
