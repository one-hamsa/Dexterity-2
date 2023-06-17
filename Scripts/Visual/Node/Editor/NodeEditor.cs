using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(Node)), CanEditMultipleObjects]
    public class NodeEditor : BaseStateNodeEditor
    {
        static bool fieldValuesDebugOpen;
        static bool upstreamDebugOpen;
        private static HashSet<BaseField> upstreams = new();
        Node node;
        bool foldoutOpen;
        
        private HashSet<Node.OutputOverride> unusedOverrides = new();
        private bool gatesUpdated;
        private StepListView stepListView;

        protected void OnEnable()
        {
            foldoutOpen = true;
            fieldValuesDebugOpen = Application.isPlaying;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI_ChooseReference));

            var foldout = new Foldout { text = "Evaluation Steps" };
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.marginLeft = 10;
            foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
            
            stepListView = new StepListView(serializedObject, nameof(Node.customSteps));
            foreach (var node in targets.OfType<Node>())
            {
                node.onStateChanged += OnNodeStateChanged;
            }
            
            foldout.Add(stepListView);
            
            // disallow editing in play mode - this would require re-initialization of StepList
            foldout.SetEnabled(!Application.isPlaying);
            root.Add(foldout);
            
            // TODO 
            // EditorGUILayout.HelpBox($"State functions are added automatically from references. You can change the order and add manual ones.", MessageType.Info);
            
            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI));

            return root;
        }

        private void OnNodeStateChanged(int oldState, int newState)
        {
            stepListView.RefreshItems();
        }

        private void OnDestroy()
        {
            foreach (var node in targets.OfType<Node>())
            {
                node.onStateChanged -= OnNodeStateChanged;
            }
        }

        private void Legacy_OnInspectorGUI_ChooseReference() {
            node = target as Node;

            serializedObject.Update();
            
            // XXX call this from here because adding to customSteps from OnValidate() literally causes editor to crash
            //. when selecting multiple editor targets
            foreach (var node in targets.Cast<Node>())
                node.FixSteps();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.referenceAssets)));

            // runtime
            if (node.reference != null) {
                if (GUILayout.Button("Open Live Reference"))
                {
                    NodeReferenceEditorWindow.Open(node.reference); 
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        protected override void Legacy_OnInspectorGUI()
        {
            gatesUpdated = false;
            base.Legacy_OnInspectorGUI();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                node.NotifyGatesUpdate();
        }

        private void ShowChooseInitialState()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.initialState)));
        }

        protected override void ShowFieldOverrides()
        {
            // add nice name for all overrides
            foreach (var o in node.overrides)
            {
                if (string.IsNullOrEmpty(o.outputFieldName))
                    continue;
                
                var definition = DexteritySettingsProvider.GetFieldDefinitionByName(node, o.outputFieldName);
                o.name = $"{definition.name} = {Utils.ConvertFieldValueToText(o.value, definition)}";
            }

            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp, new GUIContent("Field Overrides"));
        }

        protected override void ShowFields()
        {
            if (targets.Length <= 1)
                gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(Node.customGates)),
                    node, ref foldoutOpen);
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.internalFieldDefinitions)));
        }

        protected override void ShowFieldValues()
        {
            if (!(fieldValuesDebugOpen = EditorGUILayout.Foldout(fieldValuesDebugOpen, "Field values", true, EditorStyles.foldoutHeader)))
                return;

            var origColor = GUI.color;

            var outputFields = node.outputFields;
            var overrides = node.cachedOverrides;
            unusedOverrides.Clear();
            foreach (var value in overrides.Values)
                unusedOverrides.Add(value);

            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";
            {
                EditorGUILayout.HelpBox($"{outputFields.Count} output fields{overridesStr}",
                    outputFields.Count == 0 ? MessageType.Warning : MessageType.Info);
            }

            foreach (var field in outputFields.Values.ToArray().OrderBy(f => f.GetValue() == Node.emptyFieldValue))
            {
                var value = field.GetValueWithoutOverride();
                string strValue = Utils.ConvertFieldValueToText(value, field.definition);

                if (value == Node.emptyFieldValue)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.TryGetValue(field.definitionId, out var valueOverride))
                {
                    GUI.color = Color.magenta;
                    strValue = $"{Utils.ConvertFieldValueToText(valueOverride.value, field.definition)} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(valueOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.definition.name);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(strValue);
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            foreach (var outputOverride in unusedOverrides)
            {
                GUI.color = Color.magenta;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(outputOverride.outputFieldName);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(outputOverride.value.ToString());
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            Repaint();
        }

        protected override void ShowAllTargetsDebug()
        {
            if (!Application.isPlaying)
                return;

            if (!(upstreamDebugOpen = EditorGUILayout.Foldout(upstreamDebugOpen, "Upstreams", true, EditorStyles.foldoutHeader)))
                return;

            foreach (var t in targets) {
                if (targets.Length > 1)
                    EditorGUILayout.LabelField(t.name, EditorStyles.whiteBoldLabel);

                foreach (var output in (t as Node).outputFields.Values)
                {
                    GUILayout.Label(output.definition.name, EditorStyles.boldLabel);

                    upstreams.Clear();
                    ShowUpstreams(output, t as Node);

                    GUILayout.Space(5);
                }

                GUILayout.Space(10);
            }
            
        }

        private static void ShowUpstreams(BaseField field, Node context)
        {
            upstreams.Add(field);

            if (Manager.instance.graph.edges.TryGetValue(field, out var upstreamFields)) {
                EditorGUI.indentLevel++;
                foreach (var upstreamField in upstreamFields) {
                    var upstreamFieldName = upstreamField.ToShortString();
                    var upstreamValue = upstreamField.GetValueAsString();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{upstreamFieldName} = {upstreamValue}");
                    GUILayout.FlexibleSpace();
                    if (upstreamField.context != context && GUILayout.Button(upstreamField.context.name)) {
                        Selection.activeObject = upstreamField.context;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (upstreams.Contains(upstreamField)) {
                        EditorGUILayout.HelpBox($"Cyclic dependency in {upstreamFieldName}", MessageType.Error);
                        continue;
                    }

                    ShowUpstreams(upstreamField, context);
                }
                EditorGUI.indentLevel--;
            }
        }

        protected override void ShowWarnings()
        {
            if (node.customSteps.Count == 0)
            {
                EditorGUILayout.HelpBox($"Node has no steps", MessageType.Error);
            }
            base.ShowWarnings();
        }
    }
}
