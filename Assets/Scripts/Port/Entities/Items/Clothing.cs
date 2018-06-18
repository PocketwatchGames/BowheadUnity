using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Clothing : Item {

        #region State

        #endregion

        new public ClothingData Data { get { return GetData<ClothingData>(); } }
        public static ClothingData GetData(string dataName) { return DataManager.GetData<ClothingData>(dataName); }


    }
}
