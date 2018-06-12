using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Utils {

        public static int positive_modulo (int i, int n) {
            return (i % n + n) % n;
        }
    }
}
