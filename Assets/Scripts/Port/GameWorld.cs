using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {


    public class GameWorld : MonoBehaviour {

        #region State
        public float worldTime;
        public Vector3 spawnPoint;
        #endregion

        public delegate void OnSetPlayerFn(Player player);
        public event OnSetPlayerFn OnSetPlayer;






        public DataManager dataManager = new DataManager();

        public UnityEngine.Random random = new UnityEngine.Random();

        float time;
        public WorldData data;

        [HideInInspector]
        public Player player;
        public Queue<Critter> critterPool = new Queue<Critter>();
        [HideInInspector]
        public GameObject critters;
        [HideInInspector]
        public GameObject items;
        [HideInInspector]
        public List<WorldItem> allItems = new List<WorldItem>();
        public CameraController camera;

        //FastNoiseSIMD* noise;
        //float* noiseFloats;



        // Use this for initialization
        void Start() {
            Init();
        }

        // Update is called once per frame
        void Update() {
            Tick(Time.deltaTime, camera.GetYaw());
        }


        void Init() {
            //int seed = 185;
            //	seed = 15485;
            //std::srand(seed);
            //noise = FastNoiseSIMD::NewFastNoiseSIMD(seed);
            //noiseFloats = noise->GetEmptySet(1, 1, 1);

            items = new GameObject("items");
            items.transform.parent = transform;

            critters = new GameObject("critters");
            critters.transform.parent = transform;

            DataManager.initData();


            CreatePlayer();

            //for (int i = 0; i < 80; i++) {
            //    critterPool.Enqueue(CreateCritter("Bunny"));
            //}
            //for (int i = 0; i < 30; i++) {
            //    critterPool.Enqueue(CreateCritter("Wolf"));
            //}

            for (int i = 0; i < 100; i++) {
                var item = Item.Create<Money>(Money.GetData("Money"), this);
                item.count = 100;
                var worldItem = CreateWorldItem(item);
                worldItem.position = new Vector3(UnityEngine.Random.Range(-500f, 500f) + 0.5f, 500f, UnityEngine.Random.Range(-500f, 500f) + 0.5f);
            }

            time = data.secondsPerHour * 8;
        }

        void Tick(float dt, float cameraYaw) {

            if (!player.spawned) {
                if (GetFirstSolidBlockDown(1000, ref spawnPoint)) {
                    player.Spawn(spawnPoint + new Vector3(0, 1, 0));
                }
            }
            else {

                Actor.PlayerCmd_t cmd = new Actor.PlayerCmd_t();
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

                player.UpdatePlayerCmd(cmd);


                player.Tick(dt, cameraYaw);

                SpawnNewCritter();
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
                    if (GetFirstSolidBlockDown(1000, ref pos)) {
                        pos.y++;
                        i.Spawn(pos);
                    }
                }
                else {
                    i.Tick(dt);
                }
            }

            time += dt;
        }



        #region perlin utils
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


        #endregion

        #region entity creation
        public Player CreatePlayer() {
            // init the player state
            var playerPrefab = Player.GetData("player").prefab.Load();
            player = Instantiate(playerPrefab);
            player.transform.parent = transform;
            player.Init(player.Data, this);
            player.SetPosition(new Vector3(0, 500, 35));
            camera.SetTarget(player);

            OnSetPlayer(player);

            return player;
        }

        public WorldItem CreateWorldItem(Item item) {
            var p = WorldItem.GetData("worldItem");
            if (p == null) {
                return null;
            }
            var i = Instantiate(p.prefab.Load());
            i.transform.parent = items.transform;
            i.Create(item, this);
            return i;
        }
        public Item CreateItem(string item) {
            return Item.Create(Item.GetData(item), this);
        }

        public Critter CreateCritter(string prefabName) {
            var p = Critter.GetData(prefabName).prefab.Load();
            if (p == null) {
                return null;
            }
            var i = Instantiate(p);
            i.Create(i.Data, this);
            return i;
        }


        void SpawnNewCritter() {
            if (critterPool.Count > 0) {
                Vector3 pos = player.position;
                pos.y = 500;
                pos.x += UnityEngine.Random.Range(-200f, 200f) + 0.5f;
                pos.z += UnityEngine.Random.Range(-200f, 200f) + 0.5f;

                var bunnyData = Critter.GetData("bunny");
                var wolfData = Critter.GetData("wolf");
                if (GetFirstSolidBlockDown(1000, ref pos)) {
                    Critter c = critterPool.Dequeue();
                    c.transform.parent = critters.transform;
                    c.Init();
                    c.position = pos;
                    c.team = 1;

                    if (c.Data == bunnyData) {
                        var item = CreateItem("Raw Meat") as Loot;
                        item.count = 1;
                        c.loot[0] = item;
                    }
                    else if (c.Data == wolfData) {
                        var weapon = CreateItem("Teeth");
                        c.SetInventorySlot(0, weapon);
                    }

                    c.Spawn(pos);
                }

            }
        }
        #endregion


        #region getting blocks
        public EBlockType GetBlock(Vector3 pos) {
            return GetBlock(pos.x, pos.y, pos.z);
        }

        public EBlockType GetBlock(float x, float y, float z) {

            World.EVoxelBlockType blockType;
            if (Bowhead.GameManager.instance.serverWorld.worldStreaming.GetVoxelAt(new WorldVoxelPos_t((int)x, (int)y, (int)z), out blockType)) {
                return (EBlockType)blockType;
            }

            return EBlockType.BLOCK_TYPE_AIR;
        }

        public float GetFirstOpenBlockUp(int checkDist, Vector3 from) {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(from.x,500,from.z), Vector3.down, out hit, checkDist, Bowhead.Layers.ToLayerMask(Bowhead.ELayers.Terrain))) {
                return hit.point.y;
            }
            return from.y;
        }

        public bool GetFirstSolidBlockDown(int checkDist, ref Vector3 from) {
            RaycastHit hit;
            if (Physics.Raycast(from, Vector3.down, out hit, checkDist, Bowhead.Layers.ToLayerMask(Bowhead.ELayers.Terrain))) {
                from.y = hit.point.y + 1;
                return true;
            }
            return false;
        }
        #endregion

        #region time, weather and water currents

        float GetTimeOfDay() {
            return Mathf.Repeat(time, data.SecondsPerDay) / data.SecondsPerDay * 24;
        }



        static Color[] dayColors = { new Color(0.7f, 0.7f, 1.0f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.90f, 0.90f, 0.95f, 1.0f), new Color(0.80f, 0.80f, 0.65f, 1.0f), new Color(0.45f, 0.45f, 0.70f, 1.0f), };
        static Color[] nightColors = { new Color(0.0f, 0.0f, 0.1f, 1.0f), new Color(0.10f, 0.10f, 0.15f, 1.0f), new Color(0.30f, 0.30f, 0.60f, 1.0f), new Color(0.30f, 0.20f, 0.20f, 1.0f), new Color(0.20f, 0.30f, 0.20f, 1.0f), };
        static Color[] sunsetColors = { new Color(0.6f, 0.5f, 0.5f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.80f, 0.65f, 0.60f, 1.0f), new Color(0.60f, 0.50f, 0.45f, 1.0f), new Color(0.40f, 0.40f, 0.50f, 1.0f), };
        static Color[] sunriseColors = { new Color(0.6f, 0.6f, 0.4f, 1.0f), new Color(0.05f, 0.05f, 0.05f, 1.0f), new Color(0.75f, 0.75f, 0.65f, 1.0f), new Color(0.60f, 0.55f, 0.50f, 1.0f), new Color(0.35f, 0.40f, 0.50f, 1.0f), };

        Color[] GetSkyColor(float timeOfDay) {

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

        public float GetRiver(int blockX, int blockY) {
            int offsetX = 0;
            int offsetY = 0;
            float powerScaleInverse = 0.001f;
            float power =
                0.4f * GetPerlinNormal((blockX + offsetX), (blockY + offsetY), 0, powerScaleInverse) +
                0.3f * GetPerlinNormal((blockX + offsetX + 25254), (blockY + offsetY + 65363), 0, powerScaleInverse * 0.5f) +
                0.3f * GetPerlinNormal((blockX + offsetX + 2254), (blockY + offsetY + 6563), 0, powerScaleInverse * 0.1f);
            return power;
        }
        public Vector3 GetCurrent(int x, int y, int z) {
            float inverseRegionSize = 0.01f;
            var center = GetRiver(x, z);
            Vector3 diff = Vector3.zero;
            diff += new Vector3(1, 0, 0) * Math.Abs(GetRiver(x - 1, z) - center);
            diff += new Vector3(1, 0, 0) * Math.Abs(GetRiver(x + 1, z) - center);
            diff += new Vector3(0, 0, 1) * Math.Abs(GetRiver(x, z - 1) - center);
            diff += new Vector3(0, 0, 1) * Math.Abs(GetRiver(x, z + 1) - center);
            float currentSpeed = diff.magnitude;
            diff /= currentSpeed;
            currentSpeed *= 1000 * GetPerlinValue(x, y, z, inverseRegionSize);
            return diff * Mathf.Clamp(currentSpeed, -8f, 8f);
        }

        public Vector3 GetWind(Vector3 p) {
            return GetWind((int)p.x, (int)p.y, (int)p.z);
        }
        public Vector3 GetWind(int x, int y, int z) {
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
        #endregion

        #region static block properties


        public static bool IsCapBlock(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_SNOW) return true;
            return false;
        }


        public static bool IsSolidBlock(EBlockType type) {
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

        public static bool IsClimbable(EBlockType type, bool skilledClimber) {
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

        public static bool IsHangable(EBlockType type, bool skilledClimber) {
            if (type == EBlockType.BLOCK_TYPE_DIRT || type == EBlockType.BLOCK_TYPE_ROCK || type == EBlockType.BLOCK_TYPE_GRASS) {
                return true;
            }
            return false;
        }


        public static void GetSlideThreshold(EBlockType foot, EBlockType mid, EBlockType head, out float slideFriction, out float slideThreshold) {
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

        public static float GetWorkModifier(EBlockType foot, EBlockType mid, EBlockType head) {
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

        public static float GetFallDamage(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_SNOW)
                return 0.5f;
            else if (type == EBlockType.BLOCK_TYPE_SAND)
                return 0.75f;
            else if (type == EBlockType.BLOCK_TYPE_DIRT || type == EBlockType.BLOCK_TYPE_GRASS)
                return 0.9f;
            return 1.0f;
        }

        public static bool IsTransparentBlock(EBlockType type) {
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

        public static bool IsDiggable(EBlockType type) {
            if (type == EBlockType.BLOCK_TYPE_WATER) return false;
            return true;
        }


        #endregion

    }
}