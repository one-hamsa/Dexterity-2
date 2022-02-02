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

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            for (int i = 0; i < Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append($"{this[i].field}: {this[i].value}");
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}