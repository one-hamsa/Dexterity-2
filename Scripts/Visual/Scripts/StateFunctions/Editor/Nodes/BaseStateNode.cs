using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public abstract class BaseStateNode : UnityEditor.Experimental.GraphView.Node
    {
        public string GUID;
        public bool EntryPoint = false;
    }
}