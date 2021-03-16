using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [Serializable]
    public class ConditionNodeData
    {
        public string NodeGUID;
        public string FreeText;
        public string Field;
        public bool EntryPoint;
        public Vector2 Position;
    }
}