using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Weapon : Item {

        #region State

        public int attackType;
        public float castTime;
        public float cooldown;
        public float chargeTime;

        #endregion

        new public WeaponData Data { get { return GetData<WeaponData>(); } }
        public static WeaponData GetData(string dataName) { return DataManager.GetData<WeaponData>(dataName); }


        public override void OnSlotChange() {
            castTime = 0;
            chargeTime = 0;

        }
        public void Charge(float dt) {
            if (cooldown <= 0) {
                chargeTime += dt;
            }
        }

        public bool Attack(Actor actor) {
            if (castTime > 0 || cooldown > 0) {
                return false;
            }
            attackType = getCurCharge();
            var attackData = getCurAttackData();

            castTime = attackData.castTime;
            chargeTime = 0;
            Vector3 attackDir = new Vector3(Mathf.Sin(actor.yaw), 0, Mathf.Cos(actor.yaw));
            float stepAmt = attackData.stepDistance;
            if (stepAmt != 0 && actor.activity == Actor.Activity.OnGround) {
                actor.moveImpulse = attackDir * stepAmt;
                actor.moveImpulseTimer = attackData.castTime;
                actor.velocity = Vector3.zero;
            }
            if (castTime <= 0) {
                Activate(actor);
            }
            return true;
        }

        bool CheckIfHit(Actor owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, WeaponData.AttackData attackData, Actor enemy) {

            float critterRadius = 1.0f;
            var diff = enemy.waistPosition(enemy.position) - attackPos;
            float dist = diff.magnitude;
            if (dist <= attackData.attackRadius + critterRadius) {

                enemy.hit(owner, this, attackData);

                if (attackType == 0) {
                    world.camera.Shake(0.15f, 0.05f, 0.01f);
                }
                else {
                    world.camera.Shake(0.2f, 0.2f, 0.05f);
                }
                return true;
            }
            return false;
        }
        void Activate(Actor actor) {
            var d = getCurAttackData();

            castTime = 0;
            cooldown = d.cooldown;

            Vector3 attackDir = new Vector3((float)Mathf.Sin(actor.yaw), 0, (float)Mathf.Cos(actor.yaw));
            Vector3 attackPos = actor.position + attackDir * d.attackRange;
            bool hit = false;

            if (actor.team == 0) {
                var cs = world.critters.GetComponentsInAllChildren<Critter>();
                foreach (var c in cs) {
                    if (c.spawned) {
                        hit |= CheckIfHit(actor, attackPos, attackDir, actor.position, d, c);
                    }
                }
            }
            else {
                hit |= CheckIfHit(actor, attackPos, attackDir, actor.position, d, world.player);
            }
            //RendererWorld.createMarker(attackPos, d.attackRadius * 2, 0.1f, hit ? new Color(1, 0, 0, 1f) : new Color(0, 0, 0, 1f));

            actor.useStamina(d.staminaUse);

        }




        override public void UpdateCast(float dt, Actor actor) {

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
        public void interrupt(Actor owner) {

            if (castTime > 0) {
                owner.moveImpulseTimer = 0;
            }
            chargeTime = 0;
            castTime = 0;

        }
        int getCurCharge() {
            if (Data.chargeTime > 0 && chargeTime >= Data.chargeTime) {
                return 1;
            }
            else {
                return 0;
            }
        }
        WeaponData.AttackData getCurAttackData() {
            return Data.attacks[attackType];
        }
        WeaponData.AttackData getCurChargeData() {
            int chargeIndex = getCurCharge();
            return Data.attacks[chargeIndex];
        }


        public void defend(Actor owner, Actor attacker, Item weapon, WeaponData.AttackData attackData, ref float remainingStun, ref float remainingDamage) {
            if (chargeTime > 0) {
                var defense = getCurChargeData();
                if (defense == null) {
                    return;
                }

                remainingDamage = Mathf.Max(0, remainingDamage - defense.defendDamageAbsorb);
                remainingStun = Mathf.Max(0, remainingStun - defense.defendPower);

                owner.useStamina(defense.defendStaminaUse);

                int chargeLevel = getCurCharge();
                if (Data.parries.Length > chargeLevel) {
                    var parry = Data.parries[chargeLevel];
                    if (parry != null) {
                        attacker.hit(owner, this, parry);
                    }
                }
            }

        }


    }
}
