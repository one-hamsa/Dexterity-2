using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [Serializable]
    public class ConditionNodeData
    {
        public string nodeGUID;
        public string freeText;
        public string field;
        public bool entryPoint;
        public Vector2 position;
    }
}