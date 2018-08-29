using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

	public class Player : Pawn<Player, PlayerData> {

		public override Type serverType => typeof(Player);
		public override Type clientType => typeof(Player);

		public enum Stance {
			Combat,
			Explore
		}

		#region State
		[Header("Player")]
		public int playerIndex;
		public Vector3 spawnPoint;
        public Vector2 mapPos;
		public Pawn tradePartner;
		public bool useKeyboard;

		[Header("Inventory")]
        public int money;
        public WeightClass weight;
        public float dropTimer;
		public Weapon unarmedWeaponLeft;
		public Weapon unarmedWeaponRight;

		[Header("Player Stats")]
        public float temperature;
		public bool freezeMotion;
		#endregion

		public enum InventorySlot {
            ARMOR = 0,
            LEFT_HAND = 1,
            RIGHT_HAND = 2,
			LEFT_HAND_ALT = 3,
			RIGHT_HAND_ALT = 4,
			PACK = 5,
        }
        public enum WeightClass {
            LIGHT,
            MEDIUM,
            HEAVY,
            ENCUMBERED,
            IMMOBILE,
            COUNT
        }
		        
		World.Streaming.IVolume _worldStreaming;

        Pawn attackTargetPreview;
		private bool _addedToCamera;

		public event Action OnMoneyChange;
        public event Action OnWeightClassChange;
        public event Action<Vector2, int, bool> OnExplore;
		public event Action OnInventoryChange;
		public event Action OnSwapWeapon;
		public event Action OnInputMethodChange;
		public event Action<Pawn> OnMerchantActivated;

		public delegate void OnLandFn(float damage);
        public event OnLandFn OnLand;

		#region core functions

		public Player() {
			SetReplicates(true);
			var joystickNames = Input.GetJoystickNames();
			useKeyboard = joystickNames.Length == 0 || (joystickNames[0] == "");
		}

		public string GetButtonHint(string gpHint) {
			if (!useKeyboard) {
				return gpHint;
			}
			switch (gpHint) {
				case "A":
					return "SPACE";
				case "B":
					return "SHIFT";
				case "X":
					return "R";
				case "Y":
					return "I";
				case "LT":
					return "LMB";
				case "RT":
					return "RMB";
				case "LB":
					return "Q";
				case "RB":
					return "E";
			}
			return "";
		}
		private void RecordInputDevices() {

			int pi = playerIndex + 1;

			PlayerCmd_t cmd = new PlayerCmd_t();

			if (!freezeMotion) {

				if (pi == 1) {

					if (Input.GetButtonDown("Enter")) {
						useKeyboard = !useKeyboard;
						OnInputMethodChange?.Invoke();
					}

					Vector2 look;
					if (useKeyboard) {
						look = (Input.mousePosition - new Vector3(Screen.width / 2, Screen.height / 2)).normalized;
						if (look != Vector2.zero) {
							look.Normalize();
						}
						look.y = -look.y;
					} else {
						look = new Vector2(Input.GetAxis("LookHorizontal"), Input.GetAxis("LookVertical"));
						if (look.magnitude > 0.5f) {
							look.Normalize();
						} else {
							look = Vector2.zero;
						}
					}
					cmd.lookFwd = (sbyte)(-look.y * 127);
					cmd.lookRight = (sbyte)(look.x * 127);
				}

				Vector2 move = new Vector2(Input.GetAxis("MoveHorizontal" + pi), Input.GetAxis("MoveVertical" + pi)); ;
				cmd.fwd = (sbyte)(move.y * 127);
				cmd.right = (sbyte)(move.x * 127);

				if (Input.GetButton("A" + pi)) {
					cmd.buttons |= 1 << (int)InputType.Jump;
				}
				if (Input.GetButton("X" + pi)) {
					cmd.buttons |= 1 << (int)InputType.Swap;
				}
				if (Input.GetButton("Y" + pi)) {
					cmd.buttons |= 1 << (int)InputType.Interact;
				}
				if (Input.GetButton("AttackRight" + pi) || Input.GetAxis("RightTrigger" + pi) != 0) {
					cmd.buttons |= 1 << (int)InputType.AttackRight;
				}
				if (Input.GetButton("AttackLeft" + pi) || Input.GetAxis("LeftTrigger" + pi) != 0) {
					cmd.buttons |= 1 << (int)InputType.SprintDodge;
				}
				//if (Input.GetButton("ShoulderLeft" + pi)) {
				//	cmd.buttons |= 1 << (int)InputType.AttackRangedLeft;
				//}
				if (Input.GetButton("ShoulderRight")) {
					cmd.buttons |= 1 << (int)InputType.AttackLeft;
				}
				if (Input.GetButton("ShoulderLeft")) {
					cmd.buttons |= 1 << (int)InputType.Jump;
				}
				if (Input.GetButton("R3" + pi)) {
					cmd.buttons |= 1 << (int)InputType.Dodge;
				}
			}

			UpdatePlayerCmd(cmd);
        }




        public override void Tick() {

			if (!_addedToCamera && Client.Actors.ClientPlayerController.localPlayer != null && Client.Actors.ClientPlayerController.localPlayer.cameraController != null && rigidBody != null) {
				Client.Actors.ClientPlayerController.localPlayer.cameraController.AddTarget(this);
				_addedToCamera = true;
			}

			// kinda hacky here
			RecordInputDevices();

            base.Tick();
            if (!hasAuthority) {
                return;
            }

            // Hacky initial spawn
            if (!active) {
                if (WorldUtils.GetFirstSolidBlockDown(1000, ref spawnPoint)) {
                    spawnPoint += Vector3.up*2;
					velocity = Vector3.zero;
                    position = spawnPoint;
                    Respawn();
                    active = true;

                    var horseData = CritterData.Get("horse");
                    var c = gameMode.SpawnCritter(horseData, position + new Vector3(3,0,0), yaw, team);
                    c.SetActive(position + new Vector3(3, 0, 0));
				}
				else {
                    return;
                }
            }
			
			

			var head = go.GetChildComponent<MeshRenderer>("Head");
			if (head != null) {
				if (stunned) {
					head.material.color = Color.yellow;
				} else if (stunInvincibilityTimer > 0) {
					head.material.color = Color.gray;
				} else if (dodgeTimer > 0) {
					head.material.color = Color.black;
				} else if (skidding) {
					head.material.color = Color.cyan;
				} else {
					head.material.color = Color.white;
				}
			}
		}

        override public void PreSimulate(float dt) {

			if (unarmedWeaponLeft != null) {
				var left = GetInventorySlot((int)InventorySlot.LEFT_HAND);
				if (left == null) {
					SetInventorySlot((int)InventorySlot.LEFT_HAND, unarmedWeaponLeft);
					SetSilhouetteDirty();
				} else if (left != unarmedWeaponLeft && GetInventorySlot((int)InventorySlot.LEFT_HAND_ALT) == null) {
					SetInventorySlot((int)InventorySlot.LEFT_HAND_ALT, unarmedWeaponLeft);
					SetSilhouetteDirty();
				}
			}
			if (unarmedWeaponRight != null) {
				var right = GetInventorySlot((int)InventorySlot.RIGHT_HAND);
				if (right == null) {
					SetInventorySlot((int)InventorySlot.RIGHT_HAND, unarmedWeaponRight);
					SetSilhouetteDirty();
				} else if (right != unarmedWeaponRight && GetInventorySlot((int)InventorySlot.RIGHT_HAND_ALT) == null) {
					SetInventorySlot((int)InventorySlot.RIGHT_HAND_ALT, unarmedWeaponRight);
					SetSilhouetteDirty();
				}
			}

			canMove = weight < WeightClass.IMMOBILE;
            canAttack = weight < WeightClass.IMMOBILE;
            canRun = weight < WeightClass.ENCUMBERED;
			canJump = weight < WeightClass.ENCUMBERED;
			canSprint = weight < WeightClass.HEAVY;
			canSwim = true;
			canClimb = true;
            canTurn = true;
			canStrafe = mount == null;

			if (stunned) {
				canRun = false;
				canSprint = false;
				canJump = false;
				canClimb = false;
				canAttack = false;
			}

			if (activity == Activity.Swimming || activity == Activity.Climbing) {
                canAttack = false;
            }

			base.PreSimulate(dt);

		}


		public override Input_t GetInput(float dt) {
            Input_t input = base.GetInput(dt);

            Vector3 forward = Vector3.forward;
            if (Client.Actors.ClientPlayerController.localPlayer != null) {
                float cameraYaw = Client.Actors.ClientPlayerController.localPlayer.cameraController.GetYaw();
                forward = new Vector3(Mathf.Sin(cameraYaw), 0, Mathf.Cos(cameraYaw));
            }

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
			if (input.movement.magnitude > 1) {
				input.movement.Normalize();
			}

			Vector3 moveDir;
			if (input.movement != Vector3.zero) {
				moveDir = input.movement.normalized;
			} else {
				moveDir = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
			}

			if (canStrafe) {
				if (cur.lookFwd != 0 || cur.lookRight != 0) {
					input.look += forward * (float)cur.lookFwd / 127f;
					input.look += right * (float)cur.lookRight / 127f;
				} else {
					input.look = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
					//if (input.movement != Vector3.zero && Input.GetJoystickNames().Length > 0) {
					//    input.look = input.movement.normalized;
					//}
				}
			} else {
				if (mount != null) {
					input.look = moveDir;
				} else if (tradePartner != null) {
					var lookDir = (tradePartner.position - position);
					lookDir.y = 0;
					input.look = lookDir.normalized;
				}
				if (input.movement != Vector3.zero) {
					input.look = moveDir;
				}
			}

			return input;
        }

        override public void Simulate(float dt, Input_t input) {

            if (!alive) {
                return;
            }

			if (tradePartner != null) {
				var diff = tradePartner.position - position;
				if (diff.magnitude > data.tradePartnerCancelDistance) {
					SetTradePartner(null);
				}
			}

			if (input.JustPressed(InputType.Swap)) {
				if (tradePartner != null) {
					SetTradePartner(null);
				} else if (mount != null) {
					SetMount(null);
				} else {
					SwapWeapons();
				}				
			}


			if (input.JustPressed(InputType.Interact)) {
				Interact();
			}
			bool isCasting = false;
			Weapon itemLeft, itemRight;
			GetEquippedWeapons(out itemLeft, out itemRight);
			if (canAttack) {
				if (itemLeft != null) {
					if (itemLeft.CanCast()) {
						if (input.JustReleased(InputType.AttackLeft)) {
							if (itemLeft.chargeTime < itemLeft.data.jabChargeTime) {
								itemLeft.Attack(this, 0);
								isCasting = true;
							}
						}
					}
				}
				if (itemRight != null) {
					if (itemRight.CanCast()) {
						if (input.JustReleased(InputType.AttackRight)) {
							itemRight.Attack(this, 0);
							isCasting = true;
						}
					}
				}
				if (itemLeft != null) {
					if (itemLeft.CanCast() && input.IsPressed(InputType.AttackLeft)) {
						itemLeft.Charge(dt, 0);
						isCasting = true;
					}
					else {
						if (itemLeft.attackHand == 0) {
							itemLeft.chargeTime = 0;
						}
					}
                }
				if (itemRight != null) {
					if (itemRight.CanCast() && input.IsPressed(InputType.AttackRight)) {
						itemRight.Charge(dt, 1);
						isCasting = true;
					} else {
						if (itemRight.attackHand == 1) {
							itemRight.chargeTime = 0;
						}
					}
				}

			}

			attackTargetPreview = GetAttackTarget(yaw, 20, 360 * Mathf.Deg2Rad, null);

			if (isCasting) {
				SetTradePartner(null);
			}

			base.Simulate(dt, input);
            UpdateStats(dt);

        }

		public void SwapWeapons() {
			var l = GetInventorySlot((int)InventorySlot.LEFT_HAND);
			var r = GetInventorySlot((int)InventorySlot.RIGHT_HAND);
			SetInventorySlot((int)InventorySlot.LEFT_HAND, GetInventorySlot((int)InventorySlot.LEFT_HAND_ALT));
			SetInventorySlot((int)InventorySlot.RIGHT_HAND, GetInventorySlot((int)InventorySlot.RIGHT_HAND_ALT));
			SetInventorySlot((int)InventorySlot.LEFT_HAND_ALT, l);
			SetInventorySlot((int)InventorySlot.RIGHT_HAND_ALT, r);
			OnSwapWeapon?.Invoke();
		}

		public void GetEquippedWeapons(out Weapon itemLeft, out Weapon itemRight) {
			itemRight = GetInventorySlot((int)InventorySlot.RIGHT_HAND) as Weapon;
			if (itemRight?.data.hand == WeaponData.Hand.BOTH) {
				itemLeft = itemRight;
			} else {
				itemLeft = GetInventorySlot((int)InventorySlot.LEFT_HAND) as Weapon;
			}
		}

		////////////
		// Spawning
		////////////

		public override void Spawn(EntityData d, int index, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.Spawn(d, index, pos, yaw, instigator, owner, team);

			playerIndex = index;

			var gameObject = GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity, null);
            AttachExternalGameObject(gameObject);

			attackTargetPreview = null;

			// JOSEPH: this will be better once this is moved into new Actor framework, for now HACK
			_worldStreaming = GameManager.instance.serverWorld.worldStreaming.NewStreamingVolume(World.VOXEL_CHUNK_VIS_MAX_XZ, World.VOXEL_CHUNK_VIS_MAX_Y_UP, World.VOXEL_CHUNK_VIS_MAX_Y_DOWN);
			_worldStreaming.position = World.WorldToChunk(World.Vec3ToWorld(pos));

            spawnPosition = pos;

            SetSpawnPoint(pos);

			unarmedWeaponLeft = ItemData.Get(data.unarmedWeaponLeft.name).CreateItem() as Weapon;
			unarmedWeaponRight = ItemData.Get(data.unarmedWeaponRight.name).CreateItem() as Weapon;
			PickUp(unarmedWeaponLeft);
			PickUp(unarmedWeaponRight);
			PickUp(ItemData.Get("Rapier").CreateItem());
			PickUp(ItemData.Get("Buckler").CreateItem());
			SwapWeapons();
			PickUp(ItemData.Get("SpellMagicMissile").CreateItem());
			PickUp(ItemData.Get("SpellHeal").CreateItem());
			PickUp(ItemData.Get("TreeClimbingGear").CreateItem());
			SetMoney(50);

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

		public World.Streaming.IVolume worldStreaming => _worldStreaming;

		protected override void Dispose(bool disposing) {
			if (_worldStreaming != null) {
				_worldStreaming.Dispose();
				_worldStreaming = null;
			}
			base.Dispose(disposing);
		}

		public void Respawn() {
            position = spawnPoint;
            maxHealth = data.maxHealth;
            health = maxHealth;
        }

        public void SetSpawnPoint(Vector3 sp) {
            spawnPoint = sp;
        }

        public void SetMapPos(Vector2 p) {
            mapPos = p;
        }

		#endregion

		#region Tick


		void UpdateStats(float dt) {
            //float time = dt / 60 / 24;
            //float sleep = 0;


        }

		public override void SetPosition(Vector3 p) {
			base.SetPosition(p);
			UpdateStreaming(p);
		}

		protected override void MountMoved() {
			base.MountMoved();
			UpdateStreaming(mount.position);
		}

		override public void LandOnGround() {
            // Land on ground
            var block = world.GetBlock(position);
            if (!WorldUtils.IsCapBlock(block)) {
                block = world.GetBlock(footPosition(position));
            }

            float d = 0;
            float fallSpeed = -velocity.y;
            if (fallSpeed > data.fallDamageSpeed) {
                d = (fallSpeed - data.fallDamageSpeed) / data.fallDamageSpeed * gameMode.GetTerrainData(position).fallDamage * data.maxHealth;
                if (d > 0) {
                    Damage(d, PawnData.DamageType.Falling, true);
                    Stun((float)d);
                }
            }

			gameMode.CreateAudioEvent(this, fallSpeed*data.fallSpeedLoudness);

            OnLand?.Invoke(d);
        }

		void UpdateStreaming(Vector3 p) {
			XRayCamera.origin = p;
			_worldStreaming.position = World.WorldToChunk(World.Vec3ToWorld(p));
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

        public bool PickUp(Item item) {
            Money m;
            if ((m = item as Money) != null) {
                SetMoney(money + m.data.count * m.count);
                return true;
            }

			Armor armor;
			if ((armor = item as Armor) != null) {

				int slot = -1;
				slot = (int)InventorySlot.ARMOR;
				if (slot >= 0) {
					var i = GetInventorySlot(slot);
					if (i == null) {
						SetInventorySlot(slot, item);
						return true;
					}
				}
			}

			Weapon weapon;
			if ((weapon = item as Weapon) != null) {

				var curLeft = GetInventorySlot((int)InventorySlot.LEFT_HAND);
				var curRight = GetInventorySlot((int)InventorySlot.RIGHT_HAND);

				if (weapon.data.hand == WeaponData.Hand.BOTH && (curLeft == null || curLeft == unarmedWeaponLeft) && (curRight == null || curRight == unarmedWeaponRight)) {
					SetInventorySlot((int)InventorySlot.RIGHT_HAND, item);
					return true;
				}
				int slot = -1;
				if (weapon.data.hand == WeaponData.Hand.LEFT) {
					slot = (int)InventorySlot.LEFT_HAND;
				} else if (weapon.data.hand == WeaponData.Hand.RIGHT) {
					slot = (int)InventorySlot.RIGHT_HAND;
				}
				if (slot >= 0) {
					var i = GetInventorySlot(slot);
					if (i == null || i == unarmedWeaponRight || i == unarmedWeaponLeft) {
						SetInventorySlot(slot, item);
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

			OnInventoryChange?.Invoke();

		}

		public bool Use(Item item) {
            if (item == null) {
                return false;
            }

            Loot loot;
            if ((loot = item as Loot) != null && loot.use(this)) {
                loot.count--;
                if (loot.count <= 0) {
                    RemoveFromInventory(item);
                }
				OnInventoryChange?.Invoke();
				return true;
            }

			Weapon weapon;
			if ((weapon=item as Weapon) != null) {
				return Equip(weapon);
			}

            return false;
        }

        public bool Equip(Item item) {
            int[] emptyPackSlots = new int[2];

			////////////////
			// UNEQUIP
            bool inInventory = false;
            int curSlot = -1;
            for (int i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) == item) {
                    // unequip
                    if (i == (int)InventorySlot.ARMOR || i == (int)InventorySlot.LEFT_HAND || i == (int)InventorySlot.RIGHT_HAND) {
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

			//////////////////
			// EQUIP
			Armor armor;
			if ((armor = item as Armor) != null) {

				// clothing
				if (GetInventorySlot((int)InventorySlot.ARMOR) == null) {
					SetInventorySlot((int)InventorySlot.ARMOR, item);
					return true;
				} else {
					if (inInventory || FindEmptyPackSlots(1, ref emptyPackSlots)) {
						SetInventorySlot(emptyPackSlots[0], GetInventorySlot((int)InventorySlot.ARMOR));
						SetInventorySlot((int)InventorySlot.ARMOR, item);
						return true;
					}
				}
				return false;
			}


			Weapon weapon;
            if ((weapon = item as Weapon) != null) {

				// when equipping a two handed weapon, we need to make sure we have pack space to be able to unequip left and right handed weapons
				if (weapon.data.hand == WeaponData.Hand.BOTH) {
                    int slotsRequired = 0;
					var curLeft = GetInventorySlot((int)InventorySlot.LEFT_HAND);
					var curRight = GetInventorySlot((int)InventorySlot.RIGHT_HAND);

					if (curLeft != null && curLeft != unarmedWeaponLeft) {
                        slotsRequired++;
                    }
                    if (curRight != null && curRight != unarmedWeaponRight) {
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
                        if (curLeft != null && curLeft != unarmedWeaponLeft) {
							SetInventorySlot(emptyPackSlots[slotIndex++], curLeft);
                        }
                        if (curRight != null && curRight != unarmedWeaponRight) {
                            SetInventorySlot(emptyPackSlots[slotIndex++], curRight);
                        }
                        SetInventorySlot((int)InventorySlot.RIGHT_HAND, item);
                        return true;
                    }
                }
                else if (weapon.data.hand == WeaponData.Hand.LEFT || weapon.data.hand == WeaponData.Hand.RIGHT) {
                    int slot;
                    if (weapon.data.hand == WeaponData.Hand.LEFT) {
						slot = (int)InventorySlot.LEFT_HAND;
                    } else {
						slot = (int)InventorySlot.RIGHT_HAND;
                    }

					// if the item in our right hand is two-handed, unequip and equip the new item
					var slotBothWeapon = GetInventorySlot((int)InventorySlot.RIGHT_HAND) as Weapon;
					if (slotBothWeapon != null && slotBothWeapon.data.hand == WeaponData.Hand.BOTH) {
                        if (inInventory || FindEmptyPackSlots(1, ref emptyPackSlots)) {
                            SetInventorySlot(emptyPackSlots[0], slotBothWeapon);
                            SetInventorySlot(slot, weapon);
                            return true;
                        }
                    } else {

						// if our slot is full, unequip
						var slotWeapon = GetInventorySlot(slot) as Weapon;
						if (slotWeapon != null) {
							if (!FindEmptyPackSlots(1, ref emptyPackSlots)) {
								return false;
							}
							if (slotWeapon != unarmedWeaponLeft && slotWeapon != unarmedWeaponRight) {
								SetInventorySlot(emptyPackSlots[0], slotWeapon);
							}
						}

						// Equip
						SetInventorySlot(slot, weapon);

					}

				}
            }
            return false;
        }

        bool FindEmptyPackSlots(int count, ref int[] slots) {
            int emptySlotIndex = 0;
            for (int i = (int)InventorySlot.PACK; i < MaxInventorySize; i++) {
                var item = GetInventorySlot(i);
                if (item == null) {
                    slots[emptySlotIndex++] = i;
                    if (emptySlotIndex >= count) {
                        return true;
                    }
                }
            }
            return false;
        }

        public void RemoveFromInventory(Item item) {
            int slot = 0;
            int packSlots = 0;


            for (var i = 0; i < MaxInventorySize; i++) {
                if (GetInventorySlot(i) == item) {
                    slot = i;
                    break;
                }
            }

            if (slot == (int)InventorySlot.ARMOR) {
                SetInventorySlot(slot, null);
            }
            else if (slot == (int)InventorySlot.LEFT_HAND) {
                SetInventorySlot(slot, null);
            }
			else if (slot == (int)InventorySlot.RIGHT_HAND) {
				SetInventorySlot(slot, null);
			} else {
                for (int j = slot; j < slot + packSlots; j++) {
                    SetInventorySlot(j, GetInventorySlot(j + 1));
                }
                SetInventorySlot(slot + packSlots, null);
            }

            item?.OnSlotChange(-1, slot, this);


        }

        public void Drop(Item item) {
			RemoveFromInventory(item);
			var worldItem = WorldItemData.Get("chest").Spawn<WorldItem>(world, handPosition(), yaw, this, this, team);
			worldItem.item = item;
        }

		#endregion


		#region World Interaction

		protected override void SetActivity(Activity a) {
			base.SetActivity(a);
		}

		protected void SetTradePartner(Pawn t) {

			tradePartner = t;
			if (tradePartner != null) {
				OnMerchantActivated?.Invoke(tradePartner);
			}
		}


		public void Teleport() {
			SetMount(null);
			SetTradePartner(null);
			SetPosition(spawnPoint);
		}

		void Interact() {

            Entity target;
            string interaction;
			Vector3? targetPos;
            GetInteractTarget(out target, out targetPos, out interaction);


			WorldItem worldItem;
            Critter critter;
            if ((worldItem = target as WorldItem) != null && !target.pendingKill) {
                worldItem.Interact(this);
            }
            else if ((critter = target as Critter) != null) {
				if (critter.data.canMount) {
					if (mount != critter) {
						SetMount(critter);
					}
				}
				else {
					if (tradePartner == critter) {
						SetTradePartner(null);
					} else {
						SetTradePartner(critter);
					}
				}
			}
            else if (targetPos.HasValue) {
                var block = world.GetBlock(targetPos.Value);
     //           if (block == EVoxelBlockType.Water) {
     //               Loot waterItem = null;
     //               var waterData = LootData.Get("Water");
     //               foreach (var i in getInventory()) {
     //                   var other = i as Loot;
     //                   if (other != null && i.data == waterData && other.count < waterData.stackSize) {
     //                       waterItem = other;
     //                       break;
     //                   }
     //               }
     //               if (waterItem == null) {
     //                   int[] newSlot = new int[1];
     //                   if (FindEmptyPackSlots(1, ref newSlot)) {
					//		waterItem = waterData.CreateItem();
     //                       PickUp(waterItem);
     //                   }
     //               }
     //               if (waterItem != null) {
     //                   waterItem.count = waterData.stackSize;
     //               }
					//water = maxWater;
     //           }
            }
        }

        public void GetInteractTarget(out Entity target, out Vector3? targetPos, out string interactionType) {

            interactionType = null;

			if (tradePartner != null) {
				target = null;
				targetPos = null;
				return;
			}

			float closestDist = 2;
            Entity closestItem = null;
            foreach (var i in world.GetActorIterator<WorldItem>()) {
                float dist = (i.position - rigidBody.position).magnitude;
                if (dist < closestDist) {
                    closestDist = dist;
                    closestItem = i;
					if (i.item != null) {
						interactionType = "Get " + i.item.data.name;
					}
					else {
						interactionType = "Inspect";
					}
				}
            }
            foreach (var i in world.GetActorIterator<Critter>()) {
                if (i.team == team && i.active) {
					bool isInteractable = false;
					string iType = null;
					if (i.data.canMount) {
						if (mount != i) {
							isInteractable = true;
							iType = "Mount";
						}
					}
					else {
						isInteractable = true;
						if (tradePartner == i) {
							iType = "Cancel";
						}
						else {
							iType = "Greetings!";
						}
					}
					if (isInteractable) {
						float dist = (i.rigidBody.position - rigidBody.position).magnitude;
						if (dist < closestDist) {
							closestDist = dist;
							closestItem = i;
							interactionType = iType;
						}
					}
                }
            }

			target = closestItem;
			targetPos = null;
			if (closestItem == null) {
				var pos = footPosition() + Vector3.down;
				var block = world.GetBlock(pos);
				//if (block == EVoxelBlockType.Water) {
				//	targetPos = pos;
				//	interactionType = "Collect";
				//}
			}


        }


        public void Explore(Vector2 pos, int radius, bool showMap) {
            OnExplore?.Invoke(pos, radius, showMap);
			SetSpawnPoint(position);
        }

		public void FreezeMotion(bool f) {
			freezeMotion = f;
		}

        #endregion
    }
}