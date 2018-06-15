using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {



    public class Player : Actor {

        #region State
        [Header("Player")]
        public Vector3 spawnPoint;
        public Vector2 mapPos;

        [Header("Inventory")]
        public int money;
        public WeightClass weight;
        public float dropTimer;

        [Header("Player Stats")]
        public float thirst;
        public float maxThirst;
        public float temperature;
        #endregion



        public enum InventorySlot {
            CLOTHING = 0,
            LEFT_HAND = 1,
            RIGHT_HAND = 2,
            PACK = 3,
        }
        public enum WeightClass {
            LIGHT,
            MEDIUM,
            HEAVY,
            ENCUMBERED,
            IMMOBILE,
            COUNT
        }


        new public PlayerData Data { get { return GetData<PlayerData>(); } }
        new public static PlayerData GetData(string DataName) {
            return DataManager.GetData<PlayerData>(DataName);
        }



        Actor attackTargetPreview;




        public delegate void onMoneyChangeFn();
        public event onMoneyChangeFn onMoneyChange;
        public event onMoneyChangeFn onInventoryChange;







        #region core functions

        public void init(PlayerData data, World world) {
            base.init(data, world);
            attackTargetPreview = null;
        }

        // TODO: move cameraYaw into the PlayerCmd struct
        public void Tick(float dt, float cameraYaw) {

            canMove = weight < WeightClass.IMMOBILE && !stunned;
            canAttack = weight < WeightClass.IMMOBILE;
            canRun = weight < WeightClass.ENCUMBERED;
            canJump = weight < WeightClass.ENCUMBERED;
            canClimb = weight < WeightClass.HEAVY;
            canSwim = weight < WeightClass.HEAVY;
            canClimbWell = weight < WeightClass.MEDIUM;
            canTurn = true;

            if (recovering) {
                canRun = false;
                canJump = false;
                canClimb = false;
                canClimbWell = false;
                canAttack = false;
            }
            if (stunned) {
                canRun = false;
                canJump = false;
                canClimb = false;
                canClimbWell = false;
                canAttack = false;
                canMove = false;
            }

            if (activity == Activity.SWIMMING || activity == Activity.CLIMBING) {
                canAttack = false;
            }


            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] != null) {
                    inventory[i].updateCast(dt, this);
                }
            }

            Input_t input;
            handleInput(dt, cameraYaw, out input);

            if (input.inputs[(int)InputType.INTERACT] == InputState.JUST_PRESSED) {
                interact();
            }

            if (input.IsPressed(InputType.SWAP)) {
            }

            bool isCasting = false;
            var itemRight = inventory[(int)InventorySlot.RIGHT_HAND] as Weapon;
            var itemLeft = inventory[(int)InventorySlot.LEFT_HAND] as Weapon;
            if (canAttack) {
                if (itemLeft != null) {
                    if (input.IsPressed(InputType.ATTACK_LEFT)) {
                        itemLeft.charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType.ATTACK_LEFT] == InputState.JUST_RELEASED) {
                            itemLeft.attack(this);
                        }
                        itemLeft.chargeTime = 0;
                    }
                    if (itemLeft.castTime > 0) {
                        isCasting = true;
                    }
                }
                if (itemRight != null) {
                    if (input.IsPressed(InputType.ATTACK_RIGHT)) {
                        itemRight.charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType.ATTACK_RIGHT] == InputState.JUST_RELEASED) {
                            itemRight.attack(this);
                        }
                        itemRight.chargeTime = 0;
                    }
                    if (itemRight.castTime > 0) {
                        isCasting = true;
                    }
                }
            }

            attackTargetPreview = getAttackTarget(yaw);

            bool shouldLock = false;
            if (input.IsPressed(InputType.ATTACK_LEFT) || input.IsPressed(InputType.ATTACK_RIGHT)) {
                shouldLock = true;
            }
            if ((!shouldLock && !isCasting) || !isValidAttackTarget(attackTarget)) {
                attackTarget = null;
            }
            if (shouldLock && attackTarget == null) {
                attackTarget = attackTargetPreview;
            }
            lockedToTarget = attackTarget != null;



            base.Tick(dt, input);
            updateStats(dt);

        }

        bool isValidAttackTarget(Actor actor) {
            if (actor == null)
                return false;
            var diff = actor.position - position;
            if (diff.magnitude > 20) {
                return false;
            }
            return true;
        }

        Actor getAttackTarget(float yaw) {

            float maxDist = 40;
            float maxTargetAngle = Mathf.PI;

            Actor bestTarget = null;
            float bestTargetAngle = maxTargetAngle;
            var cs = world.critters.GetComponentsInAllChildren<Critter>();
            foreach (var c in cs) {
                var diff = c.position - position;
                float dist = diff.magnitude;
                if (dist < maxDist) {
                    float angleToEnemy = Mathf.Atan2(diff.x, diff.z);

                    float yawDiff = Math.Abs(Mathf.Repeat(angleToEnemy - yaw, Mathf.PI*2));

                    // take the target's radius into account based on how far away they are
                    yawDiff = Math.Max(0.001f, yawDiff - Mathf.Atan2(c.Data.collisionRadius, dist));

                    float distT = Mathf.Pow(dist / maxDist, 2);
                    yawDiff *= distT;

                    if (yawDiff < bestTargetAngle) {
                        bestTarget = c;
                        bestTargetAngle = yawDiff;
                    }
                }
            }
            return bestTarget;
        }

        ////////////
        // Spawning
        ////////////

        public void spawn(Vector3 pos) {
            position = pos;
            spawned = true;

            setSpawnPoint(pos);

            // NOTE: andy these will leak unless you delete these somewhere.

            pickUp(world.CreateItem("Pack"));
            pickUp(world.CreateItem("Hat"));
            pickUp(world.CreateItem("Helmet"));
            pickUp(world.CreateItem("Sword"));
            pickUp(world.CreateItem("2HSword"));
            pickUp(world.CreateItem("Shield"));
            pickUp(world.CreateItem("Spear"));

            //Equip(new game.items.Clothing("Cloak"));
            //AddInventory(new Clothing("Backpack"));
            //Equip(new game.items.Weapon("PickAxe"));
            //Equip(new game.items.Parachute("Parachute"));
            //AddInventory(new Jetpack("Jetpack"));
            //SetMapPos(new Vector2(spawnPoint.X, spawnPoint.Z));
            //int exploreSize = 1024;
            //Explore(this, new EventArgsExplore(){ region = new Rectangle((int)(mapPos.X - exploreSize / 2), (int)(mapPos.Y - exploreSize / 2), exploreSize, exploreSize) });
            respawn();

        }

        public void respawn() {
            position = spawnPoint;
            maxHealth = Data.maxHealth;
            health = maxHealth;
            maxStamina = Data.maxStamina;
            stamina = maxStamina;
            maxThirst = Data.maxThirst;
            thirst = maxThirst;
            team = 0;
        }

        public void setSpawnPoint(Vector3 sp) {
            spawnPoint = sp;
        }

        public void setMapPos(Vector2 p) {
            mapPos = p;
        }



        void handleInput(float dt, float cameraYaw, out Input_t input) {
            input = new Input_t();
            for (int i = 0; i < (int)InputType.COUNT; i++) {
                if ((cur.buttons & (0x1 << i)) != 0) {
                    if ((last.buttons & (0x1 << i)) == 0) {
                        input.inputs[i] = InputState.JUST_PRESSED;
                    }
                    else {
                        input.inputs[i] = InputState.PRESSED;
                    }
                }
                else {
                    if ((last.buttons & (0x1 << i)) != 0) {
                        input.inputs[i] = InputState.JUST_RELEASED;
                    }
                    else {
                        input.inputs[i] = InputState.RELEASED;
                    }
                }
            }
            var forward = new Vector3(Mathf.Sin(cameraYaw), 0, Mathf.Cos(cameraYaw));
            var right = Vector3.Cross(Vector3.up, forward);
            input.movement += forward * (float)cur.fwd / 127f;
            input.movement += right * (float)cur.right / 127f;

            input.yaw = Mathf.Atan2(input.movement.x, input.movement.z);
        }


        override public void onLand() {
            // Land on ground
            var block = world.getBlock(position);
            if (!World.isCapBlock(block)) {
                block = world.getBlock(footPosition(position));
            }
            float d = -velocity.y / Data.fallDamageVelocity * World.getFallDamage(block);
            if (d > 0) {
                damage(d);
                useStamina((float)d);
                stun((float)d);
                world.camera.shake(0.2f, d * 0.2f, d * 0.05f);
            }
            else {
                world.camera.shake(0.15f, 0.05f, 0.01f);
            }
        }

        public WorldItem getInteractTarget() {
            float closestDist = 2;
            WorldItem closestItem = null;
            foreach (var i in world.items.GetComponentsInAllChildren<WorldItem>()) {
                float dist = (i.position - position).magnitude;
                if (dist < closestDist) {
                    closestDist = dist;
                    closestItem = i;
                }
            }
            return closestItem;

        }

        void updateStats(float dt) {
            //float time = dt / 60 / 24;
            //float sleep = 0;

            int itemCount = 0;
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] != null) {
                    itemCount++;
                }
            }
            weight = WeightClass.LIGHT;
            for (int i = 0; i < (int)WeightClass.COUNT; i++) {
                if (itemCount >= Data.weightClassItemCount[i]) {
                    weight = (WeightClass)i;
                }
            }

            if (health <= 0) {
                health = maxHealth;
                respawn();
            }

        }




        #endregion

        #region inventory

        void setInventorySlot(int index, Item item) {
            inventory[index] = item;
            onInventoryChange();
        }

        bool pickUp(Item item) {
            Money m;
            if ((m = item as Money) != null) {
                SetMoney(money + m.Data.count * m.count);
                return true;
            }

            Pack p;
            if ((p = item as Pack) != null) {
                int packSlots = 0;
                // find the first available pack slot (there might be empty slots in a previous pack)
                for (int i = (int)InventorySlot.PACK; i < MAX_INVENTORY_SIZE - (p.Data.slots + 1); i++) {
                    var j = inventory[i];
                    Pack p2;
                    if ((p2 = j as Pack) != null) {
                        packSlots = p2.Data.slots;
                    }
                    else {
                        if (packSlots == 0) {
                            setInventorySlot(i++, item);
                            foreach (var c in p.contained) {
                                setInventorySlot(i++, c);
                            }
                            return true;
                        }
                        packSlots--;
                    }
                }
                return false;
            }

            if (item is Clothing && inventory[(int)InventorySlot.CLOTHING] == null) {
                setInventorySlot((int)InventorySlot.CLOTHING, item);
                return true;
            }

            Weapon weapon;
            if ((weapon = item as Weapon) != null) {
                if (weapon.Data.hand == WeaponData.Hand.BOTH && inventory[(int)InventorySlot.LEFT_HAND] == null && inventory[(int)InventorySlot.RIGHT_HAND] == null) {
                    setInventorySlot((int)InventorySlot.RIGHT_HAND, item);
                    return true;
                }
                if (weapon.Data.hand == WeaponData.Hand.LEFT || weapon.Data.hand == WeaponData.Hand.RIGHT) {
                    int slotPreference1;
                    int slotPreference2;
                    if (weapon.Data.hand == WeaponData.Hand.LEFT) {
                        slotPreference1 = (int)InventorySlot.LEFT_HAND;
                        slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                    }
                    else {
                        slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                        slotPreference2 = (int)InventorySlot.LEFT_HAND;
                    }
                    if (inventory[slotPreference1] == null) {
                        setInventorySlot(slotPreference1, item);
                        return true;
                    }
                    if (inventory[slotPreference2] == null) {
                        setInventorySlot(slotPreference2, item);
                        return true;
                    }
                }
            }

            Loot loot;
            if ((loot = item as Loot) != null) {
                if (loot.Data.stackSize > 0) {
                    for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                        var item2 = inventory[i] as Loot;
                        if (item2 != null && item2.Data == item.Data) {
                            int numToTransfer = Math.Min(loot.count, item2.Data.stackSize - item2.count);
                            loot.count -= numToTransfer;
                            item2.count += numToTransfer;
                            if (loot.count == 0) {
                                return true;
                            }
                        }
                    }
                }
            }

            int[] slots = new int[1];
            if (findEmptyPackSlots(1, ref slots)) {
                setInventorySlot(slots[0], item);
                return true;
            }
            return false;
        }

        public bool use(Item item) {
            if (item == null) {
                return false;
            }

            if (item is Clothing
                || item is Pack
                || item is Weapon) {
                return equip(item);
            }

            Loot loot;
            if ((loot = item as Loot) != null && loot.use(this)) {
                loot.count--;
                if (loot.count <= 0) {
                    removeFromInventory(item);
                }
                return true;
            }
            return false;
        }

        void setItemSlot(Item item, int newSlot) {
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] == item) {
                    setInventorySlot(i, null);
                    break;
                }
            }

            setInventorySlot(newSlot, item);

            item.onSlotChange();
        }

        public bool equip(Item item) {
            int[] emptyPackSlots = new int[2];

            bool inInventory = false;
            int curSlot = -1;
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] == item) {
                    // unequip
                    if (i == (int)InventorySlot.CLOTHING || i == (int)InventorySlot.LEFT_HAND || i == (int)InventorySlot.RIGHT_HAND) {
                        if (findEmptyPackSlots(1, ref emptyPackSlots)) {
                            setItemSlot(inventory[i], emptyPackSlots[0]);
                            return true;
                        }
                        return false;
                    }

                    curSlot = i;
                    emptyPackSlots[0] = i;
                    inInventory = true;
                    break;
                }
            }

            Clothing clothing;
            if ((clothing = item as Clothing) != null) {
                if (inventory[(int)InventorySlot.CLOTHING] == null) {
                    setItemSlot(item, (int)InventorySlot.CLOTHING);
                    return true;
                }
                else {
                    if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                        if (inventory[(int)InventorySlot.CLOTHING] != null) {
                            setItemSlot(inventory[(int)InventorySlot.CLOTHING], emptyPackSlots[0]);
                        }
                        setItemSlot(item, (int)InventorySlot.CLOTHING);
                        return true;
                    }
                }
                return false;
            }

            Weapon weapon;
            if ((weapon = item as Weapon) != null) {

                if (weapon.Data.hand == WeaponData.Hand.BOTH) {
                    int slotsRequired = 0;
                    if (inventory[(int)InventorySlot.LEFT_HAND] != null) {
                        slotsRequired++;
                    }
                    if (inventory[(int)InventorySlot.RIGHT_HAND] != null) {
                        slotsRequired++;
                    }
                    int swapInventorySlot = emptyPackSlots[0];
                    if (inInventory) {
                        slotsRequired--;
                    }
                    if (slotsRequired <= 0 || findEmptyPackSlots(slotsRequired, ref emptyPackSlots)) {
                        if (inInventory) {
                            emptyPackSlots[1] = emptyPackSlots[0];
                            emptyPackSlots[0] = swapInventorySlot;
                        }
                        int slotIndex = 0;
                        if (inventory[(int)InventorySlot.LEFT_HAND] != null) {
                            setItemSlot(inventory[(int)InventorySlot.LEFT_HAND], emptyPackSlots[slotIndex++]);
                        }
                        if (inventory[(int)InventorySlot.RIGHT_HAND] != null) {
                            setItemSlot(inventory[(int)InventorySlot.RIGHT_HAND], emptyPackSlots[slotIndex++]);
                        }
                        setItemSlot(item, (int)InventorySlot.RIGHT_HAND);
                        return true;
                    }
                }
                else if (weapon.Data.hand == WeaponData.Hand.LEFT || weapon.Data.hand == WeaponData.Hand.RIGHT) {
                    int slotPreference1;
                    int slotPreference2;
                    if (weapon.Data.hand == WeaponData.Hand.LEFT) {
                        slotPreference1 = (int)InventorySlot.LEFT_HAND;
                        slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                    }
                    else {
                        slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                        slotPreference2 = (int)InventorySlot.LEFT_HAND;
                    }
                    var slot1Weapon = inventory[slotPreference1] as Weapon;
                    var slot2Weapon = inventory[slotPreference2] as Weapon;
                    var slotBothWeapon = inventory[(int)InventorySlot.RIGHT_HAND] as Weapon;

                    // if the item in our left hand is two-handed, unequip and equip the desired item in preferred hand
                    if (slotBothWeapon != null && slotBothWeapon.Data.hand == WeaponData.Hand.BOTH) {
                        if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                            setItemSlot(slotBothWeapon, emptyPackSlots[0]);
                            setItemSlot(weapon, (int)InventorySlot.RIGHT_HAND);
                            return true;
                        }
                    }
                    else {
                        // if our preferred slot is empty, equip in that slot
                        if (slot1Weapon == null) {
                            setItemSlot(weapon, slotPreference1);
                            return true;
                        }
                        // if our preferred slot is full but the secondary slot is empty
                        else if (slot2Weapon == null) {
                            // but the secondary slot has an off-hand item, rearrange (eg. move shield to left hand and equip sword in right)
                            if (slot1Weapon.Data.hand != weapon.Data.hand) {
                                setItemSlot(slot1Weapon, slotPreference2);
                                setItemSlot(weapon, slotPreference1);
                            }
                            // item in our primary hand is of same type, equip in off-hand
                            else {
                                setItemSlot(weapon, slotPreference2);
                            }
                            return true;
                        }
                        // if both slots are full
                        if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                            // if our preferred hand has an offhand item in it, unequip that and equip the item in preferred hand
                            if (slot1Weapon.Data.hand != weapon.Data.hand || slot2Weapon.Data.hand == weapon.Data.hand) {
                                setItemSlot(slot1Weapon, emptyPackSlots[0]);
                                setItemSlot(weapon, slotPreference1);

                            }
                            // our preferred hand has an item of the same type, unequip the secondary item and equip in secondary slot
                            else {
                                setItemSlot(slot2Weapon, emptyPackSlots[0]);
                                setItemSlot(weapon, slotPreference2);
                            }
                            return true;
                        }
                    }

                }
            }
            return false;
        }

        bool findEmptyPackSlots(int count, ref int[] slots) {
            int packSlots = 0;
            int emptySlotIndex = 0;
            for (int i = (int)InventorySlot.PACK; i < MAX_INVENTORY_SIZE; i++) {
                var item = inventory[i];
                if (item == null) {
                    if (packSlots > 0) {
                        slots[emptySlotIndex++] = i;
                        if (emptySlotIndex >= count) {
                            return true;
                        }
                    }
                    return false;
                }
                Pack pack;
                if ((pack = item as Pack) != null) {
                    packSlots = pack.Data.slots;
                }
                else {
                    packSlots--;
                }
            }
            return false;
        }

        void removeFromInventory(Item item) {
            int slot = 0;
            int packSlots = 0;


            for (var i = 0; i < MAX_INVENTORY_SIZE; i++) {
                var checkItem = inventory[i] as Pack;
                if (checkItem != null) {
                    packSlots = checkItem.Data.slots;
                }
                else {
                    packSlots--;
                }
                if (inventory[i] == item) {
                    slot = i;
                    break;
                }
            }

            Pack pack;
            if ((pack = item as Pack) != null) {
                for (int i = 0; i < pack.Data.slots; i++) {
                    var packItem = inventory[i + slot + 1];
                    if (packItem != null) {
                        pack.contained.Add(packItem);
                    }
                }
            }


            if (slot == (int)InventorySlot.CLOTHING) {
                setInventorySlot(slot, null);
            }
            else if (slot == (int)InventorySlot.LEFT_HAND) {
                setInventorySlot(slot, null);
            }
            else if (slot == (int)InventorySlot.RIGHT_HAND) {
                setInventorySlot(slot, null);
            }
            else if (pack != null) {
                for (int i = slot; i < MAX_INVENTORY_SIZE - packSlots - 1; i++) {
                    setInventorySlot(i, inventory[i + packSlots + 1]);
                }
            }
            else {
                for (int j = slot; j < slot + packSlots; j++) {
                    setInventorySlot(j, inventory[j + 1]);
                }
                setInventorySlot(slot + packSlots, null);
            }

            onInventoryChange.Invoke();
        }

        public void drop(Item item) {
            removeFromInventory(item);

            var worldItem = world.CreateWorldItem(item);
            worldItem.position = handPosition(position);
            worldItem.transform.parent = world.items.transform;
        }


        void interact() {
            var t = getInteractTarget();
            if (t != null) {
                if (pickUp(t.item)) {
                    Destroy(t);
                }
            }
            else {
                var block = world.getBlock(footPosition(position));
                if (block == EBlockType.BLOCK_TYPE_WATER) {
                    Loot waterItem = null;
                    var waterData = Loot.GetData("Water");
                    foreach (var i in inventory) {
                        var other = i as Loot;
                        if (other != null && i.Data == waterData && other.count < waterData.stackSize) {
                            waterItem = other;
                            break;
                        }
                    }
                    if (waterItem == null) {
                        int[] newSlot = new int[1];
                        if (findEmptyPackSlots(1, ref newSlot)) {
                            waterItem = Item.Create(waterData, world) as Loot;
                            pickUp(waterItem);
                        }
                    }
                    if (waterItem != null) {
                        waterItem.count = waterData.stackSize;
                    }
                }
            }
        }
        #endregion

        public void SetMoney(int m) {
            money = m;
            onMoneyChange();
        }
    }
}