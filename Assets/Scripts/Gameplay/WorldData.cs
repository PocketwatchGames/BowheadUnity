
using UnityEngine;

[CreateAssetMenuAttribute(menuName = "WorldData")]
public class WorldData : ScriptableObject {


	[System.Serializable]
	public class TerrainType {
		public string name;
		public float workModifier = 1;
		public float slideThreshold = 100;
		public float slideFriction = 0.5f;
		public float fallDamage = 1;
		public bool solid;
		public bool canClimbLight;
		public bool canClimbMedium;
		public bool canHang;
	}

	public TerrainType[] terrainTypes;

    public float windSpeedStormy;
    public float windSpeedWindy;
    public float windSpeedBreezy;
    public float maxWindSpeedVariance;
    public float minWindSpeedVariance;
    public float secondsPerHour = 60;

    public float SecondsPerDay { get { return 24 * secondsPerHour; } }
    public float DaysPerSecond { get { return 1.0f / SecondsPerDay; } }


}
