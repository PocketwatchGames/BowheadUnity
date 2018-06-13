using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    [CreateAssetMenuAttribute]
    public class CritterData : ActorData {
        public delegate void updateFn(Critter c, float dt, ref Actor.Input_t input);
        public float fallDamageVelocity;

        public float visionWeight;
        public float smellWeight;
        public float hearingWeight;
        public float waryCooldownTime;
        public float panicCooldownTime;
        public float waryIncreaseAtMaxAwareness;
        public float waryIncreaseAtMaxAwarenessWhilePanicked;
        public updateFn updatePanicked;
    };
}


