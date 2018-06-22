﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
    public class WorldItem : Interactable<WorldItem, WorldItemData> {

        #region State

        public bool inMotion;
        public Vector3 position;
        public Vector3 velocity;
        public float yaw;
        public Item item;

		#endregion

		bool _hackDidFindGround;

		public override System.Type clientType => typeof(WorldItem);
		public override System.Type serverType => typeof(WorldItem);
		
        public void ServerSpawn(Item i, Vector3 pos, WorldItemData data) {
			ServerSpawn(pos, data);
            item = i;
			position = pos;
			AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity, null));
        }

        public override void Tick() {
			base.Tick();
			if (!hasAuthority) {
				return;
			}

			if (!_hackDidFindGround) {
				if (world.GetFirstSolidBlockDown(1000, ref position)) {
					position += Vector3.up;
					_hackDidFindGround = true;
				} else {
					return;
				}
			}

			var dt = world.deltaTime;

            if (!inMotion) {
                if (velocity != Vector3.zero
                    || !World.IsSolidBlock(world.GetBlock(position))) {
                    inMotion = true;
                }
            }

            if (inMotion) {
                var newVel = velocity;
                {
                    bool onGround = World.IsSolidBlock(world.GetBlock(position)) && velocity.y <= 0;
                    if (!onGround) {
                        float gravity = -30f;
                        newVel.y += gravity * dt;
                    }
                    velocity = newVel;
                }

                var newPos = position;
                {
                    newPos.y += velocity.y * dt;
                    bool onGround = World.IsSolidBlock(world.GetBlock(newPos)) && velocity.y <= 0;
                    if (onGround) {
                        float bounceVel = -5f;
                        float bounceCoefficient = 0.5f;
                        float friction = 10f;
                        newPos.y = Mathf.Ceil(newPos.y);
                        if (velocity.y > bounceVel) {
                            newVel = newVel - Mathf.Max(1f, dt * friction) * velocity;
                        }
                        else {
                            newVel.y = -newVel.y * bounceCoefficient;
                        }
                        if (newVel.magnitude < 0.1f) {
                            newVel = Vector3.zero;
                        }
                    }
                    velocity = newVel;
                    var moveXZ = new Vector3(velocity.x * dt, 0, velocity.z * dt);
                    if (!World.IsSolidBlock(world.GetBlock(newPos + moveXZ)) && velocity.y != 0) {
                        position = newPos + moveXZ;
                    }
                    else {
                        var moveX = new Vector3(velocity.x * dt, 0, 0);
                        if (!World.IsSolidBlock(world.GetBlock(newPos + moveX)) && velocity.y != 0) {
                            position = newPos + moveX;
                        }
                        else {
                            var moveZ = new Vector3(0, 0, velocity.z * dt);
                            if (!World.IsSolidBlock(world.GetBlock(newPos + moveZ)) && velocity.y != 0) {
                                position = newPos + moveZ;
                            }
                        }
                    }
                }
            }

            float yaw = 0;
            go.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
        }



    }

}