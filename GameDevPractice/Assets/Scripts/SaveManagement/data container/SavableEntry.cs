using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

namespace TH.SaveLoad
{
    [Serializable]
    public class SavableEntry
    {
        public string id;
        public string typeName;
        public string jsonPayload;

        [NonSerialized] public object RuntimeState;
    }
}