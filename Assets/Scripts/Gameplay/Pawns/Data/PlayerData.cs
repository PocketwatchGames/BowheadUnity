using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/Player")]
    public class PlayerData : PawnData<PlayerData> {

        [Header("Player")]
        public GameObject_WRef prefab;
		public GameObject_WRef minimapMarker;
        public float temperatureSleepMinimum;
        public float temperatureSleepMaximum;
        public float fallDamageSpeed;
        public float dropTime;
		public int[] weightClassItemCount;

		public override Type spawnClass => typeof(Player);
    };

}