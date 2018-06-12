using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {


    public class World : MonoBehaviour {

        // Use this for initialization
        void Start() {

        }

        // Update is called once per frame
        void Update() {

        }




        const float SECONDS_PER_HOUR = 60;
        const float SECONDS_PER_DAY = 24 * SECONDS_PER_HOUR;
        const float DAYS_PER_SECOND = 1.0f / SECONDS_PER_DAY;




        struct Data_t {
            public float windSpeedStormy;
            public float windSpeedWindy;
            public float windSpeedBreezy;
            public float maxWindSpeedVariance;
            public float minWindSpeedVariance;
        }
        struct State_t {
            public float worldTime;
        }







        public UnityEngine.Random random = new UnityEngine.Random();

        float time;
        Data_t data;
        State_t state;
        public Player player;
        public List<Critter> critters;
        public Queue<Critter> critterPool;
        public List<Item> items = new List<Item>();
        public List<Item> allItems = new List<Item>();
        public CameraController camera;

        //FastNoiseSIMD* noise;
        //float* noiseFloats;

        float GetPerlinNormal(int x, int y, int z, float scale) {
            //noise->SetFrequency(scale);
            //noise->FillPerlinSet(noiseFloats, x, y, z, 1, 1, 1);
            //const float v = noiseFloats[0];
            //return (v + 1) / 2;
            return 0.5f;
        }

        float GetPerlinValue(int x, int y, int z, float scale) {
            //noise->SetFrequency(scale);
            //noise->FillPerlinSet(noiseFloats, x, y, z, 1, 1, 1);
            //const float v = noiseFloats[0];
            //return v;
            return 0;
        }

        Player.CData playerData;
        void init() {
            //int seed = 185;
            //	seed = 15485;
            //std::srand(seed);
            //noise = FastNoiseSIMD::NewFastNoiseSIMD(seed);
            //noiseFloats = noise->GetEmptySet(1, 1, 1);

            data.minWindSpeedVariance = 10;
            data.maxWindSpeedVariance = 100;
            data.windSpeedBreezy = 8;
            data.windSpeedWindy = 16;
            data.windSpeedStormy = 24;

            Item.initData();


            playerData = new Player.CData();
            playerData.maxHealth = 200;
            playerData.temperatureSleepMinimum = 50;
            playerData.temperatureSleepMaximum = 100;
            playerData.maxThirst = 100;
            playerData.dropTime = 0.5f;
            playerData.fallDamageVelocity = 20;
            playerData.collisionRadius = 0.5f;

            playerData.weightClassItemCount[(int)Player.WeightClass.LIGHT] = 0;
            playerData.weightClassItemCount[(int)Player.WeightClass.MEDIUM] = 4;
            playerData.weightClassItemCount[(int)Player.WeightClass.HEAVY] = 9;
            playerData.weightClassItemCount[(int)Player.WeightClass.ENCUMBERED] = 14;
            playerData.weightClassItemCount[(int)Player.WeightClass.IMMOBILE] = 19;

            playerData.maxStamina = 100;
            playerData.dodgeTime = 0.25f;
            playerData.recoveryTime = 2;
            playerData.staminaRechargeTime = 1;
            playerData.stunLimit = 1.0f;
            playerData.stunRecoveryTime = 1.0f;
            playerData.backStabAngle = 45f * Mathf.Deg2Rad;
            playerData.jumpSpeed = 12f;
            playerData.dodgeSpeed = 12f;
            playerData.jumpStaminaUse = 10;
            playerData.fallJumpTime = 0.25f;
            playerData.climbSpeed = 2f;
            playerData.swimJumpSpeed = 60f;
            playerData.swimSinkAcceleration = 100f;
            playerData.swimJumpBoostAcceleration = 12f;
            playerData.jumpBoostAcceleration = 24f;
            playerData.gravity = -50f;
            playerData.bouyancy = 20f;
            playerData.groundAcceleration = 2.0f;
            playerData.groundMaxSpeed = 12f;
            playerData.fallAcceleration = 30f;
            playerData.swimAcceleration = 15f;
            playerData.swimMaxSpeed = 10.0f;
            playerData.swimDragVertical = 5.0f;
            playerData.swimDragHorizontal = 0.25f;
            playerData.fallDragHorizontal = 0.05f;
            playerData.fallMaxHorizontalSpeed = 5.0f;
            playerData.climbWallRange = 0.25f;
            playerData.walkSpeed = 8f;
            playerData.crouchSpeed = 4f;
            playerData.walkStartTime = 0.15f;
            playerData.walkStopTime = 0.15f;
            playerData.groundWindDrag = 0.01f;
            playerData.slideThresholdSlope = 4;
            playerData.slideThresholdFlat = 15;
            playerData.climbGrabMinZVel = -20f;
            playerData.height = 2;


            // init the player state
            player = new Player(playerData);


            var bunny = Critter.createData<Critter, Critter.CData>("bunny");
            bunny.maxHealth = 30;
            bunny.maxStamina = 20.0f;
            bunny.fallDamageVelocity = 20;
            bunny.stunLimit = 1.0f;
            bunny.stunRecoveryTime = 3.0f;
            bunny.backStabAngle = 45f * Mathf.Deg2Rad;
            bunny.visionWeight = 2f;
            bunny.smellWeight = 1f;
            bunny.hearingWeight = 2f;
            bunny.waryCooldownTime = 3f;
            bunny.panicCooldownTime = 10f;
            bunny.waryIncreaseAtMaxAwareness = 4f;
            bunny.waryIncreaseAtMaxAwarenessWhilePanicked = 8f;
            bunny.updatePanicked = Critter.bounceAndFlee;

            bunny.collisionRadius = 0.5f;
            bunny.height = 1f;
            bunny.jumpSpeed = 8f;
            bunny.dodgeSpeed = 0;
            bunny.dodgeTime = 0;
            bunny.swimJumpSpeed = 60f;
            bunny.swimSinkAcceleration = 100f;
            bunny.gravity = -30f;
            bunny.bouyancy = 20f;
            bunny.groundAcceleration = 2.0f;
            bunny.groundMaxSpeed = 12f;
            bunny.fallAcceleration = 30f;
            bunny.swimAcceleration = 15f;
            bunny.swimMaxSpeed = 10.0f;
            bunny.swimDragVertical = 5.0f;
            bunny.swimDragHorizontal = 0.25f;
            bunny.fallDragHorizontal = 0.05f;
            bunny.fallMaxHorizontalSpeed = 5.0f;
            bunny.groundWindDrag = 0.01f;
            bunny.slideThresholdSlope = 4;
            bunny.slideThresholdFlat = 15;

            var wolf = Critter.createData<Critter, Critter.CData>("wolf");
            wolf.maxHealth = 100;
            wolf.maxStamina = 50.0f;
            wolf.fallDamageVelocity = 20;
            wolf.stunLimit = 1.0f;
            wolf.stunRecoveryTime = 3.0f;
            wolf.backStabAngle = 45f * Mathf.Deg2Rad;
            wolf.visionWeight = 2f;
            wolf.smellWeight = 1f;
            wolf.hearingWeight = 2f;
            wolf.waryCooldownTime = 3f;
            wolf.panicCooldownTime = 10f;
            wolf.waryIncreaseAtMaxAwareness = 4f;
            wolf.waryIncreaseAtMaxAwarenessWhilePanicked = 8f;
            wolf.updatePanicked = Critter.approachAndAttack;

            wolf.collisionRadius = 0.5f;
            wolf.height = 1.5f;
            wolf.jumpSpeed = 8f;
            wolf.dodgeSpeed = 0;
            wolf.dodgeTime = 0;
            wolf.swimJumpSpeed = 60f;
            wolf.swimSinkAcceleration = 100f;
            wolf.gravity = -30f;
            wolf.bouyancy = 20f;
            wolf.groundAcceleration = 2.0f;
            wolf.groundMaxSpeed = 12f;
            wolf.fallAcceleration = 30f;
            wolf.swimAcceleration = 15f;
            wolf.swimMaxSpeed = 10.0f;
            wolf.swimDragVertical = 5.0f;
            wolf.swimDragHorizontal = 0.25f;
            wolf.fallDragHorizontal = 0.05f;
            wolf.fallMaxHorizontalSpeed = 5.0f;
            wolf.groundWindDrag = 0.01f;
            wolf.slideThresholdSlope = 4;
            wolf.slideThresholdFlat = 15;

            for (int i = 0; i < 80; i++) {
                critterPool.Enqueue(new Critter(bunny));
            }
            for (int i = 0; i < 30; i++) {
                critterPool.Enqueue(new Critter(wolf));
            }

            for (int i = 0; i < 100; i++) {
                var item = new Item("Money");
                item.State.count = 100;
                item.State.position = new Vector3(UnityEngine.Random.Range(-500f,500f) + 0.5f, UnityEngine.Random.Range(-500f, 500f) + 0.5f, 500f);
                items.Add(item);
            }

            time = SECONDS_PER_HOUR * 8;
        }
        void shutdown() {
            //delete player;
            //delete playerData;

            //for (var c : critters) {
            //    delete c;
            //}
            //for (var c : critterPool) {
            //    delete c;
            //}
            //for (var i : Item::allItems) {
            //    delete i;
            //}


            //Entity::freeData();

            //items.clear();
            //critterPool.clear();
            //critters.clear();

            //noise->FreeNoiseSet(noiseFloats);
            //delete noise;
        }

        static Color[] dayColors = { new Color(0.7f, 0.7f, 1.0f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.90f, 0.90f, 0.95f, 1.0f), new Color(0.80f, 0.80f, 0.65f, 1.0f), new Color(0.45f, 0.45f, 0.70f, 1.0f), };
        static Color[] nightColors = { new Color(0.0f, 0.0f, 0.1f, 1.0f), new Color(0.10f, 0.10f, 0.15f, 1.0f), new Color(0.30f, 0.30f, 0.60f, 1.0f), new Color(0.30f, 0.20f, 0.20f, 1.0f), new Color(0.20f, 0.30f, 0.20f, 1.0f), };
        static Color[] sunsetColors = { new Color(0.6f, 0.5f, 0.5f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.80f, 0.65f, 0.60f, 1.0f), new Color(0.60f, 0.50f, 0.45f, 1.0f), new Color(0.40f, 0.40f, 0.50f, 1.0f), };
        static Color[] sunriseColors = { new Color(0.6f, 0.6f, 0.4f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.75f, 0.75f, 0.65f, 1.0f), new Color(0.60f, 0.55f, 0.50f, 1.0f), new Color(0.35f, 0.40f, 0.50f, 1.0f), };

        Color[] getSkyColor(float timeOfDay) {

            var skyColors = new Color[5];

            float sunriseTime = 5f;
            float sunriseLength = 3.0f;
            float sunsetTime = 18f;
            float sunsetLength = 3.0f;
            if (timeOfDay < sunriseTime || timeOfDay > sunsetTime + sunsetLength) {
                skyColors = nightColors;
            }
            else if (timeOfDay < sunriseTime + sunriseLength / 2) {
                float t = (timeOfDay - sunriseTime) / (sunriseLength / 2);
                for (int i = 0; i < 5; i++) {
                    skyColors[i] = nightColors[i] * (1f - t) + sunriseColors[i] * t;
                }
            }
            else if (timeOfDay < sunriseTime + sunriseLength) {
                float t = (timeOfDay - (sunriseTime + sunriseLength / 2)) / (sunriseLength / 2);
                for (int i = 0; i < 5; i++) {
                    skyColors[i] = sunriseColors[i] * (1f - t) + dayColors[i] * t;
                }
            }
            else if (timeOfDay < sunsetTime) {
                skyColors = dayColors;
            }
            else if (timeOfDay < sunsetTime + sunsetLength / 2) {
                float t = (timeOfDay - sunsetTime) / (sunsetLength / 2);
                for (int i = 0; i < 5; i++) {
                    skyColors[i] = dayColors[i] * (1f - t) + sunsetColors[i] * t;
                }
            }
            else {
                float t = (timeOfDay - (sunsetTime + sunsetLength / 2)) / (sunsetLength / 2);
                for (int i = 0; i < 5; i++) {
                    skyColors[i] = sunsetColors[i] * (1f - t) + nightColors[i] * t;
                }
            }

            return skyColors;
        }

        void spawnNewCritter() {
            if (critterPool.Count > 0) {
                Vector3 pos = player.State.position;
                pos.z = 500;
                pos.x += UnityEngine.Random.Range(-500f, 500f) + 0.5f;
                pos.y += UnityEngine.Random.Range(-500f, 500f) + 0.5f;
                if (getTopmostBlock(500, ref pos)) {
                    Critter c = critterPool.Dequeue();
                    critters.Add(c);
                    c.init();
                    c.State.position = pos;
                    c.State.team = 1;

                    if (c.Data == Critter.GetData("bunny")) {
                        var item = new Item("Raw Meat");
                        item.State.count = 1;
                        c.State.loot[0] = item;
                    }
                    else if (c.Data == Critter.GetData("wolf")) {
                        var weapon = new Item("Teeth");
                        c.State.inventory[0] = weapon;
                    }

                    c.spawn(pos);
                }

            }
        }

        void update(float dt, float cameraYaw) {

            if (!player.State.spawned) {
                var spawnPoint = new Vector3(0.5f, 0.5f, 500);
                if (getTopmostBlock(500, ref spawnPoint)) {
                    player.spawn(spawnPoint + new Vector3(0, 0, 1));
                }
            }
            else {
                player.update(dt, cameraYaw);

                spawnNewCritter();
                foreach (var c in critters) {
                    c.Update(dt);

                    var diff = c.State.position - player.State.position;
                    if (diff.magnitude > 500) {
                        c.removeFlag = true;
                    }
                }

                foreach (var c in critters) {
                    if (c.removeFlag) {
                        critterPool.Enqueue(c);
                    }
                }
                critters.RemoveAll(c => c.removeFlag);

            }

            updateCollision(dt);

            foreach (var i in items) {
                if (!i.State.spawned) {
                    var pos = i.State.position;
                    if (getTopmostBlock(500, ref pos)) {
                        pos.z++;
                        i.spawn(pos);
                    }
                }
                else {
                    i.update(dt);
                }
            }

            time += dt;
        }

        float getTimeOfDay() {
            return Mathf.Repeat(time, SECONDS_PER_DAY) / SECONDS_PER_DAY * 24;
        }


        void testCollision(float dt, Actor a1, Actor a2) {
            var diff = a2.State.position - a1.State.position;
            float dist = diff.magnitude;
            float minDist = a1.Data.collisionRadius + a2.Data.collisionRadius;
            if (dist < minDist) {
                float a2Speed = a2.State.velocity.magnitude;
                float a1Speed = a1.State.velocity.magnitude;
                float totalSpeed = a2Speed + a1Speed;
                if (totalSpeed == 0) {
                    totalSpeed = 0.1f;
                }
                float a2Move = a2Speed / totalSpeed;
                var dir = diff.normalized;
                if (a2Move > 0) {
                    if (!a2.Move(dir * a2Move * dist, dt)) {
                        a2Move = 0;
                    }
                }
                if (a2Move < 1.0f) {
                    a1.Move(dir * -(1.0f - a2Move) * dist, dt);
                }
            }
        }
        void updateCollision(float dt) {
            for (int i = 0; i < critters.Count; i++) {
                var c = critters[i];
                if (!c.State.spawned) {
                    continue;
                }

                for (int j = i; j < critters.Count; j++) {
                    var c2 = critters[j];
                    if (!c2.State.spawned) {
                        continue;
                    }
                    testCollision(dt, c, c2);
                }

                testCollision(dt, c, player);

            }
        }

        public EBlockType getBlock(Vector3 pos) {
            return getBlock(pos.x, pos.y, pos.z);
        }

        public EBlockType getBlock(float x, float y, float z) {
            if (y < 0) {
                return EBlockType.BLOCK_TYPE_DIRT;
            }
            else {
                return EBlockType.BLOCK_TYPE_AIR;
            }
            //EBlockType blockType = EBlockType.BLOCK_TYPE_AIR;
            //if (getVoxel(cg->vsv, Vector3Int((int)Math.Floor(x), (int)Math.Floor(y), (int)Math.Floor(z)), ref blockType)) {
            //    return (EBlockType)(blockType & BLOCK_TYPE_MASK);
            //}
            //return EBlockType.BLOCK_TYPE_AIR;
        }

        public bool getTopmostBlock(int checkDist, ref Vector3 from) {
            from.y = -1;
            return true;
            //int origZ = (int)from.y;
            //for (int zOffset = 0; zOffset < checkDist; zOffset++) {
            //    from->z() = (float)(origZ - zOffset);
            //    EBlockType blockType = EBlockType.BLOCK_TYPE_AIR;
            //    if (cgi.getVoxel(cg->vsv, WorldVoxelPos_t((int)std::floor(from->x()), (int)std::floor(from->y()), (int)std::floor(from->z())), &blockType)) {
            //        if (isSolidBlock((EBlockType)blockType))
            //            return true;
            //    }
            //}
            //return false;
        }


        public static bool isCapBlock(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_SNOW) return true;
            return false;
        }


        public static bool isSolidBlock(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_WATER
                || type == EBlockType.BLOCK_TYPE_AIR
                || type == EBlockType.BLOCK_TYPE_SNOW
                || type == EBlockType.BLOCK_TYPE_FLOWERS1
                || type == EBlockType.BLOCK_TYPE_FLOWERS2
                || type == EBlockType.BLOCK_TYPE_FLOWERS3
                || type == EBlockType.BLOCK_TYPE_FLOWERS4) {
                return false;
            }
            return true;
        }

        public static bool isClimbable(EBlockType type, bool skilledClimber) {
            if (type == EBlockType.BLOCK_TYPE_LEAVES || type == EBlockType.BLOCK_TYPE_NEEDLES || type == EBlockType.BLOCK_TYPE_WOOD) {
                return true;
            }
            if (skilledClimber) {
                if (type == EBlockType.BLOCK_TYPE_DIRT || type == EBlockType.BLOCK_TYPE_ROCK || type == EBlockType.BLOCK_TYPE_GRASS) {
                    return true;
                }
            }
            return false;
        }

        public static bool isHangable(EBlockType type, bool skilledClimber) {
            if (type == EBlockType.BLOCK_TYPE_DIRT || type == EBlockType.BLOCK_TYPE_ROCK || type == EBlockType.BLOCK_TYPE_GRASS) {
                return true;
            }
            return false;
        }


        public static float getFallDamage(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_SNOW)
                return 0.5f;
            else if (type == EBlockType.BLOCK_TYPE_SAND)
                return 0.75f;
            else if (type == EBlockType.BLOCK_TYPE_DIRT || type == EBlockType.BLOCK_TYPE_GRASS)
                return 0.9f;
            return 1.0f;
        }

        public static bool isTransparentBlock(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_AIR
                || type == EBlockType.BLOCK_TYPE_WATER
                || type == EBlockType.BLOCK_TYPE_NEEDLES
                || type == EBlockType.BLOCK_TYPE_FLOWERS1
                || type == EBlockType.BLOCK_TYPE_FLOWERS2
                || type == EBlockType.BLOCK_TYPE_FLOWERS3
                || type == EBlockType.BLOCK_TYPE_FLOWERS4
                || type == EBlockType.BLOCK_TYPE_LEAVES
                || type == EBlockType.BLOCK_TYPE_SNOW) {
                return true;
            }
            return false;
        }

        public static bool isDiggable(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_WATER) return false;
            return true;
        }


        public Vector3 getGroundNormal(Vector3 position) {
            Vector3 normal = Vector3.up;
            if (getGroundDiff(position, position + Vector3.right) > 0)
                normal.x--;
            if (getGroundDiff(position, position - Vector3.right) > 0)
                normal.x++;
            if (getGroundDiff(position, position + Vector3.forward) > 0)
                normal.y--;
            if (getGroundDiff(position, position - Vector3.forward) > 0)
                normal.y++;
            return normal.normalized;
        }
        public int getGroundDiff(Vector3 position1, Vector3 position2) {
            if (!isSolidBlock(getBlock(position2))) {
                if (isSolidBlock(getBlock(position2 - Vector3.up))) {
                    return -1;
                }
            }
            else if (isSolidBlock(getBlock(position2 + Vector3.up)) && !isSolidBlock(getBlock(position2 + 2 * Vector3.up))) {
                return 1;
            }
            return 0;
        }

        public float getRiver(int blockX, int blockY) {
            int offsetX = 0;
            int offsetY = 0;
            float powerScaleInverse = 0.001f;
            float power =
                0.4f * GetPerlinNormal((blockX + offsetX), (blockY + offsetY), 0, powerScaleInverse) +
                0.3f * GetPerlinNormal((blockX + offsetX + 25254), (blockY + offsetY + 65363), 0, powerScaleInverse * 0.5f) +
                0.3f * GetPerlinNormal((blockX + offsetX + 2254), (blockY + offsetY + 6563), 0, powerScaleInverse * 0.1f);
            return power;
        }
        public Vector3 getCurrent(int x, int y, int z) {
            float inverseRegionSize = 0.01f;
            var center = getRiver(x, z);
            Vector3 diff = Vector3.zero;
            diff += new Vector3(1, 0, 0) * Math.Abs(getRiver(x - 1, z) - center);
            diff += new Vector3(1, 0, 0) * Math.Abs(getRiver(x + 1, z) - center);
            diff += new Vector3(0, 0, 1) * Math.Abs(getRiver(x, z - 1) - center);
            diff += new Vector3(0, 0, 1) * Math.Abs(getRiver(x, z + 1) - center);
            float currentSpeed = diff.magnitude;
            diff /= currentSpeed;
            currentSpeed *= 1000 * GetPerlinValue(x, y, z, inverseRegionSize);
            return diff * Mathf.Clamp(currentSpeed, -8f, 8f);
        }

        public Vector3 getWind(Vector3 p) {
            return getWind((int)p.x, (int)p.y, (int)p.z);
        }
        public Vector3 getWind(int x, int y, int z) {
            float inverseRegionSize = 0.001f;
            float windAngle = GetPerlinValue(x + 6543, z + 6543, 0, inverseRegionSize) * Mathf.PI * 2;
            var wind = new Vector3(Mathf.Cos(windAngle), Mathf.Sin(windAngle), 0);
            float currentSpeed = data.minWindSpeedVariance + (data.maxWindSpeedVariance - data.minWindSpeedVariance) * Mathf.Pow(0.5f + 0.5f * GetPerlinValue((x + 88943), (y + 653), z, inverseRegionSize), 2f);

            float timeScale = 0.002f;
            float weatherSpeed = (GetPerlinNormal((int)((state.worldTime + 46332) * timeScale), 876, 0, 1f) +
                GetPerlinNormal(18740, (int)((state.worldTime + 7476) * timeScale), 0, 1f) +
                GetPerlinNormal((int)((state.worldTime + 1454) * timeScale), (int)((state.worldTime + 76746) * timeScale), 0, 1f)) / 3;
            weatherSpeed = Mathf.Pow(weatherSpeed, 2f);
            currentSpeed *= weatherSpeed;

            if (currentSpeed >= data.windSpeedStormy) {
                currentSpeed = data.windSpeedStormy;
            }
            else if (currentSpeed >= data.windSpeedWindy) {
                currentSpeed = data.windSpeedWindy;
            }
            else if (currentSpeed >= data.windSpeedBreezy) {
                currentSpeed = data.windSpeedBreezy;
            }
            else {
                currentSpeed = 0;
            }

            return wind * currentSpeed;
        }

        public float getClimbFriction(EBlockType block) {
            switch (block) {
                case EBlockType.BLOCK_TYPE_DIRT:
                    return 0.9f;
                case EBlockType.BLOCK_TYPE_GRASS:
                    return 0.8f;
                case EBlockType.BLOCK_TYPE_SAND:
                    return 0.5f;
            }
            return 1.0f;
        }

        public static void getSlideThreshold(EBlockType foot, EBlockType mid, EBlockType head, out float slideFriction, out float slideThreshold) {
            slideThreshold = 100;
            slideFriction = 0.5f;

            if (mid == EBlockType.BLOCK_TYPE_SNOW) {
                slideThreshold = 4;
                slideFriction = 0.25f;
            }
            else if (foot == EBlockType.BLOCK_TYPE_DIRT) {
                slideThreshold = 25;
            }
            else if (foot == EBlockType.BLOCK_TYPE_GRASS) {
                slideThreshold = 25;
            }
            else if (foot == EBlockType.BLOCK_TYPE_ROCK) {
                slideThreshold = 25;
            }
            else if (foot == EBlockType.BLOCK_TYPE_SAND) {
                slideThreshold = 4;
            }
        }

        public static float getWorkModifier(EBlockType foot, EBlockType mid, EBlockType head) {
            float workModifier = 0;

            if (mid == EBlockType.BLOCK_TYPE_SNOW) {
                //workModifier = 1f;
            }
            //else if (mid == EBlockType.LongGrass || foot == EBlockType.LongGrass || head == EBlockType.LongGrass)
            //{
            //	workModifier = 15f;
            //}
            else if (foot == EBlockType.BLOCK_TYPE_GRASS) {
                workModifier = 1f;
            }
            else if (foot == EBlockType.BLOCK_TYPE_SAND) {
                workModifier = 1f;
            }
            return workModifier;

        }


    }
}