using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [CreateAssetMenu(fileName = "New Matrix Definition", menuName = "Dexterity/Matrix Definition", order = 100)]
    public class MatrixDefinition : ScriptableObject, IProvidesStateFunction {
        [Serializable]
        public struct State {
            [State]
            public string name;

            public override string ToString() => name;
        }

        [Serializable]
        public class Row
        {
            [HideInInspector]
            public string name;
            
            public State[] from;
            public State[] to;

            public float time = .2f;
            public AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [NonSerialized]
            public int[] fromIds, toIds;
            
            [NonSerialized]
            public bool isDefault;

            public override string ToString()
            {
                var fromStr = from.Length == 0 ? "(Any)" : string.Join(" / ", from.Select(f => f.ToString()).ToArray());
                var toStr = to.Length == 0 ? "(Any)" : string.Join(" / ", to.Select(f => f.ToString()).ToArray());
                return $"{fromStr} -> {toStr}";
            }

            public bool Matches(int fromId, int toId) {
                if (fromIds.Length != 0 && !fromIds.Contains(fromId)) return false;
                if (toIds.Length != 0 && !toIds.Contains(toId)) return false;
                
                return true;
            }
        }

        public StateFunctionGraph stateFunctionAsset;

        public List<Row> rows;
        public float defaultTime = .2f;
        public AnimationCurve defaultEasingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private Row defaultRow;

        StateFunctionGraph IProvidesStateFunction.stateFunctionAsset => stateFunctionAsset;

        public void Initialize()
        {
            // add default row in the end
            defaultRow = new Row {
                fromIds = new int[0],
                toIds = new int[0],
                time = defaultTime,
                easingCurve = defaultEasingCurve,
                isDefault = true,
            };

            foreach (var row in rows)
            {
                row.fromIds = row.from.Select(s => Manager.instance.GetStateID(s.name)).ToArray();
                row.toIds = row.to.Select(s => Manager.instance.GetStateID(s.name)).ToArray();
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