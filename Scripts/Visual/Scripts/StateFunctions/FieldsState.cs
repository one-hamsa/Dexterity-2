using GraphProcessor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class FieldsState : List<(int field, int value)>
    {
        public FieldsState() : base() { }
        public FieldsState(int capacity) : base(capacity) { }
    }
}