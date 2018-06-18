using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Port {
    public class Money : Item {

        #region State

        public int count;

        #endregion

        new public MoneyData Data { get { return GetData<MoneyData>(); } }
        public static MoneyData GetData(string dataName) { return DataManager.GetData<MoneyData>(dataName); }


    }
}
