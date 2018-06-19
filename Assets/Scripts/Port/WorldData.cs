
using UnityEngine;

[CreateAssetMenuAttribute(menuName = "WorldData")]
public class WorldData : ScriptableObject {
    public float windSpeedStormy;
    public float windSpeedWindy;
    public float windSpeedBreezy;
    public float maxWindSpeedVariance;
    public float minWindSpeedVariance;
    public float secondsPerHour = 60;

    public float SecondsPerDay { get { return 24 * secondsPerHour; } }
    public float DaysPerSecond { get { return 1.0f / SecondsPerDay; } }

}
