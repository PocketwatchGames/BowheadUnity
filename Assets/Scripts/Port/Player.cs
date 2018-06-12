using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {
    public class Player : Actor {

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



        public class CState : Actor.CState {
            public Vector3 spawnPoint;
            public Vector2 mapPos;

            public int money;
            public float dropTimer;
            public int inventorySelected;

            public WeightClass weight;
            public float thirst;
            public float maxThirst;
            public float temperature;

        };

        public class CData : Actor.CData {
            public float maxThirst;
            public float temperatureSleepMinimum;
            public float temperatureSleepMaximum;
            public float fallDamageVelocity;
            public float dropTime;
            public int[] weightClassItemCount;
        };

        new public CData Data { get { return GetData<CData>(); } }
        new public CState State { get { return GetState<CState>(); } }
        new public static CData GetData(string DataName) {
            return GetData<CData>(DataName);
        }



        Actor attackTargetPreview;












        #region core functions


        public Player(CData d) : base(d, new CState()) {
            attackTargetPreview = null;
        }

        // TODO: move cameraYaw into the PlayerCmd struct
        public void update(float dt, float cameraYaw) {

            State.canMove = State.weight < WeightClass.IMMOBILE && !State.stunned;
            State.canAttack = State.weight < WeightClass.IMMOBILE;
            State.canRun = State.weight < WeightClass.ENCUMBERED;
            State.canJump = State.weight < WeightClass.ENCUMBERED;
            State.canClimb = State.weight < WeightClass.HEAVY;
            State.canSwim = State.weight < WeightClass.HEAVY;
            State.canClimbWell = State.weight < WeightClass.MEDIUM;
            State.canTurn = true;

            if (State.recovering) {
                State.canRun = false;
                State.canJump = false;
                State.canClimb = false;
                State.canClimbWell = false;
                State.canAttack = false;
            }
            if (State.stunned) {
                State.canRun = false;
                State.canJump = false;
                State.canClimb = false;
                State.canClimbWell = false;
                State.canAttack = false;
                State.canMove = false;
            }

            if (State.activity == Activity.SWIMMING || State.activity == Activity.CLIMBING) {
                State.canAttack = false;
            }


            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (State.inventory[i] != null) {
                    State.inventory[i].updateCast(dt, this);
                }
            }

            Input_t input;
            handleInput(dt, &input);

            if (input.inputs[(int)InputType::INTERACT] == InputState::JUST_PRESSED) {
                interact();
            }

            if (input.inputs[(int)InputType::SELECT_LEFT] == InputState::JUST_PRESSED) {
                selectPreviousInventory();
            }
            else if (input.inputs[(int)InputType::SELECT_RIGHT] == InputState::JUST_PRESSED) {
                selectNextInventory();
            }

            if (input.IsPressed(InputType::SWAP)) {
            }
            if (input.IsPressed(InputType::USE)) {
                State.dropTimer = State.dropTimer + dt;
            }
            else {
                if (input.inputs[(int)InputType::USE] == InputState::JUST_RELEASED) {
                    var item = State.inventory[State.inventorySelected];
                    if (item != null) {
                        if (State.dropTimer >= Data.dropTime) {
                            drop(item);
                        }
                        else {
                            use(item);
                        }
                    }
                }
                State.dropTimer = 0;
            }

            bool isCasting = false;
            Item itemRight = State.inventory[(int)InventorySlot.RIGHT_HAND];
            Item itemLeft = State.inventory[(int)InventorySlot.LEFT_HAND];
            if (State.canAttack) {
                if (itemLeft != null) {
                    if (input.IsPressed(InputType::ATTACK_LEFT)) {
                        itemLeft.charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType::ATTACK_LEFT] == InputState::JUST_RELEASED) {
                            itemLeft.attack(this);
                        }
                        itemLeft.State.chargeTime = 0;
                    }
                    if (itemLeft.State.castTime > 0) {
                        isCasting = true;
                    }
                }
                if (itemRight != null) {
                    if (input.IsPressed(InputType::ATTACK_RIGHT)) {
                        itemRight.charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType::ATTACK_RIGHT] == InputState::JUST_RELEASED) {
                            itemRight.attack(this);
                        }
                        itemRight.State.chargeTime = 0;
                    }
                    if (itemRight.State.castTime > 0) {
                        isCasting = true;
                    }
                }
            }

            attackTargetPreview = getAttackTarget(State.yaw);

            bool shouldLock = false;
            if (input.IsPressed(InputType::ATTACK_LEFT) || input.IsPressed(InputType::ATTACK_RIGHT)) {
                shouldLock = true;
            }
            if ((!shouldLock && !isCasting) || !isValidAttackTarget(State.attackTarget)) {
                State.attackTarget = null;
            }
            if (shouldLock && State.attackTarget == null) {
                State.attackTarget = attackTargetPreview;
            }
            State.lockedToTarget = State.attackTarget != null;



            base.update(dt, input);
            updateStats(dt);

        }

        bool isValidAttackTarget(Actor actor) {
            if (actor == null)
                return false;
            var diff = actor.State.position - State.position;
            if (diff.safeGetLength() > 20) {
                return false;
            }
            return true;
        }

        Actor getAttackTarget(float yaw) {

            float maxDist = 40;
            float maxTargetAngle = pi<float>();

            Actor bestTarget = null;
            float bestTargetAngle = maxTargetAngle;
            foreach (var c in world.critters) {
                var diff = c.State.position - State.position;
                float dist = diff.safeGetLength();
                if (dist < maxDist) {
                    float angleToEnemy = Math.Atan2(diff.y, diff.x);

                    float yawDiff = Math.Abs(constrainAngle(angleToEnemy - yaw));

                    // take the target's radius into account based on how far away they are
                    yawDiff = Math.Max(0.001f, yawDiff - Math.Atan2(c.Data.collisionRadius, dist));

                    float distT = (float)Math.Pow(dist / maxDist, 2);
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
            State.position = pos;
            State.spawned = true;

            setSpawnPoint(pos);

            // NOTE: andy these will leak unless you delete these somewhere.

            pickUp(new Item("Pack"));
            pickUp(new Item("Pack"));
            pickUp(new Item("Hat"));
            pickUp(new Item("Helmet"));
            pickUp(new Item("Sword"));
            pickUp(new Item("2HSword"));
            pickUp(new Item("Shield"));
            pickUp(new Item("Spear"));

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
            State.position = State.spawnPoint;
            State.maxHealth = Data.maxHealth;
            State.health = State.maxHealth;
            State.maxStamina = Data.maxStamina;
            State.stamina = State.maxStamina;
            State.maxThirst = Data.maxThirst;
            State.thirst = State.maxThirst;
            State.team = 0;
        }

        public void setSpawnPoint(Vector3 sp) {
            State.spawnPoint = sp;
        }

        public void setMapPos(Vector2 p) {
            State.mapPos = p;
        }



        bool Input_t::IsPressed(InputType i) {
            return inputs[(int)i] == InputState::PRESSED || inputs[(int)i] == InputState::JUST_PRESSED;
        }

        void handleInput(float dt, ref Input_t input) {
            for (int i = 0; i < (int)InputType::COUNT; i++) {
                if (cur.buttons & (0x1 << i)) {
                    if (!(State.last.buttons & (0x1 << i))) {
                        input.inputs[i] = InputState::JUST_PRESSED;
                    }
                    else {
                        input.inputs[i] = InputState::PRESSED;
                    }
                }
                else {
                    if ((State.last.buttons & (0x1 << i))) {
                        input.inputs[i] = InputState::JUST_RELEASED;
                    }
                    else {
                        input.inputs[i] = InputState::RELEASED;
                    }
                }
            }
            var forward = new Vector3((float)Math.Cos(cg.camera.yaw), (float)Math.Sin(cg.camera.yaw), 0);
            var right = Vector3.Cross(Vector3.down, forward);
            input.movement += forward * cur.fwd / 127f;
            input.movement += right * cur.right / 127f;
            input.yaw = (float)Math.Atan2(input.movement.y, input.movement.x);
        }


        void onLand() {
            // Land on ground
            var block = world.getBlock(State.position);
            if (!World::isCapBlock(block)) {
                block = world.getBlock(footPosition(State.position));
            }
            float d = -State.velocity.z / Data.fallDamageVelocity * World::getFallDamage(block);
            if (d > 0) {
                damage(d);
                useStamina((float)d);
                stun((float)d);
                cg.camera.shake(0.2f, d * 0.2f, d * 0.05f);
            }
            else {
                cg.camera.shake(0.15f, 0.05f, 0.01f);
            }
        }

        public Item getInteractTarget() {
            float closestDist = 2;
            Item closestItem = null;
            for (var i = world.items.begin(); i != world.items.end(); i++) {
                float dist = ((*i).State.position - State.position).getLength();
                if (dist < closestDist) {
                    closestDist = dist;
                    closestItem = *i;
                }
            }
            return closestItem;

        }

        void updateStats(float dt) {
            //float time = dt / 60 / 24;
            //float sleep = 0;

            int itemCount = 0;
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (State.inventory[i] != null) {
                    itemCount++;
                }
            }
            State.weight = WeightClass.LIGHT;
            for (int i = 0; i < (int)WeightClass.COUNT; i++) {
                if (itemCount >= Data.weightClassItemCount[i]) {
                    State.weight = (WeightClass)i;
                }
            }

            if (State.health <= 0) {
                State.health = State.maxHealth;
                respawn();
            }

        }




        #endregion

        #region inventory


        bool pickUp(Item item) {
            if (item.Data.itemType == Item.ItemType.MONEY) {
                State.money += item.State.count;
                return true;
            }

            if (item.Data.itemType == Item.ItemType.PACK) {
                int packSlots = 0;
                // find the first available pack slot (there might be empty slots in a previous pack)
                for (int i = (int)InventorySlot.PACK; i < MAX_INVENTORY_SIZE - (item.Data.slots + 1); i++) {
                    var j = State.inventory[i];
                    if (j != null && j.Data.itemType == Item.ItemType.PACK) {
                        packSlots = j.Data.slots;
                    }
                    else {
                        if (packSlots == 0) {
                            State.inventory[i++] = item;
                            foreach (var c in item.contained) {
                                State.inventory[i++] = c;
                            }
                            return true;
                        }
                        packSlots--;
                    }
                }
                return false;
            }

            if (item.Data.itemType == Item.ItemType.CLOTHING && State.inventory[(int)InventorySlot.CLOTHING] == null) {
                State.inventory[(int)InventorySlot.CLOTHING] = item;
                return true;
            }
            if (item.Data.itemType == Item.ItemType.BOTH_HANDS && State.inventory[(int)InventorySlot.LEFT_HAND] == null && State.inventory[(int)InventorySlot.RIGHT_HAND] == null) {
                State.inventory[(int)InventorySlot.CLOTHING] = item;
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
                if (State.inventory[slotPreference1] == null) {
                    State.inventory[slotPreference1] = item;
                    return true;
                }
                if (State.inventory[slotPreference2] == null) {
                    State.inventory[slotPreference2] = item;
                    return true;
                }
            }

            if (item.Data.slots > 0) {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    var item2 = State.inventory[i];
                    if (item2 != null && item2.Data == item.Data) {
                        int numToTransfer = Math.Min(item.State.count, item2.Data.slots - item2.State.count);
                        item.State.count -= numToTransfer;
                        item2.State.count += numToTransfer;
                        if (item.State.count == 0) {
                            return true;
                        }
                    }
                }
            }

            int[] slots = new int[1];
            if (findEmptyPackSlots(1, ref slots)) {
                State.inventory[slots[0]] = item;
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
                item.State.count--;
                if (item.State.count <= 0) {
                    removeFromInventory(item);
                }
                return true;
            }
            return false;
        }

        void setItemSlot(Item item, int newSlot) {
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (State.inventory[i] == item) {
                    State.inventory[i] = null;
                    break;
                }
            }

            State.inventory[newSlot] = item;

            item.State.castTime = 0;
            item.State.chargeTime = 0;
        }

        bool equip(Item item) {
            int[] emptyPackSlots = new int[2];

            bool inInventory = false;
            int curSlot = -1;
            for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                if (State.inventory[i] == item) {
                    // unequip
                    if (i == (int)InventorySlot.CLOTHING || i == (int)InventorySlot.LEFT_HAND || i == (int)InventorySlot.RIGHT_HAND) {
                        if (findEmptyPackSlots(1, ref emptyPackSlots)) {
                            setItemSlot(State.inventory[i], emptyPackSlots[0]);
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
                if (State.inventory[(int)InventorySlot.CLOTHING] == null) {
                    setItemSlot(item, (int)InventorySlot.CLOTHING);
                    return true;
                }
                else {
                    if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                        if (State.inventory[(int)InventorySlot.CLOTHING] != null) {
                            setItemSlot(State.inventory[(int)InventorySlot.CLOTHING], emptyPackSlots[0]);
                        }
                        setItemSlot(item, (int)InventorySlot.CLOTHING);
                        return true;
                    }
                }
            }
            else if (item.Data.itemType == Item.ItemType.BOTH_HANDS) {
                int slotsRequired = 0;
                if (State.inventory[(int)InventorySlot.LEFT_HAND] != null) {
                    slotsRequired++;
                }
                if (State.inventory[(int)InventorySlot.RIGHT_HAND] != null) {
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
                    if (State.inventory[(int)InventorySlot.LEFT_HAND] != null) {
                        setItemSlot(State.inventory[(int)InventorySlot.LEFT_HAND], emptyPackSlots[slotIndex++]);
                    }
                    if (State.inventory[(int)InventorySlot.RIGHT_HAND] != null) {
                        setItemSlot(State.inventory[(int)InventorySlot.RIGHT_HAND], emptyPackSlots[slotIndex++]);
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
                if (State.inventory[(int)InventorySlot.RIGHT_HAND] != null && State.inventory[(int)InventorySlot.RIGHT_HAND].Data.itemType == Item.ItemType.BOTH_HANDS) {
                    if (inInventory || findEmptyPackSlots(1, ref emptyPackSlots)) {
                        setItemSlot(State.inventory[(int)InventorySlot.RIGHT_HAND], emptyPackSlots[0]);
                        setItemSlot(item, slotPreference1);
                        return true;
                    }
                }
                else {
                    // if our preferred slot is empty, equip in that slot
                    if (State.inventory[slotPreference1] == null) {
                        setItemSlot(item, slotPreference1);
                        return true;
                    }
                    // if our preferred slot is full but the secondary slot is empty
                    else if (State.inventory[slotPreference2] == null) {
                        // but the secondary slot has an off-hand item, rearrange (eg. move shield to left hand and equip sword in right)
                        if (State.inventory[slotPreference1].Data.itemType != item.Data.itemType) {
                            setItemSlot(State.inventory[slotPreference1], slotPreference2);
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
                        if (State.inventory[slotPreference1].Data.itemType != item.Data.itemType || State.inventory[slotPreference2].Data.itemType == item.Data.itemType) {
                            setItemSlot(State.inventory[slotPreference1], emptyPackSlots[0]);
                            setItemSlot(item, slotPreference1);

                        }
                        // our preferred hand has an item of the same type, unequip the secondary item and equip in secondary slot
                        else {
                            setItemSlot(State.inventory[slotPreference2], emptyPackSlots[0]);
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
                var item = State.inventory[i];
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
                var checkItem = State.inventory[i];
                if (checkItem != null && checkItem.Data.itemType == Item.ItemType.PACK) {
                    packSlots = checkItem.Data.slots;
                }
                else {
                    packSlots--;
                }
                if (State.inventory[i] == item) {
                    slot = i;
                    break;
                }
            }


            if (item.Data.itemType == Item.ItemType.PACK) {
                for (int i = 0; i < item.Data.slots; i++) {
                    var packItem = State.inventory[i + slot + 1];
                    if (packItem != null) {
                        item.contained.push_back(packItem);
                    }
                }
            }


            if (slot == (int)InventorySlot.CLOTHING) {
                State.inventory[slot] = null;
            }
            else if (slot == (int)InventorySlot.LEFT_HAND) {
                State.inventory[slot] = null;
            }
            else if (slot == (int)InventorySlot.RIGHT_HAND) {
                State.inventory[slot] = null;
            }
            else if (item.Data.itemType == Item.ItemType.PACK) {
                for (int i = slot; i < MAX_INVENTORY_SIZE - packSlots - 1; i++) {
                    State.inventory[i] = State.inventory[i + packSlots + 1];
                }
            }
            else {
                for (int j = slot; j < slot + packSlots; j++) {
                    State.inventory[j] = State.inventory[j + 1];
                }
                State.inventory[slot + packSlots] = null;
            }

            if (State.inventory[State.inventorySelected] == null) {
                selectPreviousInventory();
            }
        }

        void drop(Item item) {
            removeFromInventory(item);

            item.State.position = handPosition(State.position);
            world.items.push_back(item);
        }


        void selectPreviousInventory() {
            if (State.dropTimer >= Data.dropTime) {
                var curItem = State.inventory[State.inventorySelected];
                if (curItem != null) {
                    if (curItem.Data.itemType == Item.ItemType.PACK) {
                        int newSlot = State.inventorySelected - 1;
                        while (newSlot >= 0 && (State.inventory[newSlot] == null || State.inventory[newSlot].Data.itemType != Item.ItemType.PACK)) {
                            newSlot--;
                        }
                        if (newSlot >= 0) {
                            List<Item> newInventory = new List<Item>();
                            for (int i = 0; i < newSlot; i++) {
                                newInventory.Add(State.inventory[i]);
                            }
                            for (int i = 0; i < curItem.Data.slots + 1; i++) {
                                newInventory.Add(State.inventory[i + State.inventorySelected]);
                            }
                            for (int i = newSlot; i < MAX_INVENTORY_SIZE; i++) {
                                if (i < State.inventorySelected || i > State.inventorySelected + curItem.Data.slots) {
                                    newInventory.Add(State.inventory[i]);
                                }
                            }
                            int index = 0;
                            foreach (var i in newInventory) {
                                State.inventory[index++] = i;
                            }
                            State.inventorySelected = newSlot;
                        }
                    }
                    else {
                        int newSlot = State.inventorySelected - 1;
                        for (; newSlot > (int)InventorySlot.PACK; newSlot--) {
                            var itemInNewSlot = State.inventory[newSlot];
                            if (itemInNewSlot != null) {
                                if (itemInNewSlot.Data.itemType == Item.ItemType.PACK) {
                                    continue;
                                }
                            }
                            State.inventory[State.inventorySelected] = itemInNewSlot;
                            State.inventory[newSlot] = curItem;
                            State.inventorySelected = newSlot;
                            return;

                        }
                    }
                }
            }
            else {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    State.inventorySelected--;
                    if (State.inventorySelected < 0) {
                        State.inventorySelected = MAX_INVENTORY_SIZE - 1;
                    }
                    if (State.inventory[State.inventorySelected] != null) {
                        break;
                    }
                }
                State.dropTimer = 0;
            }
        }

        void selectNextInventory() {
            if (State.dropTimer >= Data.dropTime) {
                var curItem = State.inventory[State.inventorySelected];
                if (curItem != null) {
                    if (curItem.Data.itemType == Item.ItemType.PACK) {
                        int newSlot = State.inventorySelected + 1;
                        while (newSlot < MAX_INVENTORY_SIZE && (State.inventory[newSlot] == null || State.inventory[newSlot].Data.itemType != Item.ItemType.PACK)) {
                            newSlot++;
                        }
                        if (newSlot < MAX_INVENTORY_SIZE) {
                            List<Item> newInventory = new List<Item>();
                            for (int i = 0; i < State.inventorySelected; i++) {
                                newInventory.Add(State.inventory[i]);
                            }
                            for (int i = newSlot; i < newSlot + State.inventory[newSlot].Data.slots + 1; i++) {
                                newInventory.Add(State.inventory[i]);
                            }
                            for (int i = 0; i < curItem.Data.slots + 1; i++) {
                                newInventory.Add(State.inventory[i + State.inventorySelected]);
                            }
                            for (int i = newSlot + State.inventory[newSlot].Data.slots + 1; i < MAX_INVENTORY_SIZE; i++) {
                                newInventory.Add(State.inventory[i]);
                            }
                            int index = 0;
                            foreach (var i in newInventory) {
                                State.inventory[index++] = i;
                            }
                            State.inventorySelected = newSlot;
                        }
                    }
                    else {
                        int lastPackSlot;
                        int curPackSlotsRemaining = 0;
                        for (lastPackSlot = (int)InventorySlot.PACK; lastPackSlot < MAX_INVENTORY_SIZE; lastPackSlot++) {
                            var item = State.inventory[lastPackSlot];
                            if (item != null && item.Data.itemType == Item.ItemType.PACK) {
                                curPackSlotsRemaining = item.Data.slots;
                                continue;
                            }
                            if (curPackSlotsRemaining == 0) {
                                if (State.inventorySelected < (int)InventorySlot.PACK) {
                                    return;
                                }
                                lastPackSlot--;
                                break;
                            }
                            curPackSlotsRemaining--;
                            if (item == null && lastPackSlot > State.inventorySelected) {
                                break;
                            }
                        }
                        int oldSlot = State.inventorySelected;
                        int newSlot = oldSlot + 1;
                        if (oldSlot < (int)InventorySlot.PACK) {
                            // if we are moving an equipped item to the pack, bump everything right
                            int emptySlot = (int)InventorySlot.PACK + 1;
                            for (emptySlot; emptySlot < lastPackSlot; emptySlot++) {
                                if (State.inventory[emptySlot] == null) {
                                    break;
                                }
                            }
                            int lastEmptySlot = emptySlot;
                            for (int curSlot = emptySlot - 1; curSlot > (int)InventorySlot.PACK; curSlot--) {
                                if (State.inventory[curSlot].Data.itemType == Item.ItemType.PACK) {
                                    continue;
                                }
                                State.inventory[lastEmptySlot] = State.inventory[curSlot];
                                lastEmptySlot = curSlot;
                            }
                            State.inventorySelected = (int)InventorySlot.PACK + 1;
                            State.inventory[(int)InventorySlot.PACK + 1] = curItem;
                            State.inventory[oldSlot] = null;
                        }
                        else {
                            for (newSlot; newSlot <= lastPackSlot; newSlot++) {
                                var itemInNewSlot = State.inventory[newSlot];
                                if (itemInNewSlot != null) {
                                    if (itemInNewSlot.Data.itemType == Item.ItemType.PACK) {
                                        continue;
                                    }
                                }
                                State.inventory[oldSlot] = itemInNewSlot;
                                State.inventory[newSlot] = curItem;
                                State.inventorySelected = newSlot;
                                return;

                            }
                        }

                    }
                }
            }
            else {
                for (int i = 0; i < MAX_INVENTORY_SIZE; i++) {
                    State.inventorySelected++;
                    if (State.inventorySelected >= MAX_INVENTORY_SIZE) {
                        State.inventorySelected = 0;
                    }
                    if (State.inventory[State.inventorySelected] != null) {
                        break;
                    }
                }
                State.dropTimer = 0;
            }
        }

        void interact() {
            var t = getInteractTarget();
            if (t != null) {
                if (pickUp(t)) {
                    world.items.remove(t);
                }
            }
            else {
                var block = world.getBlock(footPosition(State.position));
                if (block == EBlockType::BLOCK_TYPE_WATER) {
                    Item waterItem = null;
                    var waterData = Item.GetData("Water");
                    foreach (var i in State.inventory) {
                        if (i != null && i.Data == waterData && i.State.count < waterData.slots) {
                            waterItem = i;
                            break;
                        }
                    }
                    if (waterItem == null) {
                        int[] newSlot = new int[1];
                        if (findEmptyPackSlots(1, ref newSlot)) {
                            waterItem = new Item(waterData);
                            pickUp(waterItem);
                        }
                    }
                    if (waterItem != null) {
                        waterItem.State.count = waterData.slots;
                    }
                }
            }
        }
        #endregion


    }
}