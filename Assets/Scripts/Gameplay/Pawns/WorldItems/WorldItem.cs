using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {
    public class WorldItem : Interactable<WorldItem, WorldItemData> {

        #region State

        public class MapReveal {
            public Vector2 position;
            public int radius;
        }

        public bool inMotion;
        public Vector3 position;
        public Vector3 velocity;
        public float yaw;
        public Item item;

        public MapReveal map;

		#endregion

		bool _hackDidFindGround;
		Client.UI.IMapMarker _marker;

		public override System.Type clientType => typeof(WorldItem);
		public override System.Type serverType => typeof(WorldItem);

		public virtual void Spawn(EntityData d, Vector3 pos, float yaw, Actor instigator, Actor owner, Team team) {
			base.ConstructEntity(d);
			position = pos;
			AttachExternalGameObject(GameObject.Instantiate(data.prefab.Load(), pos, Quaternion.identity, null));
            go.SetActive(false);
		}

        public override void Tick() {
			base.Tick();
			if (!hasAuthority) {
				return;
			}

			if (!_hackDidFindGround) {
				if (WorldUtils.GetFirstSolidBlockDown(1000, ref position)) {
					position += Vector3.up;
					_hackDidFindGround = true;
                    go.SetActive(true);
                }
                else {
					return;
				}
			}

			var dt = world.deltaTime;

            if (!inMotion) {
                if (velocity != Vector3.zero
                    || !WorldUtils.IsSolidBlock(world.GetBlock(position))) {
                    inMotion = true;
                }
            }

            if (inMotion) {
                var newVel = velocity;
                {
                    bool onGround = WorldUtils.IsSolidBlock(world.GetBlock(position)) && velocity.y <= 0;
                    if (!onGround) {
                        float gravity = -30f;
                        newVel.y += gravity * dt;
                    }
                    velocity = newVel;
                }

                var newPos = position;
                {
                    newPos.y += velocity.y * dt;
                    bool onGround = WorldUtils.IsSolidBlock(world.GetBlock(newPos)) && velocity.y <= 0;
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
                    if (!WorldUtils.IsSolidBlock(world.GetBlock(newPos + moveXZ)) && velocity.y != 0) {
                        position = newPos + moveXZ;
                    }
                    else {
                        var moveX = new Vector3(velocity.x * dt, 0, 0);
                        if (!WorldUtils.IsSolidBlock(world.GetBlock(newPos + moveX)) && velocity.y != 0) {
                            position = newPos + moveX;
                        }
                        else {
                            var moveZ = new Vector3(0, 0, velocity.z * dt);
                            if (!WorldUtils.IsSolidBlock(world.GetBlock(newPos + moveZ)) && velocity.y != 0) {
                                position = newPos + moveZ;
                            }
                        }
                    }
                }
            }

            float yaw = 0;
            go.transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));

			if ((_marker == null) && ((data.mapMarker != null) && (data.mapMarker.Load() != null))) {
				// this is just horrible, normally we'd have the gamestate on the client from the World object
				// but this is all server code... so fuck it dude let's go bowling.
				if (GameManager.instance.clientWorld.gameState != null) {
					_marker = AddGC(GameManager.instance.clientWorld.gameState.hud.CreateMapMarker(data.mapMarker.Load(), data.mapMarkerStyle));
					_marker.SetAsFirstSibling(); // always sort last.
				}
			}
			if (_marker != null) {
				_marker.worldPosition = new Vector2(position.x, position.z);
			}
		}

        public void Interact(Player player) {
            if (item != null) {
                if (!player.PickUp(item)) {
                    return;
                }
            }
            if (map != null) {
                player.Explore(map.position, map.radius);
            }
            Destroy();
        }

    }

}
