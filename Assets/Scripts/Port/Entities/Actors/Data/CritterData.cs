using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Port {

    [Serializable]
    public sealed class Critter_WRef : WeakAssetRef<Critter> { }

    [CreateAssetMenuAttribute(menuName = "EntityData/Critter")]
    public class CritterData : ActorData {
        [Header("Critter")]

        public Critter_WRef prefab;
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