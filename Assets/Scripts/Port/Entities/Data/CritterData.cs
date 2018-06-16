using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {


    [CreateAssetMenuAttribute(menuName = "EntityData/Critter")]
    public class CritterData : ActorData {
        [Header("Critter")]

        public float visionWeight;
        public float smellWeight;
        public float hearingWeight;
        public float waryCooldownTime;
        public float panicCooldownTime;
        public float waryIncreaseAtMaxAwareness;
        public float waryIncreaseAtMaxAwarenessWhilePanicked;
        public CritterBehaviorType panicBehavior;
    };

    

}