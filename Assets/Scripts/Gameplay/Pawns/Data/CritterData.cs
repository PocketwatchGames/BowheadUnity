using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Bowhead.Actors {

    [CreateAssetMenu(menuName = "EntityData/Critter")]
    public class CritterData : PawnData<CritterData> {
        [Header("Critter")]

        public GameObject_WRef prefab;
        public float visionWeight;
        public float smellWeight;
        public float hearingWeight;
        public float waryCooldownTime;
        public float panicCooldownTime;
        public float waryIncreaseAtMaxAwareness;
        public float waryIncreaseAtMaxAwarenessWhilePanicked;
        public ECritterBehaviorType panicBehavior;

		public override Type spawnClass => typeof(Critter);
    };
}