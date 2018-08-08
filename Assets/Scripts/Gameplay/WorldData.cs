
using UnityEngine;

[CreateAssetMenu(menuName = "WorldData")]
public class WorldData : StaticAsset {


	[System.Serializable]
	public class TerrainType {
		public string name;
		public float speedModifier = 1;
		public float accelerationModifier = 1;
		public float slideThreshold = 100;
		public float slideFriction = 0.5f;
		public float fallDamage = 1;
		public bool solid;
		public bool canClimbLight;
		public bool canClimbMedium;
		public bool canHang;
		public float soundModifier;
	}

	public TerrainType[] terrainTypes;
	public WorldAtlasData atlasData;

	public bool player2;
	public float windSpeedStormy;
    public float windSpeedWindy;
    public float windSpeedBreezy;
    public float maxWindSpeedVariance;
    public float minWindSpeedVariance;
	public float smellDisperseAngle;
	public float smellDisperseAnglePower;
	public float secondsPerHour = 60;

    public float SecondsPerDay { get { return 24 * secondsPerHour; } }
    public float DaysPerSecond { get { return 1.0f / SecondsPerDay; } }


}
