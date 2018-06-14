using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {

    [CreateAssetMenuAttribute]
    public class PlayerData : ActorData {

        [Header("Player")]
        public float maxThirst;
        public float temperatureSleepMinimum;
        public float temperatureSleepMaximum;
        public float fallDamageVelocity;
        public float dropTime;
        public int[] weightClassItemCount = new int[(int)Player.WeightClass.COUNT];
    };

}