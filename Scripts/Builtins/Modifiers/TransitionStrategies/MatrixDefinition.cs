using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [CreateAssetMenu(fileName = "New Matrix Definition", menuName = "Dexterity/Matrix Definition", order = 100)]
    public class MatrixDefinition : ScriptableObject, IHasStates {
        [Serializable]
        public class Row
        {
            [HideInInspector]
            public string name;

            [State]
            public string[] from;
            [State]
            public string[] to;

            public float time = .2f;
            public AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [NonSerialized]
            public int[] fromIds, toIds;

            public override string ToString()
            {
                var fromStr = from.Length == 0 ? "(Any)" : string.Join(" / ", from);
                var toStr = to.Length == 0 ? "(Any)" : string.Join(" / ", to);
                return $"{fromStr} -> {toStr}";
            }

            public bool Matches(int fromId, int toId) {
                if (fromIds.Length != 0 && !fromIds.Contains(fromId)) return false;
                if (toIds.Length != 0 && !toIds.Contains(toId)) return false;
                
                return true;
            }
        }

        public StateFunction[] stateFunctionAssets;

        public List<Row> rows;
        public float defaultTime = .2f;
        public AnimationCurve defaultEasingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private Row defaultRow;


        IEnumerable<string> IHasStates.GetStateNames()
        => StateFunction.EnumerateStateNames(stateFunctionAssets);

        IEnumerable<string> IHasStates.GetFieldNames()
        => StateFunction.EnumerateFieldNames(stateFunctionAssets);

        public void Initialize()
        {
            defaultRow = new Row {
                fromIds = new int[0],
                toIds = new int[0],
                time = defaultTime,
                easingCurve = defaultEasingCurve,
            };

            foreach (var row in rows)
            {
                row.fromIds = row.from.Select(s => Core.instance.GetStateID(s)).ToArray();
                row.toIds = row.to.Select(s => Core.instance.GetStateID(s)).ToArray();
            }
        }

        private void OnValidate() {
            foreach (var row in rows)
            {
                row.name = $"{row}";
                // fix default values
                if (row.time == 0f)
                    row.time = defaultTime;
                if (row.easingCurve == null || row.easingCurve.keys.Length == 0)
                    row.easingCurve = defaultEasingCurve;
            }
        }

        public Row GetRow(int fromState, int toState)
        {
            foreach (var row in rows)
            {
                if (row.Matches(fromState, toState))
                    return row;
            }
            return defaultRow;
        }
    }
}