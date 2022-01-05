using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [CustomPropertyDrawer(typeof(MatrixStrategy.MatrixStrategyData))]
    public class MatrixStrategyDataDrawer : PropertyDrawer
    {
        const int rotationHeightOffset = 50; // about the amount we offset by 45 deg rotation

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Manager.instance == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Manager not found.");
                return;
            }

            var sf = Utils.GetStateFunctionFromObject(property.serializedObject.targetObject);
            if (sf == null)
            {
                EditorGUI.LabelField(position, label.text, $"Unsupported object type - cannot locate state function.");
                return;
            }

            var allStates = sf.GetStates().ToList();
            var rows = property.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyData.rows));
            var stateToProp = new Dictionary<(string from, string to), SerializedProperty>();

            // validate
            var foundTuples = new List<(string from, string to)>();
            var expectedTupleCount = allStates.Count * (allStates.Count - 1);
            var invalidIndices = Enumerable.Range(0, expectedTupleCount).ToList();
            for (var i = 0; i < expectedTupleCount; ++i)
            {
                if (rows.arraySize < i + 1)
                    rows.arraySize++;

                var row = rows.GetArrayElementAtIndex(i);
                var tuple = (row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.from)).stringValue,
                    row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.to)).stringValue);

                foundTuples.Add(tuple);
            }
            // find all invalid indices in array
            for (var i = 0; i < foundTuples.Count; ++i)
            {
                if (foundTuples.IndexOf(foundTuples[i]) != i)
                    // invalid - duplicate
                    continue;

                var tuple = foundTuples[i];
                if (allStates.Contains(tuple.from) && allStates.Contains(tuple.to) && tuple.from != tuple.to)
                    invalidIndices.Remove(i);
            }
            // fill missing or invalid indices with tuples
            foreach (var fromState in allStates)
            {
                foreach (var toState in allStates)
                {
                    if (fromState == toState)
                        continue;

                    if (!foundTuples.Contains((fromState, toState)))
                    {
                        var index = invalidIndices[0];
                        invalidIndices.RemoveAt(0);
                        var row = rows.GetArrayElementAtIndex(index);
                        row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.from)).stringValue = fromState;
                        row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.to)).stringValue = toState;
                    }
                }
            }
            // create dict
            for (var i = 0; i < expectedTupleCount; ++i)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var from = row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.from)).stringValue;
                var to = row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.to)).stringValue;
                stateToProp.Add((from, to), row);
            }

            var width = position.width / (allStates.Count + 1);
            var r = new Rect(position)
            {
                y = position.y - rotationHeightOffset
            };
            var j = 1;

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight
            };

            // draw horizontal labels
            foreach (var state in sf.GetStates())
            {
                r.xMin = width * j;
                r.xMax = width * ++j;

                EditorGUIUtility.RotateAroundPivot(45f, r.center);
                EditorGUI.LabelField(r, state, style);
                EditorGUIUtility.RotateAroundPivot(-45f, r.center);
            }

            // draw vertical labels
            j = 0;
            Rect innerR = default;
            foreach (var state in sf.GetStates())
            {
                innerR = new Rect(position.x, position.y + rotationHeightOffset + EditorGUIUtility.singleLineHeight * j++,
                    width, EditorGUIUtility.singleLineHeight);

                EditorGUI.LabelField(innerR, state);

                var fieldR = new Rect(innerR);
                foreach (var toState in sf.GetStates())
                {
                    fieldR.x += width;

                    if (state == toState)
                        continue;

                    var row = stateToProp[(state, toState)];
                    var timeProp = row.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.time));

                    EditorGUI.PropertyField(fieldR, timeProp, new GUIContent(""));
                }
            }

            var globalTimeR = new Rect(innerR.x, innerR.yMax, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            var globalTime = EditorGUI.FloatField(globalTimeR, new GUIContent("Set all to"), 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "set all values");
                foreach (var prop in stateToProp.Values)
                    prop.FindPropertyRelative(nameof(MatrixStrategy.MatrixStrategyRow.time)).floatValue = globalTime;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var sf = Utils.GetStateFunctionFromObject(property.serializedObject.targetObject);
            if (sf == null)
                return base.GetPropertyHeight(property, label);

            return rotationHeightOffset
                + EditorGUIUtility.singleLineHeight * (sf.GetStates().Count() + 1);
        }
    }
}
