using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {

    [Serializable]
    public sealed class Player_WRef : WeakAssetRef<Player> { }

    [CreateAssetMenuAttribute(menuName = "EntityData/Player")]
    public class PlayerData : ActorData {

        [Header("Player")]
        public Player_WRef prefab;
        public float maxThirst;
        public float temperatureSleepMinimum;
        public float temperatureSleepMaximum;
        public float fallDamageVelocity;
        public float dropTime;
        public int[] weightClassItemCount = new int[(int)Player.WeightClass.COUNT];
    };

}