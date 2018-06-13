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
        public float yaw;

        #endregion

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {
            transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw, Vector3.up));
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

        virtual public void updateCast(float dt, Actor actor) {
        }

        public virtual void onSlotChange() {

        }

        virtual public void init(ItemData data, World world) {
            base.init(data, world);
            world.allItems.Add(this);
        }

        public void init(string data) {
            init(GetData(data), world);
        }


        public void spawn(Vector3 pos) {
            position = pos;
            spawned = true;
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



    }
}