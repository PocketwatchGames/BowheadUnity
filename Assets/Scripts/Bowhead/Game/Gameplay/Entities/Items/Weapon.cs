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
        public float cooldown;
        public float chargeTime;

        #endregion

        private GameObject _mesh;

        public override void OnSlotChange(int newSlot, Pawn owner) {
            castTime = 0;
            chargeTime = 0;

            if (_mesh != null) {
                GameObject.Destroy(_mesh);
            }
            if (data.prefab != null) {
                if (newSlot == (int)Player.InventorySlot.LEFT_HAND || newSlot == (int)Player.InventorySlot.RIGHT_HAND) {
                    var prefab = data.prefab.Load();
                    _mesh = GameObject.Instantiate(prefab, owner.go.transform, false);
                    _mesh.transform.localPosition = new Vector3(newSlot == (int)Player.InventorySlot.LEFT_HAND ? -0.35f : -0.35f, 1, 0.25f);
                }
            }
        }
        public void Charge(float dt) {
            if (cooldown <= 0) {
                chargeTime += dt;
            }
        }

        public bool Attack(Pawn actor) {
            if (castTime > 0 || cooldown > 0) {
                return false;
            }
            attackType = getCurCharge();
            var attackData = getCurAttackData();

            castTime = attackData.castTime;
            chargeTime = 0;
            Vector3 attackDir = new Vector3(Mathf.Sin(actor.yaw), 0, Mathf.Cos(actor.yaw));
            float stepAmt = attackData.stepDistance;
            if (stepAmt != 0 && actor.activity == Pawn.Activity.OnGround) {
                actor.moveImpulse = attackDir * stepAmt;
                actor.moveImpulseTimer = attackData.castTime;
                actor.velocity = Vector3.zero;
            }
            if (castTime <= 0) {
                Activate(actor);
            }
            return true;
        }

        bool CheckIfHit(Pawn owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, WeaponData.AttackData attackData, Pawn enemy) {

            Debug.Assert(owner != enemy);

            float critterRadius = 1.0f;
            var diff = enemy.waistPosition(enemy.position) - attackPos;
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
        void Activate(Pawn actor) {
            var d = getCurAttackData();

            castTime = 0;
            cooldown = d.cooldown;

            Vector3 attackDir = new Vector3((float)Mathf.Sin(actor.yaw), 0, (float)Mathf.Cos(actor.yaw));
            Vector3 attackPos = actor.position + attackDir * d.attackRange;
            bool hit = false;

            if (!actor.team.isMonsterTeam) {
                foreach (var c in actor.world.GetActorIterator<Actors.Critter>()) {
                    hit |= CheckIfHit(actor, attackPos, attackDir, actor.position, d, c);
                }
            }
            else {
				foreach (var player in actor.world.GetActorIterator<Player>()) {
					hit |= CheckIfHit(actor, attackPos, attackDir, actor.position, d, player);
				}
            }
            //RendererWorld.createMarker(attackPos, d.attackRadius * 2, 0.1f, hit ? new Color(1, 0, 0, 1f) : new Color(0, 0, 0, 1f));

            actor.useStamina(d.staminaUse);

        }

        override public void UpdateCast(float dt, Pawn actor) {

            if (cooldown > 0) {
                cooldown = Mathf.Max(0, cooldown - dt);
                if (cooldown > 0) {
                    actor.canMove = false;
                    actor.canTurn = false;
                }
            }
            if (castTime > 0) {
                actor.canMove = false;
                castTime = Mathf.Max(0, castTime - dt);
                actor.canTurn = false;
                if (castTime <= 0) {
                    Activate(actor);
                }
            }

            if (chargeTime > 0) {
                actor.canRun = false;
            }

        }

        public void interrupt(Pawn owner) {

            if (castTime > 0) {
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

        public void defend(Pawn owner, Pawn attacker, Item weapon, WeaponData.AttackData attackData, ref float remainingStun, ref float remainingDamage) {
            if (chargeTime > 0) {
                var defense = getCurChargeData();
                if (defense == null) {
                    return;
                }

                remainingDamage = Mathf.Max(0, remainingDamage - defense.defendDamageAbsorb);
                remainingStun = Mathf.Max(0, remainingStun - defense.defendPower);

                owner.useStamina(defense.defendStaminaUse);

                int chargeLevel = getCurCharge();
                if (data.parries.Length > chargeLevel) {
                    var parry = data.parries[chargeLevel];
                    if (parry != null) {
                        attacker.hit(owner, this, parry);
                    }
                }
            }

        }


    }
}
