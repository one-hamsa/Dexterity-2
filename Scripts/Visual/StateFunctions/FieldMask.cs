using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class FieldMask : List<(int field, int value)>
    {
        public FieldMask() : base() { }
        public FieldMask(int capacity) : base(capacity) { }

        public int GetValue(int field)
        {
            foreach (var pair in this)
                if (pair.field == field)
                    return pair.value;
            return Node.emptyFieldValue;
        }

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