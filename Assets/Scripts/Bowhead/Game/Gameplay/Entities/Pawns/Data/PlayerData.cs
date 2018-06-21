using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    [CreateAssetMenuAttribute(menuName = "EntityData/Player")]
    public class PlayerData : PawnData<PlayerData> {

        [Header("Player")]
        public GameObject_WRef prefab;
        public float maxThirst;
        public float temperatureSleepMinimum;
        public float temperatureSleepMaximum;
        public float fallDamageVelocity;
        public float dropTime;
        public int[] weightClassItemCount = new int[(int)Player.WeightClass.COUNT];
    };

}