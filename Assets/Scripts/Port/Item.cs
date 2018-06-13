using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {


    public class Item : Entity {


        #region State

        public bool inMotion;
        public Vector3 position;
        public Vector3 velocity;
        public bool spawned;
        public int count;
        public int attackType;
        public float castTime;
        public float cooldown;
        public float chargeTime;
        public List<Item> contained = new List<Item>();
        public float yaw;

        #endregion

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {
            transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw, Vector3.up));
        }


        public enum ItemType {
            NONE,
            MONEY,
            CLOTHING,
            LEFT_HAND,
            RIGHT_HAND,
            BOTH_HANDS,
            PACK,
            LOOT,
            COUNT,
        }






        new public ItemData Data { get { return GetData<ItemData>(); } }
        public static ItemData GetData(string dataName) { return DataManager.GetData<ItemData>(dataName); }











        public static void initData() {
            //CData.Create("Pack", ItemType.PACK, 4);
            //CData.Create("HP Potion", ItemType.LOOT, 4);
            //CData.Create("Hat", ItemType.CLOTHING);
            //CData.Create("Helmet", ItemType.CLOTHING);
            //CData.Create("Money", ItemType.MONEY);

            //CData.CreateLoot("Raw Meat", 4, 50f, UseFood);
            //CData.CreateLoot("Water", 4, 50f, UseWater);

            //CData i;

            //i = CData.Create("Sword", ItemType.RIGHT_HAND);
            //{
            //    i.chargeTime = 0.5f;
            //    i.weaponSize = new Vector3(1.5f, 0.25f, 0.25f);
            //    i.attacks[0] = new CData.AttackData();
            //    i.attacks[0].castTime = 0.1f;
            //    i.attacks[0].cooldown = 0.05f;
            //    i.attacks[0].stepDistance = 0.5f;
            //    i.attacks[0].attackDamage = 10;
            //    i.attacks[0].attackRange = 1.5f;
            //    i.attacks[0].attackRadius = 0.75f;
            //    i.attacks[0].staminaUse = 5;
            //    i.attacks[0].knockback = 0;
            //    i.attacks[0].stunPower = 0.25f;
            //    i.attacks[0].stunPowerBackstab = 1f;
            //    i.attacks[0].attackDamageBackstab = 50f;

            //    i.attacks[1] = new CData.AttackData();
            //    i.attacks[1].castTime = 0.2f;
            //    i.attacks[1].cooldown = 0.5f;
            //    i.attacks[1].attackDamage = 30;
            //    i.attacks[1].attackRange = 2f;
            //    i.attacks[1].attackRadius = 1.25f;
            //    i.attacks[1].staminaUse = 30.0f;
            //    i.attacks[1].knockback = 1;
            //    i.attacks[1].stunPower = 2.0f;
            //    i.attacks[1].stunPowerBackstab = 2f;
            //    i.attacks[1].attackDamageBackstab = 50f;
            //}
            //i = CData.Create("2HSword", ItemType.BOTH_HANDS);
            //{
            //    i.chargeTime = 0.9f;
            //    i.weaponSize = new Vector3(2.5f, 0.4f, 0.4f);
            //    i.attacks[0] = new CData.AttackData();
            //    i.attacks[0].castTime = 0.35f;
            //    i.attacks[0].cooldown = 0.15f;
            //    i.attacks[0].stepDistance = 0f;
            //    i.attacks[0].attackDamage = 20;
            //    i.attacks[0].attackRange = 2f;
            //    i.attacks[0].attackRadius = 1.5f;
            //    i.attacks[0].staminaUse = 10;
            //    i.attacks[0].knockback = 1;
            //    i.attacks[0].stunPower = 1.0f;
            //    i.attacks[1] = new CData.AttackData();
            //    i.attacks[1].castTime = 0.1f;
            //    i.attacks[1].cooldown = 0.5f;
            //    i.attacks[1].stepDistance = 2.0f;
            //    i.attacks[1].attackDamage = 100;
            //    i.attacks[1].attackRange = 1.5f;
            //    i.attacks[1].attackRadius = 1.5f;
            //    i.attacks[1].staminaUse = 40.0f;
            //    i.attacks[1].knockback = 3;
            //    i.attacks[1].stunPower = 5.0f;
            //}
            //i = CData.Create("Spear", ItemType.BOTH_HANDS);
            //{
            //    i.chargeTime = 0.5f;
            //    i.weaponSize = new Vector3(3.5f, 0.25f, 0.25f);
            //    i.attacks[0] = new CData.AttackData();
            //    i.attacks[0].castTime = 0.1f;
            //    i.attacks[0].cooldown = 0.1f;
            //    i.attacks[0].stepDistance = 2.0f;
            //    i.attacks[0].attackDamage = 20;
            //    i.attacks[0].attackRange = 3f;
            //    i.attacks[0].attackRadius = 1.5f;
            //    i.attacks[0].staminaUse = 10;
            //    i.attacks[0].stunPower = 0.1f;

            //    i.attacks[1] = new CData.AttackData();
            //    i.attacks[1].castTime = 0.1f;
            //    i.attacks[1].cooldown = 0.5f;
            //    i.attacks[1].stepDistance = 0.0f;
            //    i.attacks[1].attackDamage = 20;
            //    i.attacks[1].attackRange = 2f;
            //    i.attacks[1].attackRadius = 3f;
            //    i.attacks[1].staminaUse = 40.0f;
            //    i.attacks[1].knockback = 3;
            //    i.attacks[1].stunPower = 0.1f;
            //}
            //i = CData.Create("Shield", ItemType.LEFT_HAND);
            //{
            //    i.chargeTime = 0.25f;
            //    i.weaponSize = new Vector3(1, 1, 0.25f);
            //    i.attacks[0] = new CData.AttackData();
            //    i.attacks[0].castTime = 0.1f;
            //    i.attacks[0].cooldown = 0.1f;
            //    i.attacks[0].attackDamage = 10;
            //    i.attacks[0].attackRange = 2f;
            //    i.attacks[0].attackRadius = 1.5f;
            //    i.attacks[0].staminaUse = 0;
            //    i.attacks[0].knockback = 3;
            //    i.attacks[0].stunPower = 2.0f;
            //    i.attacks[0].defendDamageAbsorb = 10;
            //    i.attacks[0].defendPower = 20;
            //    i.attacks[0].defendStaminaUse = 10;

            //    i.parries[0] = new CData.AttackData();
            //    i.parries[0].attackDamage = 0;
            //    i.parries[0].knockback = 3;
            //    i.parries[0].stunPower = 10;

            //    i.attacks[1] = new CData.AttackData();
            //    i.attacks[1].castTime = 0.1f;
            //    i.attacks[1].cooldown = 0.1f;
            //    i.attacks[1].attackDamage = 0;
            //    i.attacks[1].staminaUse = 0;
            //    i.attacks[1].defendDamageAbsorb = 5;
            //    i.attacks[1].defendPower = 20;
            //    i.attacks[1].defendStaminaUse = 10;
            //}
            //i = CData.Create("Teeth", ItemType.RIGHT_HAND);
            //{
            //    i.weaponSize = new Vector3(1.5f, 0.25f, 0.25f);
            //    i.attacks[0] = new CData.AttackData();
            //    i.attacks[0].castTime = 0.5f;
            //    i.attacks[0].cooldown = 1.0f;
            //    i.attacks[0].stepDistance = 4f;
            //    i.attacks[0].attackDamage = 10;
            //    i.attacks[0].attackRange = 1.5f;
            //    i.attacks[0].attackRadius = 0.75f;
            //    i.attacks[0].staminaUse = 20;
            //    i.attacks[0].knockback = 0;
            //    i.attacks[0].stunPower = 10.0f;
            //    i.attacks[0].attackDamageBackstab = 20f;
            //    i.attacks[0].stunPowerBackstab = 20f;
            //}

        }



        public void init(ItemData data, World world) {
            base.init(data, world);
            contained.Clear();
            world.allItems.Add(this);
        }

        public void init(string data) {
            base.init(GetData(data), world);
            contained.Clear();
            world.allItems.Add(this);
        }


        public void spawn(Vector3 pos) {
            position = pos;
            spawned = true;
        }

        public bool use(Actor actor) {
            if (Data.use == null) {
                return true;
            }
            return Data.use(this, actor);
        }

        public void charge(float dt) {
            if (cooldown <= 0) {
                chargeTime += dt;
            }
        }

        public bool attack(Actor actor) {
            if (castTime > 0 || cooldown > 0) {
                return false;
            }
            attackType = getCurCharge();
            var attackData = getCurAttackData();

            castTime = attackData.castTime;
            chargeTime = 0;
            Vector3 attackDir = new Vector3(Mathf.Cos(actor.yaw), Mathf.Sin(actor.yaw), 0);
            float stepAmt = attackData.stepDistance;
            if (stepAmt != 0 && actor.activity == Actor.Activity.ONGROUND) {
                actor.moveImpulse = attackDir * stepAmt;
                actor.moveImpulseTimer = attackData.castTime;
                actor.velocity = Vector3.zero;
            }
            if (castTime <= 0) {
                activate(actor);
            }
            return true;
        }

        bool checkIfHit(Actor owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, ItemData.AttackData attackData, Actor enemy) {

            float critterRadius = 1.0f;
            var diff = enemy.waistPosition(enemy.position) - attackPos;
            float dist = diff.magnitude;
            if (dist <= attackData.attackRadius + critterRadius) {

                enemy.hit(owner, this, attackData);

                if (attackType == 0) {
                    world.camera.shake(0.15f, 0.05f, 0.01f);
                }
                else {
                    world.camera.shake(0.2f, 0.2f, 0.05f);
                }
                return true;
            }
            return false;
        }
        void activate(Actor actor) {
            var d = getCurAttackData();

            castTime = 0;
            cooldown = d.cooldown;

            Vector3 attackDir = new Vector3((float)Math.Cos(actor.yaw), (float)Math.Sin(actor.yaw), 0);
            Vector3 attackPos = actor.position + attackDir * d.attackRange;
            bool hit = false;

            if (actor.team == 0) {
                var cs = world.critters.GetComponentsInAllChildren<Critter>();
                foreach (var c in cs) {
                    if (c.spawned) {
                        hit |= checkIfHit(actor, attackPos, attackDir, actor.position, d, c);
                    }
                }
            }
            else {
                hit |= checkIfHit(actor, attackPos, attackDir, actor.position, d, world.player);
            }
            //RendererWorld.createMarker(attackPos, d.attackRadius * 2, 0.1f, hit ? new Color(1, 0, 0, 1f) : new Color(0, 0, 0, 1f));

            actor.useStamina(d.staminaUse);

        }




        public void updateCast(float dt, Actor actor) {

            if (cooldown > 0) {
                cooldown = Math.Max(0, cooldown - dt);
                if (cooldown > 0) {
                    actor.canMove = false;
                    actor.canTurn = false;
                }
            }
            if (castTime > 0) {
                actor.canMove = false;
                castTime = Math.Max(0, castTime - dt);
                actor.canTurn = false;
                if (castTime <= 0) {
                    activate(actor);
                }
            }

            if (chargeTime > 0) {
                actor.canRun = false;
            }

        }
        public void update(float dt) {

            if (!inMotion) {
                if (velocity != Vector3.zero
                    || !World.isSolidBlock(world.getBlock(position))) {
                    inMotion = true;
                }
            }

            if (inMotion) {
                var newVel = velocity;
                {
                    bool onGround = World.isSolidBlock(world.getBlock(position)) && velocity.z <= 0;
                    if (!onGround) {
                        float gravity = -30f;
                        newVel.z += gravity * dt;
                    }
                    velocity = newVel;
                }

                var newPos = position;
                {
                    newPos.z += velocity.z * dt;
                    bool onGround = World.isSolidBlock(world.getBlock(newPos)) && velocity.z <= 0;
                    if (onGround) {
                        float bounceVel = -5f;
                        float bounceCoefficient = 0.5f;
                        float friction = 10f;
                        newPos.z = (float)Math.Ceiling(newPos.z);
                        if (velocity.z > bounceVel) {
                            newVel = newVel - Math.Max(1f, dt * friction) * velocity;
                        }
                        else {
                            newVel.z = -newVel.z * bounceCoefficient;
                        }
                        if (newVel.magnitude < 0.1f) {
                            newVel = Vector3.zero;
                        }
                    }
                    velocity = newVel;
                    var moveXZ = new Vector3(velocity.x * dt, 0, velocity.y * dt);
                    if (!World.isSolidBlock(world.getBlock(newPos + moveXZ)) && velocity.y != 0) {
                        position = newPos + moveXZ;
                    }
                    else {
                        var moveX = new Vector3(velocity.x * dt, 0, 0);
                        if (!World.isSolidBlock(world.getBlock(newPos + moveX)) && velocity.y != 0) {
                            position = newPos + moveX;
                        }
                        else {
                            var moveZ = new Vector3(0, 0, velocity.z * dt);
                            if (!World.isSolidBlock(world.getBlock(newPos + moveZ)) && velocity.y != 0) {
                                position = newPos + moveZ;
                            }
                        }
                    }
                }
            }

            float yaw = 0;
            transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw, Vector3.up));

        }

        public void interrupt(Actor owner) {

            if (castTime > 0) {
                owner.moveImpulseTimer = 0;
            }
            chargeTime = 0;
            castTime = 0;

        }

        static bool UseWater(Item item, Actor actor) {
            // TODO: This static cast is not good
            Player player = actor as Player;
            if (player == null)
                return false;
            if (player.thirst >= player.maxThirst) {
                return false;
            }
            player.thirst = Math.Min(player.thirst + item.Data.power, player.maxThirst);
            return true;
        }

        static bool UseFood(Item item, Actor actor) {
            if (actor.health >= actor.maxHealth) {
                return false;
            }
            actor.health = Math.Min(actor.health + item.Data.power, actor.maxHealth);
            return true;
        }

        int getCurCharge() {
            if (Data.chargeTime > 0 && chargeTime >= Data.chargeTime) {
                return 1;
            }
            else {
                return 0;
            }
        }
        ItemData.AttackData getCurAttackData() {
            return Data.attacks[attackType];
        }
        ItemData.AttackData getCurChargeData() {
            int chargeIndex = getCurCharge();
            return Data.attacks[chargeIndex];
        }


        public void defend(Actor owner, Actor attacker, Item weapon, ItemData.AttackData attackData, ref float remainingStun, ref float remainingDamage) {
            if (chargeTime > 0) {
                var defense = getCurChargeData();
                if (defense == null) {
                    return;
                }

                remainingDamage = Math.Max(0, remainingDamage - defense.defendDamageAbsorb);
                remainingStun = Math.Max(0, remainingStun - defense.defendPower);

                owner.useStamina(defense.defendStaminaUse);

                var parry = Data.parries[getCurCharge()];
                if (parry != null) {
                    attacker.hit(owner, this, parry);
                }
            }

        }

    }
}