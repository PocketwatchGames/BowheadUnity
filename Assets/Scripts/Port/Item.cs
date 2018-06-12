using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {
    public class Item : Entity {

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

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


        new public class CState : Entity.CState {
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
        }

        new public class CData : Entity.CData {
            public class AttackData {
                public float castTime;
                public float cooldown;
                public float attackRange;
                public float attackRadius;
                public float knockback;
                public float attackDamage;
                public float defendPower;
                public float defendDamageAbsorb;
                public float defendStaminaUse;
                public float staminaUse;
                public float stepDistance;
                public float staminaDrain;
                public float stunPower;
                public float stunPowerBackstab;
                public float attackDamageBackstab;
            }

            public delegate bool UseFn(Item item, Actor actor);

            public ItemType itemType;
            public int slots;
            public float chargeTime;
            public float power;
            public Vector3 weaponSize;
            public AttackData[] attacks;
            public AttackData[] parries;

            public UseFn use;

            public static CData Create(string name, ItemType c, int slots=0) {

                var i = createData<Item, Item.CData>(name);
                i.itemType = c;
                i.slots = slots;

                return i;
            }
            public static CData CreateLoot(string name, int slots, float power, CData.UseFn useFn) {
                var i = createData<Item, Item.CData>(name);
                i.itemType = ItemType.LOOT;
                i.slots = slots;
                i.power = power;
                i.use = useFn;

                return i;
            }

        }






        new public CData Data { get { return GetData<CData>(); } }
        new public CState State { get { return GetState<CState>(); } }
        public static CData GetData(string dataName) { return GetData<CData>(dataName); }











        public static void initData() {
            CData.Create("Pack", ItemType.PACK, 4);
            CData.Create("HP Potion", ItemType.LOOT, 4);
            CData.Create("Hat", ItemType.CLOTHING);
            CData.Create("Helmet", ItemType.CLOTHING);
            CData.Create("Money", ItemType.MONEY);

            CData.CreateLoot("Raw Meat", 4, 50f, UseFood);
            CData.CreateLoot("Water", 4, 50f, UseWater);

            CData i;

            i = CData.Create("Sword", ItemType.RIGHT_HAND);
            {
                i.chargeTime = 0.5f;
                i.weaponSize = new Vector3(1.5f, 0.25f, 0.25f);
                i.attacks[0] = new CData.AttackData();
                i.attacks[0].castTime = 0.1f;
                i.attacks[0].cooldown = 0.05f;
                i.attacks[0].stepDistance = 0.5f;
                i.attacks[0].attackDamage = 10;
                i.attacks[0].attackRange = 1.5f;
                i.attacks[0].attackRadius = 0.75f;
                i.attacks[0].staminaUse = 5;
                i.attacks[0].knockback = 0;
                i.attacks[0].stunPower = 0.25f;
                i.attacks[0].stunPowerBackstab = 1f;
                i.attacks[0].attackDamageBackstab = 50f;

                i.attacks[1] = new CData.AttackData();
                i.attacks[1].castTime = 0.2f;
                i.attacks[1].cooldown = 0.5f;
                i.attacks[1].attackDamage = 30;
                i.attacks[1].attackRange = 2f;
                i.attacks[1].attackRadius = 1.25f;
                i.attacks[1].staminaUse = 30.0f;
                i.attacks[1].knockback = 1;
                i.attacks[1].stunPower = 2.0f;
                i.attacks[1].stunPowerBackstab = 2f;
                i.attacks[1].attackDamageBackstab = 50f;
            }
            i = CData.Create("2HSword", ItemType.BOTH_HANDS);
            {
                i.chargeTime = 0.9f;
                i.weaponSize = new Vector3(2.5f, 0.4f, 0.4f);
                i.attacks[0] = new CData.AttackData();
                i.attacks[0].castTime = 0.35f;
                i.attacks[0].cooldown = 0.15f;
                i.attacks[0].stepDistance = 0f;
                i.attacks[0].attackDamage = 20;
                i.attacks[0].attackRange = 2f;
                i.attacks[0].attackRadius = 1.5f;
                i.attacks[0].staminaUse = 10;
                i.attacks[0].knockback = 1;
                i.attacks[0].stunPower = 1.0f;
                i.attacks[1] = new CData.AttackData();
                i.attacks[1].castTime = 0.1f;
                i.attacks[1].cooldown = 0.5f;
                i.attacks[1].stepDistance = 2.0f;
                i.attacks[1].attackDamage = 100;
                i.attacks[1].attackRange = 1.5f;
                i.attacks[1].attackRadius = 1.5f;
                i.attacks[1].staminaUse = 40.0f;
                i.attacks[1].knockback = 3;
                i.attacks[1].stunPower = 5.0f;
            }
            i = CData.Create("Spear", ItemType.BOTH_HANDS);
            {
                i.chargeTime = 0.5f;
                i.weaponSize = new Vector3(3.5f, 0.25f, 0.25f);
                i.attacks[0] = new CData.AttackData();
                i.attacks[0].castTime = 0.1f;
                i.attacks[0].cooldown = 0.1f;
                i.attacks[0].stepDistance = 2.0f;
                i.attacks[0].attackDamage = 20;
                i.attacks[0].attackRange = 3f;
                i.attacks[0].attackRadius = 1.5f;
                i.attacks[0].staminaUse = 10;
                i.attacks[0].stunPower = 0.1f;

                i.attacks[1] = new CData.AttackData();
                i.attacks[1].castTime = 0.1f;
                i.attacks[1].cooldown = 0.5f;
                i.attacks[1].stepDistance = 0.0f;
                i.attacks[1].attackDamage = 20;
                i.attacks[1].attackRange = 2f;
                i.attacks[1].attackRadius = 3f;
                i.attacks[1].staminaUse = 40.0f;
                i.attacks[1].knockback = 3;
                i.attacks[1].stunPower = 0.1f;
            }
            i = CData.Create("Shield", ItemType.LEFT_HAND);
            {
                i.chargeTime = 0.25f;
                i.weaponSize = new Vector3(1, 1, 0.25f);
                i.attacks[0] = new CData.AttackData();
                i.attacks[0].castTime = 0.1f;
                i.attacks[0].cooldown = 0.1f;
                i.attacks[0].attackDamage = 10;
                i.attacks[0].attackRange = 2f;
                i.attacks[0].attackRadius = 1.5f;
                i.attacks[0].staminaUse = 0;
                i.attacks[0].knockback = 3;
                i.attacks[0].stunPower = 2.0f;
                i.attacks[0].defendDamageAbsorb = 10;
                i.attacks[0].defendPower = 20;
                i.attacks[0].defendStaminaUse = 10;

                i.parries[0] = new CData.AttackData();
                i.parries[0].attackDamage = 0;
                i.parries[0].knockback = 3;
                i.parries[0].stunPower = 10;

                i.attacks[1] = new CData.AttackData();
                i.attacks[1].castTime = 0.1f;
                i.attacks[1].cooldown = 0.1f;
                i.attacks[1].attackDamage = 0;
                i.attacks[1].staminaUse = 0;
                i.attacks[1].defendDamageAbsorb = 5;
                i.attacks[1].defendPower = 20;
                i.attacks[1].defendStaminaUse = 10;
            }
            i = CData.Create("Teeth", ItemType.RIGHT_HAND);
            {
                i.weaponSize = new Vector3(1.5f, 0.25f, 0.25f);
                i.attacks[0] = new CData.AttackData();
                i.attacks[0].castTime = 0.5f;
                i.attacks[0].cooldown = 1.0f;
                i.attacks[0].stepDistance = 4f;
                i.attacks[0].attackDamage = 10;
                i.attacks[0].attackRange = 1.5f;
                i.attacks[0].attackRadius = 0.75f;
                i.attacks[0].staminaUse = 20;
                i.attacks[0].knockback = 0;
                i.attacks[0].stunPower = 10.0f;
                i.attacks[0].attackDamageBackstab = 20f;
                i.attacks[0].stunPowerBackstab = 20f;
            }

        }


        public Item(CData data) : base(data, new Entity.CState()) {
            State.contained.Clear();
            world.allItems.Add(this);
        }

        public Item(string data) : base(GetData(data), new Entity.CState()) {
            State.contained.Clear();
            world.allItems.Add(this);
        }

        ~Item() {
        }

        public void spawn(Vector3 pos) {
            State.position = pos;
            State.spawned = true;
        }

        public bool use(Actor actor) {
            if (Data.use == null) {
                return true;
            }
            return Data.use(this, actor);
        }

        public void charge(float dt) {
            if (State.cooldown <= 0) {
                State.chargeTime += dt;
            }
        }

        public bool attack(Actor actor) {
            if (State.castTime > 0 || State.cooldown > 0) {
                return false;
            }
            State.attackType = getCurCharge();
            var attackData = getCurAttackData();

            State.castTime = attackData.castTime;
            State.chargeTime = 0;
            Vector3 attackDir = new Vector3(Mathf.Cos(actor.State.yaw), Mathf.Sin(actor.State.yaw), 0);
            float stepAmt = attackData.stepDistance;
            if (stepAmt != 0 && actor.State.activity == Actor.Activity.ONGROUND) {
                actor.State.moveImpulse = attackDir * stepAmt;
                actor.State.moveImpulseTimer = attackData.castTime;
                actor.State.velocity = Vector3.zero;
            }
            if (State.castTime <= 0) {
                activate(actor);
            }
            return true;
        }

        bool checkIfHit(Actor owner, Vector3 attackPos, Vector3 dir, Vector3 attackerPos, CData.AttackData attackData, Actor enemy) {

            float critterRadius = 1.0f;
            var diff = enemy.waistPosition(enemy.State.position) - attackPos;
            float dist = diff.magnitude;
            if (dist <= attackData.attackRadius + critterRadius) {

                enemy.hit(owner, this, attackData);

                if (State.attackType == 0) {
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

            State.castTime = 0;
            State.cooldown = d.cooldown;

            Vector3 attackDir = new Vector3((float)Math.Cos(actor.State.yaw), (float)Math.Sin(actor.State.yaw), 0);
            Vector3 attackPos = actor.State.position + attackDir * d.attackRange;
            bool hit = false;

            if (actor.State.team == 0) {
                foreach (var c in world.critters) {
                    if (c.State.spawned) {
                        hit |= checkIfHit(actor, attackPos, attackDir, actor.State.position, d, c);
                    }
                }
            }
            else {
                hit |= checkIfHit(actor, attackPos, attackDir, actor.State.position, d, world.player);
            }
            //RendererWorld.createMarker(attackPos, d.attackRadius * 2, 0.1f, hit ? new Color(1, 0, 0, 1f) : new Color(0, 0, 0, 1f));

            actor.useStamina(d.staminaUse);

        }




        public void updateCast(float dt, Actor actor) {

            if (State.cooldown > 0) {
                State.cooldown = Math.Max(0, State.cooldown - dt);
                if (State.cooldown > 0) {
                    actor.State.canMove = false;
                    actor.State.canTurn = false;
                }
            }
            if (State.castTime > 0) {
                actor.State.canMove = false;
                State.castTime = Math.Max(0, State.castTime - dt);
                actor.State.canTurn = false;
                if (State.castTime <= 0) {
                    activate(actor);
                }
            }

            if (State.chargeTime > 0) {
                actor.State.canRun = false;
            }

        }
        public void update(float dt) {

            if (!State.inMotion) {
                if (State.velocity != Vector3.zero
                    || !World.isSolidBlock(world.getBlock(State.position))) {
                    State.inMotion = true;
                }
            }

            if (State.inMotion) {
                var newVel = State.velocity;
                {
                    bool onGround = World.isSolidBlock(world.getBlock(State.position)) && State.velocity.z <= 0;
                    if (!onGround) {
                        float gravity = -30f;
                        newVel.z += gravity * dt;
                    }
                    State.velocity = newVel;
                }

                var newPos = State.position;
                {
                    newPos.z += State.velocity.z * dt;
                    bool onGround = World.isSolidBlock(world.getBlock(newPos)) && State.velocity.z <= 0;
                    if (onGround) {
                        float bounceVel = -5f;
                        float bounceCoefficient = 0.5f;
                        float friction = 10f;
                        newPos.z = (float)Math.Ceiling(newPos.z);
                        if (State.velocity.z > bounceVel) {
                            newVel = newVel - Math.Max(1f, dt * friction) * State.velocity;
                        }
                        else {
                            newVel.z = -newVel.z * bounceCoefficient;
                        }
                        if (newVel.magnitude < 0.1f) {
                            newVel = Vector3.zero;
                        }
                    }
                    State.velocity = newVel;
                    var moveXY = new Vector3(State.velocity.x * dt, State.velocity.y * dt, 0);
                    if (!World.isSolidBlock(world.getBlock(newPos + moveXY)) && State.velocity.z != 0) {
                        State.position = newPos + moveXY;
                    }
                    else {
                        var moveX = new Vector3(State.velocity.x * dt, 0, 0);
                        if (!World.isSolidBlock(world.getBlock(newPos + moveX)) && State.velocity.z != 0) {
                            State.position = newPos + moveX;
                        }
                        else {
                            var moveY = new Vector3(0, State.velocity.y * dt, 0);
                            if (!World.isSolidBlock(world.getBlock(newPos + moveY)) && State.velocity.z != 0) {
                                State.position = newPos + moveY;
                            }
                        }
                    }
                }
            }
        }

        public void interrupt(Actor owner) {

            if (State.castTime > 0) {
                owner.State.moveImpulseTimer = 0;
            }
            State.chargeTime = 0;
            State.castTime = 0;

        }

        static bool UseWater(Item item, Actor actor) {
            // TODO: This static cast is not good
            Player player = actor as Player;
            if (player == null)
                return false;
            if (player.State.thirst >= player.State.maxThirst) {
                return false;
            }
            player.State.thirst = Math.Min(player.State.thirst + item.Data.power, player.State.maxThirst);
            return true;
        }

        static bool UseFood(Item item, Actor actor) {
            if (actor.State.health >= actor.State.maxHealth) {
                return false;
            }
            actor.State.health = Math.Min(actor.State.health + item.Data.power, actor.State.maxHealth);
            return true;
        }

        int getCurCharge() {
            if (Data.chargeTime > 0 && State.chargeTime >= Data.chargeTime) {
                return 1;
            }
            else {
                return 0;
            }
        }
        CData.AttackData getCurAttackData() {
            return Data.attacks[State.attackType];
        }
        CData.AttackData getCurChargeData() {
            int chargeIndex = getCurCharge();
            return Data.attacks[chargeIndex];
        }


        public void defend(Actor owner, Actor attacker, Item weapon, CData.AttackData attackData, ref float remainingStun, ref float remainingDamage) {
            if (State.chargeTime > 0) {
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