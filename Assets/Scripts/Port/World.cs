using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {


    public class World : MonoBehaviour {

        #region State
        public float worldTime;
        #endregion


        // Use this for initialization
        void Start() {
            init();
        }

        // Update is called once per frame
        void Update() {
            update(Time.deltaTime, camera.yaw);
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




        public DataManager dataManager = new DataManager();

        public UnityEngine.Random random = new UnityEngine.Random();

        float time;
        Data_t data;
        public Player player;
        public Queue<Critter> critterPool = new Queue<Critter>();
        public GameObject critters;
        public GameObject items;
        public List<WorldItem> allItems = new List<WorldItem>();
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

        PlayerData playerData;
        void init() {
            //int seed = 185;
            //	seed = 15485;
            //std::srand(seed);
            //noise = FastNoiseSIMD::NewFastNoiseSIMD(seed);
            //noiseFloats = noise->GetEmptySet(1, 1, 1);

            items = new GameObject("items");
            items.transform.parent = transform;

            critters = new GameObject("critters");
            critters.transform.parent = transform;

            data.minWindSpeedVariance = 10;
            data.maxWindSpeedVariance = 100;
            data.windSpeedBreezy = 8;
            data.windSpeedWindy = 16;
            data.windSpeedStormy = 24;

            DataManager.initData();


            // init the player state
            var prefab = DataManager.GetPrefab<Player>("Player");
            player = Instantiate(prefab);
            player.transform.parent = transform;
            player.init(player.Data, this);

            camera.setTarget(player);


            for (int i = 0; i < 80; i++) {
                critterPool.Enqueue(CreateCritter("Bunny"));
            }
            for (int i = 0; i < 30; i++) {
                critterPool.Enqueue(CreateCritter("Wolf"));
            }

            for (int i = 0; i < 100; i++) {
                var item = CreateWorldItem(Item.Create(Money.GetData("Money"), this));
                item.position = new Vector3(UnityEngine.Random.Range(-500f, 500f) + 0.5f, 500f, UnityEngine.Random.Range(-500f, 500f) + 0.5f);
                (item.item as Money).count = 100;
            }

            time = SECONDS_PER_HOUR * 8;
        }
        public WorldItem CreateWorldItem(Item item) {
            var p = DataManager.GetPrefab<WorldItem>("Item");
            if (p == null) {
                return null;
            }
            var i = Instantiate<WorldItem>(p);
            i.transform.parent = items.transform;
            i.init(item, this);
            return i;
        }
        public Item CreateItem(string item) {
            return Item.Create(Item.GetData(item), this);
        }

        public Critter CreateCritter(string prefabName) {
            var p = DataManager.GetPrefab<Critter>(prefabName);
            if (p == null) {
                return null;
            }
            var i = Instantiate<Critter>(p);
            i.init(i.Data, this);
            return i;
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
                Vector3 pos = player.position;
                pos.y = 500;
                pos.x += UnityEngine.Random.Range(-500f, 500f) + 0.5f;
                pos.z += UnityEngine.Random.Range(-500f, 500f) + 0.5f;
                if (getTopmostBlock(500, ref pos)) {
                    Critter c = critterPool.Dequeue();
                    c.transform.parent = critters.transform;
                    c.init();
                    c.position = pos;
                    c.team = 1;

                    if (c.Data == Critter.GetData("bunny")) {
                        var item = CreateItem("Raw Meat") as Loot;
                        item.count = 1;
                        c.loot[0] = item;
                    }
                    else if (c.Data == Critter.GetData("wolf")) {
                        var weapon = CreateItem("Teeth");
                        c.inventory[0] = weapon;
                    }

                    c.spawn(pos);
                }

            }
        }

        void update(float dt, float cameraYaw) {

            if (!player.spawned) {
                var spawnPoint = new Vector3(0.5f, 500, 0.5f);
                if (getTopmostBlock(500, ref spawnPoint)) {
                    player.spawn(spawnPoint + new Vector3(0, 1, 0));
                }
            }
            else {

                Actor.PlayerCmd_t cmd = new Actor.PlayerCmd_t();
                Vector2 move = new Vector2(Input.GetAxis("MoveHorizontal"), Input.GetAxis("MoveVertical"));
                cmd.fwd = (sbyte)(move.y*127);
                cmd.right = (sbyte)(move.x*127);
                if (Input.GetButton("Jump")) {
                    cmd.buttons |= 1 << (int)InputType.JUMP;
                }
                if (Input.GetButton("AttackLeft")) {
                    cmd.buttons |= 1 << (int)InputType.ATTACK_LEFT;
                }
                if (Input.GetButton("AttackRight")) {
                    cmd.buttons |= 1 << (int)InputType.ATTACK_RIGHT;
                }
                if (Input.GetButton("Interact")) {
                    cmd.buttons |= 1 << (int)InputType.INTERACT;
                }
                if (Input.GetButton("SelectLeft")) {
                    cmd.buttons |= 1 << (int)InputType.SELECT_LEFT;
                }
                if (Input.GetButton("SelectRight")) {
                    cmd.buttons |= 1 << (int)InputType.SELECT_RIGHT;
                }
                if (Input.GetButton("Use")) {
                    cmd.buttons |= 1 << (int)InputType.USE;
                }
                if (Input.GetButton("Map")) {
                    cmd.buttons |= 1 << (int)InputType.MAP;
                }

                player.UpdatePlayerCmd(cmd);


                player.Tick(dt, cameraYaw);

                spawnNewCritter();
                var cs = critters.GetComponentsInAllChildren<Critter>();
                foreach (var c in cs) {
                    c.Tick(dt);

                    var diff = c.position - player.position;
                    if (diff.magnitude > 500) {
                        c.removeFlag = true;
                    }
                }

                foreach (var c in cs) {
                    if (c.removeFlag) {
                        critterPool.Enqueue(c);
                        c.transform.parent = null;
                    }
                }

            }

            //updateCollision(dt);

            foreach (var i in items.GetComponentsInAllChildren<WorldItem>()) {
                if (!i.spawned) {
                    var pos = i.position;
                    if (getTopmostBlock(500, ref pos)) {
                        pos.y++;
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
            var diff = a2.position - a1.position;
            float dist = diff.magnitude;
            float minDist = a1.Data.collisionRadius + a2.Data.collisionRadius;
            if (dist < minDist) {
                float a2Speed = a2.velocity.magnitude;
                float a1Speed = a1.velocity.magnitude;
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
            var cs = critters.GetComponentsInAllChildren<Critter>();
            for (int i = 0; i < cs.Length; i++) {
                var c = cs[i];
                if (!c.spawned) {
                    continue;
                }

                for (int j = i; j < cs.Length; j++) {
                    var c2 = cs[j];
                    if (!c2.spawned) {
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
                normal.z--;
            if (getGroundDiff(position, position - Vector3.forward) > 0)
                normal.z++;
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
            float weatherSpeed = (GetPerlinNormal((int)((worldTime + 46332) * timeScale), 876, 0, 1f) +
                GetPerlinNormal(18740, (int)((worldTime + 7476) * timeScale), 0, 1f) +
                GetPerlinNormal((int)((worldTime + 1454) * timeScale), (int)((worldTime + 76746) * timeScale), 0, 1f)) / 3;
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