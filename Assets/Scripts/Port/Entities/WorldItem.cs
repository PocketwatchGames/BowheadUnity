using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class WorldItem : Entity {

        #region State

        public bool inMotion;
        public Vector3 position;
        public Vector3 velocity;
        public bool spawned;
        public float yaw;
        [SerializeField]
        public Item item;

        #endregion

        virtual public void init(Item i, World w) {
            world = w;
            world.allItems.Add(this);
            item = i;
        }


        // Use this for initialization
        void Start() {

        }


        // Update is called once per frame
        void Update() {
            transform.SetPositionAndRotation(position, Quaternion.AngleAxis(yaw, Vector3.up));
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
                        newPos.z = Mathf.Ceil(newPos.z);
                        if (velocity.z > bounceVel) {
                            newVel = newVel - Mathf.Max(1f, dt * friction) * velocity;
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
