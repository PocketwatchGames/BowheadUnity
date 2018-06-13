using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {



    public class Player : Actor {

        #region State
        public Vector3 spawnPoint;
        public Vector2 mapPos;

        public int money;
        public float dropTimer;
        public int inventorySelected;

        public Player.WeightClass weight;
        public float thirst;
        public float maxThirst;
        public float temperature;
        #endregion


        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }


        public enum InventorySlot {
            CLOTHING = 0,
            LEFT_HAND = 1,
            RIGHT_HAND = 2,
            PACK = 3
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












        #region core functions

        public void init(PlayerData data, World world) {
            base.init(data, world);
            attackTargetPreview = null;
        }

        // TODO: move cameraYaw into the PlayerCmd struct
        public void update(float dt, float cameraYaw) {

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

            if (input.inputs[(int)InputType.SELECT_LEFT] == InputState.JUST_PRESSED) {
                selectPreviousInventory();
            }
            else if (input.inputs[(int)InputType.SELECT_RIGHT] == InputState.JUST_PRESSED) {
                selectNextInventory();
            }

            if (input.IsPressed(InputType.SWAP)) {
            }
            if (input.IsPressed(InputType.USE)) {
                dropTimer = dropTimer + dt;
            }
            else {
                if (input.inputs[(int)InputType.USE] == InputState.JUST_RELEASED) {
                    var item = inventory[inventorySelected];
                    if (item != null) {
                        if (dropTimer >= Data.dropTime) {
                            drop(item);
                        }
                        else {
                            use(item);
                        }
                    }
                }
                dropTimer = 0;
            }

            bool isCasting = false;
            Item itemRight = inventory[(int)InventorySlot.RIGHT_HAND];
            Item itemLeft = inventory[(int)InventorySlot.LEFT_HAND];
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



            base.Update(dt, input);
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
            var forward = new Vector3(Mathf.Sin(cameraYaw), 0, Mathf.Sin(cameraYaw));
            var right = Vector3.Cross(Vector3.down, forward);
            input.movement += forward * cur.fwd / 127f;
            input.movement += right * cur.right / 127f;
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

        public Item getInteractTarget() {
            float closestDist = 2;
            Item closestItem = null;
            foreach (var i in world.items.GetComponentsInAllChildren<Item>()) {
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


        bool pickUp(Item item) {
            if (item.Data.itemType == Item.ItemType.MONEY) {
                money += item.count;
                return true;
            }

            if (item.Data.itemType == Item.ItemType.PACK) {
                int packSlots = 0;
                // find the first available pack slot (there might be empty slots in a previous pack)
                for (int i = (int)InventorySlot.PACK; i < MAX_INVENTORY_SIZE - (item.Data.slots + 1); i++) {
                    var j = inventory[i];
                    if (j != null && j.Data.itemType == Item.ItemType.PACK) {
                        packSlots = j.Data.slots;
                    }
                    else {
                        if (packSlots == 0) {
                            inventory[i++] = item;
                            foreach (var c in item.contained) {
                                inventory[i++] = c;
                            }
                            return true;
                        }
                        packSlots--;
                    }
                }
                return false;
            }

            if (item.Data.itemType == Item.ItemType.CLOTHING && inventory[(int)InventorySlot.CLOTHING] == null) {
                inventory[(int)InventorySlot.CLOTHING] = item;
                return true;
            }
            if (item.Data.itemType == Item.ItemType.BOTH_HANDS && inventory[(int)InventorySlot.LEFT_HAND] == null && inventory[(int)InventorySlot.RIGHT_HAND] == null) {
                inventory[(int)InventorySlot.CLOTHING] = item;
                return true;
            }
            if (item.Data.itemType == Item.ItemType.LEFT_HAND || item.Data.itemType == Item.ItemType.RIGHT_HAND) {
                int slotPreference1;
                int slotPreference2;
                if (item.Data.itemType == Item.ItemType.LEFT_HAND) {
                    slotPreference1 = (int)InventorySlot.LEFT_HAND;
                    slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                }
                else {
                    slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                    slotPreference2 = (int)InventorySlot.LEFT_HAND;
                }
                if (inventory[slotPreference1] == null) {
                    inventory[slotPreference1] = item;
                    return true;
                }
                if (inventory[slotPreference2] == null) {
                    inventory[slotPreference2] = item;
                    return true;
                }
            }

            if (item.Data.slots > 0) {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    var item2 = inventory[i];
                    if (item2 != null && item2.Data == item.Data) {
                        int numToTransfer = Math.Min(item.count, item2.Data.slots - item2.count);
                        item.count -= numToTransfer;
                        item2.count += numToTransfer;
                        if (item.count == 0) {
                            return true;
                        }
                    }
                }
            }

            int[] slots = new int[1];
            if (findEmptyPackSlots(1, ref slots)) {
                inventory[slots[0]] = item;
                return true;
            }
            return false;
        }

        bool use(Item item) {
            if (item == null) {
                return false;
            }

            if (item.Data.itemType == Item.ItemType.CLOTHING
                || item.Data.itemType == Item.ItemType.PACK
                || item.Data.itemType == Item.ItemType.LEFT_HAND
                || item.Data.itemType == Item.ItemType.RIGHT_HAND
                || item.Data.itemType == Item.ItemType.BOTH_HANDS) {
                return equip(item);
            }

            if (item.use(this)) {
                item.count--;
                if (item.count <= 0) {
                    removeFromInventory(item);
                }
                return true;
            }
            return false;
        }

        void setItemSlot(Item item, int newSlot) {
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (inventory[i] == item) {
                    inventory[i] = null;
                    break;
                }
            }

            inventory[newSlot] = item;

            item.castTime = 0;
            item.chargeTime = 0;
        }

        bool equip(Item item) {
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

            if (item.Data.itemType == Item.ItemType.CLOTHING) {
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
            }
            else if (item.Data.itemType == Item.ItemType.BOTH_HANDS) {
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
            else if (item.Data.itemType == Item.ItemType.LEFT_HAND || item.Data.itemType == Item.ItemType.RIGHT_HAND) {
                int slotPreference1;
                int slotPreference2;
                if (item.Data.itemType == Item.ItemType.LEFT_HAND) {
                    slotPreference1 = (int)InventorySlot.LEFT_HAND;
                    slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                }
                else {
                    slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                    slotPreference2 = (int)InventorySlot.LEFT_HAND;
                }

                // if the item in our left hand is two-handed, unequip and equip the desired item in preferred hand
                if (inventory[(int)InventorySlot.RIGHT_HAND] != null && inventory[(int)InventorySlot.RIGHT_HAND].Data.itemType == Item.ItemType.BOTH_HANDS) {
                    if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                        setItemSlot(inventory[(int)InventorySlot.RIGHT_HAND], emptyPackSlots[0]);
                        setItemSlot(item, slotPreference1);
                        return true;
                    }
                }
                else {
                    // if our preferred slot is empty, equip in that slot
                    if (inventory[slotPreference1] == null) {
                        setItemSlot(item, slotPreference1);
                        return true;
                    }
                    // if our preferred slot is full but the secondary slot is empty
                    else if (inventory[slotPreference2] == null) {
                        // but the secondary slot has an off-hand item, rearrange (eg. move shield to left hand and equip sword in right)
                        if (inventory[slotPreference1].Data.itemType != item.Data.itemType) {
                            setItemSlot(inventory[slotPreference1], slotPreference2);
                            setItemSlot(item, slotPreference1);
                        }
                        // item in our primary hand is of same type, equip in off-hand
                        else {
                            setItemSlot(item, slotPreference2);
                        }
                        return true;
                    }
                    // if both slots are full
                    if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                        // if our preferred hand has an offhand item in it, unequip that and equip the item in preferred hand
                        if (inventory[slotPreference1].Data.itemType != item.Data.itemType || inventory[slotPreference2].Data.itemType == item.Data.itemType) {
                            setItemSlot(inventory[slotPreference1], emptyPackSlots[0]);
                            setItemSlot(item, slotPreference1);

                        }
                        // our preferred hand has an item of the same type, unequip the secondary item and equip in secondary slot
                        else {
                            setItemSlot(inventory[slotPreference2], emptyPackSlots[0]);
                            setItemSlot(item, slotPreference2);
                        }
                        return true;
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
                if (item.Data.itemType == Item.ItemType.PACK) {
                    packSlots = item.Data.slots;
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
                var checkItem = inventory[i];
                if (checkItem != null && checkItem.Data.itemType == Item.ItemType.PACK) {
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


            if (item.Data.itemType == Item.ItemType.PACK) {
                for (int i = 0; i < item.Data.slots; i++) {
                    var packItem = inventory[i + slot + 1];
                    if (packItem != null) {
                        item.contained.Add(packItem);
                    }
                }
            }


            if (slot == (int)InventorySlot.CLOTHING) {
                inventory[slot] = null;
            }
            else if (slot == (int)InventorySlot.LEFT_HAND) {
                inventory[slot] = null;
            }
            else if (slot == (int)InventorySlot.RIGHT_HAND) {
                inventory[slot] = null;
            }
            else if (item.Data.itemType == Item.ItemType.PACK) {
                for (int i = slot; i < MAX_INVENTORY_SIZE - packSlots - 1; i++) {
                    inventory[i] = inventory[i + packSlots + 1];
                }
            }
            else {
                for (int j = slot; j < slot + packSlots; j++) {
                    inventory[j] = inventory[j + 1];
                }
                inventory[slot + packSlots] = null;
            }

            if (inventory[inventorySelected] == null) {
                selectPreviousInventory();
            }
        }

        void drop(Item item) {
            removeFromInventory(item);

            item.position = handPosition(position);
            item.transform.parent = world.items.transform;
        }


        void selectPreviousInventory() {
            if (dropTimer >= Data.dropTime) {
                var curItem = inventory[inventorySelected];
                if (curItem != null) {
                    if (curItem.Data.itemType == Item.ItemType.PACK) {
                        int newSlot = inventorySelected - 1;
                        while (newSlot >= 0 && (inventory[newSlot] == null || inventory[newSlot].Data.itemType != Item.ItemType.PACK)) {
                            newSlot--;
                        }
                        if (newSlot >= 0) {
                            List<Item> newInventory = new List<Item>();
                            for (int i = 0; i < newSlot; i++) {
                                newInventory.Add(inventory[i]);
                            }
                            for (int i = 0; i < curItem.Data.slots + 1; i++) {
                                newInventory.Add(inventory[i + inventorySelected]);
                            }
                            for (int i = newSlot; i < MAX_INVENTORY_SIZE; i++) {
                                if (i < inventorySelected || i > inventorySelected + curItem.Data.slots) {
                                    newInventory.Add(inventory[i]);
                                }
                            }
                            int index = 0;
                            foreach (var i in newInventory) {
                                inventory[index++] = i;
                            }
                            inventorySelected = newSlot;
                        }
                    }
                    else {
                        int newSlot = inventorySelected - 1;
                        for (; newSlot > (int)InventorySlot.PACK; newSlot--) {
                            var itemInNewSlot = inventory[newSlot];
                            if (itemInNewSlot != null) {
                                if (itemInNewSlot.Data.itemType == Item.ItemType.PACK) {
                                    continue;
                                }
                            }
                            inventory[inventorySelected] = itemInNewSlot;
                            inventory[newSlot] = curItem;
                            inventorySelected = newSlot;
                            return;

                        }
                    }
                }
            }
            else {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    inventorySelected--;
                    if (inventorySelected < 0) {
                        inventorySelected = MAX_INVENTORY_SIZE - 1;
                    }
                    if (inventory[inventorySelected] != null) {
                        break;
                    }
                }
                dropTimer = 0;
            }
        }

        void selectNextInventory() {
            if (dropTimer >= Data.dropTime) {
                var curItem = inventory[inventorySelected];
                if (curItem != null) {
                    if (curItem.Data.itemType == Item.ItemType.PACK) {
                        int newSlot = inventorySelected + 1;
                        while (newSlot < MAX_INVENTORY_SIZE && (inventory[newSlot] == null || inventory[newSlot].Data.itemType != Item.ItemType.PACK)) {
                            newSlot++;
                        }
                        if (newSlot < MAX_INVENTORY_SIZE) {
                            List<Item> newInventory = new List<Item>();
                            for (int i = 0; i < inventorySelected; i++) {
                                newInventory.Add(inventory[i]);
                            }
                            for (int i = newSlot; i < newSlot + inventory[newSlot].Data.slots + 1; i++) {
                                newInventory.Add(inventory[i]);
                            }
                            for (int i = 0; i < curItem.Data.slots + 1; i++) {
                                newInventory.Add(inventory[i + inventorySelected]);
                            }
                            for (int i = newSlot + inventory[newSlot].Data.slots + 1; i < MAX_INVENTORY_SIZE; i++) {
                                newInventory.Add(inventory[i]);
                            }
                            int index = 0;
                            foreach (var i in newInventory) {
                                inventory[index++] = i;
                            }
                            inventorySelected = newSlot;
                        }
                    }
                    else {
                        int lastPackSlot;
                        int curPackSlotsRemaining = 0;
                        for (lastPackSlot = (int)InventorySlot.PACK; lastPackSlot < MAX_INVENTORY_SIZE; lastPackSlot++) {
                            var item = inventory[lastPackSlot];
                            if (item != null && item.Data.itemType == Item.ItemType.PACK) {
                                curPackSlotsRemaining = item.Data.slots;
                                continue;
                            }
                            if (curPackSlotsRemaining == 0) {
                                if (inventorySelected < (int)InventorySlot.PACK) {
                                    return;
                                }
                                lastPackSlot--;
                                break;
                            }
                            curPackSlotsRemaining--;
                            if (item == null && lastPackSlot > inventorySelected) {
                                break;
                            }
                        }
                        int oldSlot = inventorySelected;
                        int newSlot = oldSlot + 1;
                        if (oldSlot < (int)InventorySlot.PACK) {
                            // if we are moving an equipped item to the pack, bump everything right
                            int emptySlot = (int)InventorySlot.PACK + 1;
                            for (; emptySlot < lastPackSlot; emptySlot++) {
                                if (inventory[emptySlot] == null) {
                                    break;
                                }
                            }
                            int lastEmptySlot = emptySlot;
                            for (int curSlot = emptySlot - 1; curSlot > (int)InventorySlot.PACK; curSlot--) {
                                if (inventory[curSlot].Data.itemType == Item.ItemType.PACK) {
                                    continue;
                                }
                                inventory[lastEmptySlot] = inventory[curSlot];
                                lastEmptySlot = curSlot;
                            }
                            inventorySelected = (int)InventorySlot.PACK + 1;
                            inventory[(int)InventorySlot.PACK + 1] = curItem;
                            inventory[oldSlot] = null;
                        }
                        else {
                            for (; newSlot <= lastPackSlot; newSlot++) {
                                var itemInNewSlot = inventory[newSlot];
                                if (itemInNewSlot != null) {
                                    if (itemInNewSlot.Data.itemType == Item.ItemType.PACK) {
                                        continue;
                                    }
                                }
                                inventory[oldSlot] = itemInNewSlot;
                                inventory[newSlot] = curItem;
                                inventorySelected = newSlot;
                                return;

                            }
                        }

                    }
                }
            }
            else {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    inventorySelected++;
                    if (inventorySelected >= MAX_INVENTORY_SIZE) {
                        inventorySelected = 0;
                    }
                    if (inventory[inventorySelected] != null) {
                        break;
                    }
                }
                dropTimer = 0;
            }
        }

        void interact() {
            var t = getInteractTarget();
            if (t != null) {
                if (pickUp(t)) {
                    t.transform.parent = null;
                }
            }
            else {
                var block = world.getBlock(footPosition(position));
                if (block == EBlockType.BLOCK_TYPE_WATER) {
                    Item waterItem = null;
                    var waterData = Item.GetData("Water");
                    foreach (var i in inventory) {
                        if (i != null && i.Data == waterData && i.count < waterData.slots) {
                            waterItem = i;
                            break;
                        }
                    }
                    if (waterItem == null) {
                        int[] newSlot = new int[1];
                        if (findEmptyPackSlots(1, ref newSlot)) {
                            waterItem = world.CreateItem(waterData);
                            pickUp(waterItem);
                        }
                    }
                    if (waterItem != null) {
                        waterItem.count = waterData.slots;
                    }
                }
            }
        }
        #endregion


    }
}