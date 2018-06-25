using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

	public class Player : Pawn<Player, PlayerData> {

		public override Type serverType => typeof(Player);
		public override Type clientType => typeof(Player);

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
		        
		World.Streaming.Volume _worldStreaming;

        Pawn attackTargetPreview;
		bool _hackDidFindGround;

        public event Action OnMoneyChange;
        public event Action OnWeightClassChange;

        public delegate void OnLandFn(float damage);
        public event OnLandFn OnLand;

		#region core functions

		public Player() {
			SetReplicates(true);
		}

		public override void Tick() {
			base.Tick();
			if (!hasAuthority) {
				return;
			}

			if (!_hackDidFindGround) {
				if (WorldUtils.GetFirstSolidBlockDown(1000, ref spawnPoint)) {
					spawnPoint += Vector3.up;
					position = spawnPoint;
					Respawn();
					_hackDidFindGround = true;

                    var horseData = CritterData.Get("horse");
                    var c = gameMode.SpawnCritter(horseData, position, team);
                    var weapon = PackData.Get("Pack").CreateItem();
                    c.SetInventorySlot(0, weapon);

                }
                else {
					return;
				}
			}

			PlayerCmd_t cmd = new PlayerCmd_t();
            Vector2 move = new Vector2(Input.GetAxis("MoveHorizontal"), Input.GetAxis("MoveVertical"));
            cmd.fwd = (sbyte)(move.y * 127);
            cmd.right = (sbyte)(move.x * 127);
            if (Input.GetButton("Jump")) {
                cmd.buttons |= 1 << (int)InputType.Jump;
            }
            if (Input.GetButton("AttackLeft")) {
                cmd.buttons |= 1 << (int)InputType.AttackLeft;
            }
            if (Input.GetButton("AttackRight")) {
                cmd.buttons |= 1 << (int)InputType.AttackRight;
            }
            if (Input.GetButton("Interact")) {
                cmd.buttons |= 1 << (int)InputType.Interact;
            }
            if (Input.GetButton("Map")) {
                cmd.buttons |= 1 << (int)InputType.Map;
            }

			UpdatePlayerCmd(cmd);

			// HACK we should not be directly reading the camera yaw like this in a multiplayer game!
			if (Client.Actors.ClientPlayerController.localPlayer != null) {
				Tick(world.deltaTime, Client.Actors.ClientPlayerController.localPlayer.cameraController.GetYaw());
			}

			// despawn critters
			foreach (var c in world.GetActorIterator<Critter>()) {
				var diff = c.position - position;
				if (diff.magnitude > 500) {
					c.Destroy();
				}
			}

			gameMode.SpawnRandomCritter();
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

            if (activity == Activity.Swimming || activity == Activity.Climbing) {
                canAttack = false;
            }


            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) != null) {
                    GetInventorySlot(i).UpdateCast(dt, this);
                }
            }

            Input_t input;
            var forward = new Vector3(Mathf.Sin(cameraYaw), 0, Mathf.Cos(cameraYaw));
            UpdateBrain(dt, forward, out input);
            if (mount != null) {
                mount.Tick(dt, input);
            }

            if (input.inputs[(int)InputType.Interact] == InputState.JustPressed) {
                Interact();
            }

            if (input.IsPressed(InputType.Swap)) {
            }

            bool isCasting = false;
            var itemRight = GetInventorySlot((int)InventorySlot.RIGHT_HAND) as Weapon;
            var itemLeft = GetInventorySlot((int)InventorySlot.LEFT_HAND) as Weapon;
            if (canAttack) {
                if (itemLeft != null) {
                    if (input.IsPressed(InputType.AttackLeft)) {
                        itemLeft.Charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType.AttackLeft] == InputState.JustReleased) {
                            itemLeft.Attack(this);
                        }
                        itemLeft.chargeTime = 0;
                    }
                    if (itemLeft.castTime > 0) {
                        isCasting = true;
                    }
                }
                if (itemRight != null) {
                    if (input.IsPressed(InputType.AttackRight)) {
                        itemRight.Charge(dt);
                    }
                    else {
                        if (input.inputs[(int)InputType.AttackRight] == InputState.JustReleased) {
                            itemRight.Attack(this);
                        }
                        itemRight.chargeTime = 0;
                    }
                    if (itemRight.castTime > 0) {
                        isCasting = true;
                    }
                }
            }

            attackTargetPreview = GetAttackTarget(yaw);

            bool shouldLock = false;
            if (input.IsPressed(InputType.AttackLeft) || input.IsPressed(InputType.AttackRight)) {
                shouldLock = true;
            }
            if ((!shouldLock && !isCasting) || !IsValidAttackTarget(attackTarget)) {
                attackTarget = null;
            }
            if (shouldLock && attackTarget == null) {
                attackTarget = attackTargetPreview;
            }
            lockedToTarget = attackTarget != null;



            base.Tick(dt, input);
            UpdateStats(dt);

        }

		////////////
		// Spawning
		////////////

		public override void ServerSpawn(Vector3 pos, EntityData baseData) {
			base.ServerSpawn(pos, baseData);

			AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity, null));

			attackTargetPreview = null;

			// JOSEPH: this will be better once this is moved into new Actor framework, for now HACK
			_worldStreaming = Bowhead.GameManager.instance.serverWorld.worldStreaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y);
			_worldStreaming.position = default(WorldChunkPos_t);

			position = pos;

            SetSpawnPoint(pos);

			PickUp(ItemData.Get("Pack").CreateItem());
			PickUp(ItemData.Get("Hat").CreateItem());
			PickUp(ItemData.Get("Helmet").CreateItem());
			PickUp(ItemData.Get("Sword").CreateItem());
			PickUp(ItemData.Get("2HSword").CreateItem());
			PickUp(ItemData.Get("Shield").CreateItem());
			PickUp(ItemData.Get("Spear").CreateItem());

			//Equip(new game.items.Clothing("Cloak"));
			//AddInventory(new Clothing("Backpack"));
			//Equip(new game.items.Weapon("PickAxe"));
			//Equip(new game.items.Parachute("Parachute"));
			//AddInventory(new Jetpack("Jetpack"));
			//SetMapPos(new Vector2(spawnPoint.X, spawnPoint.Z));
			//int exploreSize = 1024;
			//Explore(this, new EventArgsExplore(){ region = new Rectangle((int)(mapPos.X - exploreSize / 2), (int)(mapPos.Y - exploreSize / 2), exploreSize, exploreSize) });
			Respawn();
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			if (_worldStreaming != null) {
				_worldStreaming.Dispose();
				_worldStreaming = null;
			}
		}

		public void Respawn() {
            position = spawnPoint;
            maxHealth = data.maxHealth;
            health = maxHealth;
            maxStamina = data.maxStamina;
            stamina = maxStamina;
            maxThirst = data.maxThirst;
            thirst = maxThirst;
        }

        public void SetSpawnPoint(Vector3 sp) {
            spawnPoint = sp;
        }

        public void SetMapPos(Vector2 p) {
            mapPos = p;
        }

        #endregion

        #region Tick

        override public void UpdateBrain(float dt, Vector3 forward, out Input_t input) {
            input = new Input_t();
            for (int i = 0; i < (int)InputType.Count; i++) {
                if ((cur.buttons & (0x1 << i)) != 0) {
                    if ((last.buttons & (0x1 << i)) == 0) {
                        input.inputs[i] = InputState.JustPressed;
                    }
                    else {
                        input.inputs[i] = InputState.Pressed;
                    }
                }
                else {
                    if ((last.buttons & (0x1 << i)) != 0) {
                        input.inputs[i] = InputState.JustReleased;
                    }
                    else {
                        input.inputs[i] = InputState.Released;
                    }
                }
            }
            var right = Vector3.Cross(Vector3.up, forward);
            input.movement += forward * (float)cur.fwd / 127f;
            input.movement += right * (float)cur.right / 127f;

            input.yaw = Mathf.Atan2(input.movement.x, input.movement.z);
        }


        void UpdateStats(float dt) {
            //float time = dt / 60 / 24;
            //float sleep = 0;

            if (health <= 0) {
                Die();
            }

        }

        override public void LandOnGround() {
            // Land on ground
            var block = world.GetBlock(position);
            if (!WorldUtils.IsCapBlock(block)) {
                block = world.GetBlock(footPosition(position));
            }
            float d = -velocity.y / data.fallDamageVelocity * WorldUtils.GetFallDamage(block);
            if (d > 0) {
                damage(d);
                useStamina((float)d);
                stun((float)d);
            }

            OnLand?.Invoke(d);
        }

        override protected void Die() {
            health = maxHealth;
            Respawn();

        }


        #endregion

        #region inventory

        public void SetMoney(int m) {
            money = m;
            OnMoneyChange?.Invoke();
        }

        bool PickUp(Item item) {
            Money m;
            if ((m = item as Money) != null) {
                SetMoney(money + m.data.count * m.count);
                return true;
            }

            Pack p;
            if ((p = item as Pack) != null) {
                int packSlots = 0;
                // find the first available pack slot (there might be empty slots in a previous pack)
                for (int i = (int)InventorySlot.PACK; i < MaxInventorySize - (p.data.slots + 1); i++) {
                    var j = GetInventorySlot(i);
                    Pack p2;
                    if ((p2 = j as Pack) != null) {
                        packSlots = p2.data.slots;
                    }
                    else {
                        if (packSlots == 0) {
                            SetInventorySlot(i++, item);
                            foreach (var c in p.contained) {
                                SetInventorySlot(i++, c);
                            }
                            return true;
                        }
                        packSlots--;
                    }
                }
                return false;
            }

            if (item is Clothing && GetInventorySlot((int)InventorySlot.CLOTHING) == null) {
                SetInventorySlot((int)InventorySlot.CLOTHING, item);
                return true;
            }

            Weapon weapon;
            if ((weapon = item as Weapon) != null) {
                if (weapon.data.hand == WeaponData.Hand.BOTH && GetInventorySlot((int)InventorySlot.LEFT_HAND) == null && GetInventorySlot((int)InventorySlot.RIGHT_HAND) == null) {
                    SetInventorySlot((int)InventorySlot.RIGHT_HAND, item);
                    return true;
                }
                if (weapon.data.hand == WeaponData.Hand.LEFT || weapon.data.hand == WeaponData.Hand.RIGHT) {
                    int slotPreference1;
                    int slotPreference2;
                    if (weapon.data.hand == WeaponData.Hand.LEFT) {
                        slotPreference1 = (int)InventorySlot.LEFT_HAND;
                        slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                    }
                    else {
                        slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                        slotPreference2 = (int)InventorySlot.LEFT_HAND;
                    }
                    if (GetInventorySlot(slotPreference1) == null) {
                        SetInventorySlot(slotPreference1, item);
                        return true;
                    }
                    if (GetInventorySlot(slotPreference2) == null) {
                        SetInventorySlot(slotPreference2, item);
                        return true;
                    }
                }
            }

            Loot loot;
            if ((loot = item as Loot) != null) {
                if (loot.data.stackSize > 0) {
                    for (int i = 0; i < MaxInventorySize; i++) {
                        var item2 = GetInventorySlot(i) as Loot;
                        if (item2 != null && item2.data == item.data) {
                            int numToTransfer = Math.Min(loot.count, item2.data.stackSize - item2.count);
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
            if (FindEmptyPackSlots(1, ref slots)) {
                SetInventorySlot(slots[0], item);
                return true;
            }
            return false;
        }

        public override void SetInventorySlot(int index, Item item) {
            base.SetInventorySlot(index, item);

            int itemCount = 0;
            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) != null) {
                    itemCount++;
                }
            }
            weight = WeightClass.LIGHT;
            for (int i = 0; i < (int)WeightClass.COUNT; i++) {
                if (itemCount >= data.weightClassItemCount[i]) {
                    weight = (WeightClass)i;
                }
            }

        }

        public bool Use(Item item) {
            if (item == null) {
                return false;
            }

            if (item is Clothing
                || item is Pack
                || item is Weapon) {
                return Equip(item);
            }

            Loot loot;
            if ((loot = item as Loot) != null && loot.use(this)) {
                loot.count--;
                if (loot.count <= 0) {
                    RemoveFromInventory(item);
                }
                return true;
            }
            return false;
        }

        public bool Equip(Item item) {
            int[] emptyPackSlots = new int[2];

            bool inInventory = false;
            int curSlot = -1;
            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) == item) {
                    // unequip
                    if (i == (int)InventorySlot.CLOTHING || i == (int)InventorySlot.LEFT_HAND || i == (int)InventorySlot.RIGHT_HAND) {
                        if (FindEmptyPackSlots(1, ref emptyPackSlots)) {
                            SetInventorySlot(emptyPackSlots[0], GetInventorySlot(i));
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
                if (GetInventorySlot((int)InventorySlot.CLOTHING) == null) {
                    SetInventorySlot((int)InventorySlot.CLOTHING, item);
                    return true;
                }
                else {
                    if (inInventory || FindEmptyPackSlots(1, ref emptyPackSlots)) {
                        if (GetInventorySlot((int)InventorySlot.CLOTHING) != null) {
                            SetInventorySlot(emptyPackSlots[0], GetInventorySlot((int)InventorySlot.CLOTHING));
                        }
                        SetInventorySlot((int)InventorySlot.CLOTHING, item);
                        return true;
                    }
                }
                return false;
            }

            Weapon weapon;
            if ((weapon = item as Weapon) != null) {

                if (weapon.data.hand == WeaponData.Hand.BOTH) {
                    int slotsRequired = 0;
                    if (GetInventorySlot((int)InventorySlot.LEFT_HAND) != null) {
                        slotsRequired++;
                    }
                    if (GetInventorySlot((int)InventorySlot.RIGHT_HAND) != null) {
                        slotsRequired++;
                    }
                    int swapInventorySlot = emptyPackSlots[0];
                    if (inInventory) {
                        slotsRequired--;
                    }
                    if (slotsRequired <= 0 || FindEmptyPackSlots(slotsRequired, ref emptyPackSlots)) {
                        if (inInventory) {
                            emptyPackSlots[1] = emptyPackSlots[0];
                            emptyPackSlots[0] = swapInventorySlot;
                        }
                        int slotIndex = 0;
                        if (GetInventorySlot((int)InventorySlot.LEFT_HAND) != null) {
                            SetInventorySlot(emptyPackSlots[slotIndex++], GetInventorySlot((int)InventorySlot.LEFT_HAND));
                        }
                        if (GetInventorySlot((int)InventorySlot.RIGHT_HAND) != null) {
                            SetInventorySlot(emptyPackSlots[slotIndex++], GetInventorySlot((int)InventorySlot.RIGHT_HAND));
                        }
                        SetInventorySlot((int)InventorySlot.RIGHT_HAND, item);
                        return true;
                    }
                }
                else if (weapon.data.hand == WeaponData.Hand.LEFT || weapon.data.hand == WeaponData.Hand.RIGHT) {
                    int slotPreference1;
                    int slotPreference2;
                    if (weapon.data.hand == WeaponData.Hand.LEFT) {
                        slotPreference1 = (int)InventorySlot.LEFT_HAND;
                        slotPreference2 = (int)InventorySlot.RIGHT_HAND;
                    }
                    else {
                        slotPreference1 = (int)InventorySlot.RIGHT_HAND;
                        slotPreference2 = (int)InventorySlot.LEFT_HAND;
                    }
                    var slot1Weapon = GetInventorySlot(slotPreference1) as Weapon;
                    var slot2Weapon = GetInventorySlot(slotPreference2) as Weapon;
                    var slotBothWeapon = GetInventorySlot((int)InventorySlot.RIGHT_HAND) as Weapon;

                    // if the item in our left hand is two-handed, unequip and equip the desired item in preferred hand
                    if (slotBothWeapon != null && slotBothWeapon.data.hand == WeaponData.Hand.BOTH) {
                        if (inInventory || FindEmptyPackSlots(1, ref emptyPackSlots)) {
                            SetInventorySlot(emptyPackSlots[0], slotBothWeapon);
                            SetInventorySlot((int)InventorySlot.RIGHT_HAND, weapon);
                            return true;
                        }
                    }
                    else {
                        // if our preferred slot is empty, equip in that slot
                        if (slot1Weapon == null) {
                            SetInventorySlot(slotPreference1, weapon);
                            return true;
                        }
                        // if our preferred slot is full but the secondary slot is empty
                        else if (slot2Weapon == null) {
                            // but the secondary slot has an off-hand item, rearrange (eg. move shield to left hand and equip sword in right)
                            if (slot1Weapon.data.hand != weapon.data.hand) {
                                SetInventorySlot(slotPreference2, slot1Weapon);
                                SetInventorySlot(slotPreference1, weapon);
                            }
                            // item in our primary hand is of same type, equip in off-hand
                            else {
                                SetInventorySlot(slotPreference2, weapon);
                            }
                            return true;
                        }
                        // if both slots are full
                        if (inInventory || FindEmptyPackSlots(1, ref emptyPackSlots)) {
                            // if our preferred hand has an offhand item in it, unequip that and equip the item in preferred hand
                            if (slot1Weapon.data.hand != weapon.data.hand || slot2Weapon.data.hand == weapon.data.hand) {
                                SetInventorySlot(emptyPackSlots[0], slot1Weapon);
                                SetInventorySlot(slotPreference1, weapon);

                            }
                            // our preferred hand has an item of the same type, unequip the secondary item and equip in secondary slot
                            else {
                                SetInventorySlot(emptyPackSlots[0], slot2Weapon);
                                SetInventorySlot(slotPreference2, weapon);
                            }
                            return true;
                        }
                    }

                }
            }
            return false;
        }

        bool FindEmptyPackSlots(int count, ref int[] slots) {
            int packSlots = 0;
            int emptySlotIndex = 0;
            for (int i = (int)InventorySlot.PACK; i < MaxInventorySize; i++) {
                var item = GetInventorySlot(i);
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
                    packSlots = pack.data.slots;
                }
                else {
                    packSlots--;
                }
            }
            return false;
        }

        void RemoveFromInventory(Item item) {
            int slot = 0;
            int packSlots = 0;


            for (var i = 0; i < MaxInventorySize; i++) {
                var checkItem = GetInventorySlot(i) as Pack;
                if (checkItem != null) {
                    packSlots = checkItem.data.slots;
                }
                else {
                    packSlots--;
                }
                if (GetInventorySlot(i) == item) {
                    slot = i;
                    break;
                }
            }

            Pack pack;
            if ((pack = item as Pack) != null) {
                for (int i = 0; i < pack.data.slots; i++) {
                    var packItem = GetInventorySlot(i + slot + 1);
                    if (packItem != null) {
                        pack.contained.Add(packItem);
                    }
                }
            }


            if (slot == (int)InventorySlot.CLOTHING) {
                SetInventorySlot(slot, null);
            }
            else if (slot == (int)InventorySlot.LEFT_HAND) {
                SetInventorySlot(slot, null);
            }
            else if (slot == (int)InventorySlot.RIGHT_HAND) {
                SetInventorySlot(slot, null);
            }
            else if (pack != null) {
                for (int i = slot; i < MaxInventorySize - packSlots - 1; i++) {
                    SetInventorySlot(i, GetInventorySlot(i + packSlots + 1));
                }
            }
            else {
                for (int j = slot; j < slot + packSlots; j++) {
                    SetInventorySlot(j, GetInventorySlot(j + 1));
                }
                SetInventorySlot(slot + packSlots, null);
            }

        }

        public void Drop(Item item) {
            RemoveFromInventory(item);
			gameMode.SpawnWorldItem(item, handPosition(position));
        }

        #endregion


        #region World Interaction

        void Interact() {
            var t = GetInteractTarget();
            WorldItem worldItem;
            Critter critter;
            if ((worldItem = t as WorldItem) != null && !t.pendingKill) {
                if (PickUp(worldItem.item)) {
					t.Destroy();
                }
            }
            else if ((critter = t as Critter) != null) {
                if (mount == critter) {
                    SetMount(null);
                }
                else {
                    SetMount(critter);
                }
            }
            else {
                var block = world.GetBlock(footPosition(position));
                if (block == EVoxelBlockType.WATER) {
                    Loot waterItem = null;
                    var waterData = LootData.Get("Water");
                    foreach (var i in getInventory()) {
                        var other = i as Loot;
                        if (other != null && i.data == waterData && other.count < waterData.stackSize) {
                            waterItem = other;
                            break;
                        }
                    }
                    if (waterItem == null) {
                        int[] newSlot = new int[1];
                        if (FindEmptyPackSlots(1, ref newSlot)) {
							waterItem = waterData.CreateItem();
                            PickUp(waterItem);
                        }
                    }
                    if (waterItem != null) {
                        waterItem.count = waterData.stackSize;
                    }
                }
            }
        }

        bool IsValidAttackTarget(Pawn actor) {
            if ((actor == null) || actor.pendingKill)
                return false;
            var diff = actor.position - position;
            if (diff.magnitude > 20) {
                return false;
            }
            return true;
        }

        Pawn GetAttackTarget(float yaw) {

            float maxDist = 40;
            float maxTargetAngle = Mathf.PI;

            Pawn bestTarget = null;
            float bestTargetAngle = maxTargetAngle;
            foreach (var c in world.GetActorIterator<Critter>()) {
                var diff = c.position - position;
                float dist = diff.magnitude;
                if (dist < maxDist) {
                    float angleToEnemy = Mathf.Atan2(diff.x, diff.z);

                    float yawDiff = Math.Abs(Mathf.Repeat(angleToEnemy - yaw, Mathf.PI * 2));

                    // take the target's radius into account based on how far away they are
                    yawDiff = Math.Max(0.001f, yawDiff - Mathf.Atan2(c.data.collisionRadius, dist));

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


        public Entity GetInteractTarget() {
            if (mount != null) {
                return mount;
            }

            float closestDist = 2;
            Entity closestItem = null;
            foreach (var i in world.GetActorIterator<WorldItem>()) {
                float dist = (i.position - position).magnitude;
                if (dist < closestDist) {
                    closestDist = dist;
                    closestItem = i;
                }
            }
            foreach (var i in world.GetActorIterator<Critter>()) {
                if (i.team == team) {
                    float dist = (i.position - position).magnitude;
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestItem = i;
                    }
                }
            }
            return closestItem;

        }


        #endregion
    }
}