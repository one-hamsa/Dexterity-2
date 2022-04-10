using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public class StepListView : ListView
    {
        const int kListItemHeight = 20;
        private readonly SerializedObject serializedObject;
        private readonly string propertyName;
        IStepList stepList;
        Dictionary<StateFunction.Step, int> stepToDepth;

        public StepListView(SerializedObject serializedObject, string propertyName) : base()
        {
            this.serializedObject = serializedObject;
            this.propertyName = propertyName;
            stepList = serializedObject.targetObject as IStepList;

            itemsSource = stepList.steps;
            fixedItemHeight = kListItemHeight;
            makeItem = CreateListItem;
            bindItem = BindListItem;

            selectionType = SelectionType.Multiple;
            reorderable = true;
            style.flexGrow = 1;
            showAddRemoveFooter = true;
            itemsAdded += HandleItemsAdded;
            itemIndexChanged += HandleItemIndexChanged;
            itemsRemoved += HandleItemsRemoved;

            RebuildDataAndCache(isMutated: false);
        }

        private VisualElement CreateListItem()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.minHeight = kListItemHeight;
            return container;
        }

        private void BindListItem(VisualElement e, int i)
        {
            e.Clear();

            var step = stepList.steps[i];

            e.style.opacity = GetStepEnabled(i) ? 1 : 0.35f;

            var depth = stepToDepth[step];
            if (depth > 0) {
                var indent = new VisualElement();
                indent.style.width = 0;
                indent.style.borderRightColor = new Color(1f, 1f, 0f, .2f);
                indent.style.borderRightWidth = 2;
                indent.style.borderTopColor = new Color(1f, 1f, 0f, .2f);
                indent.style.borderTopWidth = stepList.steps[i - 1].id == step.parent ? 2 : 0;
                indent.style.paddingRight = 5 * depth;
                indent.style.marginLeft = 5 * depth;
                e.Add(indent);
            }

            var rest = new VisualElement();
            rest.style.flexDirection = FlexDirection.Row;
            rest.style.flexGrow = 1;
            e.Add(rest);

            var typeBtn = new Button(() => {
                var toggleType = step.type == StateFunction.Step.Type.Condition 
                    ? StateFunction.Step.Type.Result 
                    : StateFunction.Step.Type.Condition;

                step.type = toggleType;
                RebuildDataAndCache();
            });
            typeBtn.RegisterCallback<MouseUpEvent>(e => {
                if (e.button != (int)MouseButton.RightMouse)
                    return;
                
                var toggleType = step.type == StateFunction.Step.Type.Reference 
                    ? StateFunction.Step.Type.Condition 
                    : StateFunction.Step.Type.Reference;

                step.type = toggleType;
                RebuildDataAndCache();
            });

            Color typeColor = default;
            string typeText = default;
            switch (step.type) {
                case StateFunction.Step.Type.Condition:
                    typeColor = new Color(1f, 1f, 0f, .1f);
                    typeText = "If";
                    break;
                case StateFunction.Step.Type.Result:
                    typeColor = new Color(1f, 0f, 1f, .1f);
                    typeText = "Go to";
                    break;
                case StateFunction.Step.Type.Reference:
                    typeColor = new Color(1f, 0f, 0f, .15f);
                    typeText = "Run";
                    break;
            }
            typeBtn.text = typeText;
            typeBtn.style.backgroundColor = typeColor;
            rest.Add(typeBtn);
            
            serializedObject.Update();
            var stepProp = GetPropertyForStep(step);
            switch (step.type)
            {
                case StateFunction.Step.Type.Condition:
                    var fieldNamePf = new PropertyField();
                    fieldNamePf.label = "";
                    fieldNamePf.BindProperty(stepProp.FindPropertyRelative(nameof(StateFunction.Step.condition_fieldName)));
                    rest.Add(fieldNamePf);

                    var signBtn = new Button(() => {
                        step.condition_negate = !step.condition_negate;
                        RebuildDataAndCache();
                    }){ text = !step.condition_negate ? "==" : "!=" };
                    signBtn.style.backgroundColor = !step.condition_negate ? new Color(0f, 1f, 0f, .1f) : new Color(1f, 0f, 0f, .1f);
                    rest.Add(signBtn);

                    var fieldValuePf = new PropertyField();
                    fieldValuePf.label = "";
                    fieldValuePf.BindProperty(stepProp.FindPropertyRelative(nameof(StateFunction.Step.condition_fieldValue)));
                    rest.Add(fieldValuePf);
                    break;

                case StateFunction.Step.Type.Result:
                    var stateNamePf = new PropertyField();
                    stateNamePf.label = "";
                    stateNamePf.BindProperty(stepProp.FindPropertyRelative(nameof(StateFunction.Step.result_stateName)));
                    stateNamePf.style.flexGrow = 1;
                    rest.Add(stateNamePf);
                    break;

                case StateFunction.Step.Type.Reference:
                    var refPf = new PropertyField();
                    refPf.label = "";
                    refPf.BindProperty(stepProp.FindPropertyRelative(nameof(StateFunction.Step.reference_stateFunction)));
                    refPf.style.flexGrow = 1;
                    rest.Add(refPf);
                    break;
            }

            var upHierarchyBtn = new Button(() => {
                if (step.parent != -1) {
                    step.parent = stepList.steps.First(s => s.id == step.parent).parent;
                }
                RebuildDataAndCache();
            }) { text = "◀" };
            upHierarchyBtn.style.width = 17;
            upHierarchyBtn.style.visibility = step.parent == -1 ? Visibility.Hidden : Visibility.Visible;
            rest.Add(upHierarchyBtn);
        }

        private bool GetStepEnabled(int index)
        {
            var step = stepList.steps[index];
            if (step.parent == -1)
                return true;

            // check if step has older siblings that are result steps
            var parentIndex = stepList.steps.FindIndex(s => s.id == step.parent);
            for (var i = index - 1; i >= parentIndex; --i) {
                var sibling = stepList.steps[i];
                if (sibling.type == StateFunction.Step.Type.Result && sibling.parent == step.parent)
                    return false;
            }
            
            return GetStepEnabled(parentIndex);
        }

        private SerializedProperty GetPropertyForStep(StateFunction.Step step)
        {
            return serializedObject.FindProperty(propertyName)
                .GetArrayElementAtIndex(stepList.steps.IndexOf(step));
        }

        private void RebuildDataAndCache(bool isMutated = true) {
            ReparentStepsIfNeeded();
            BuildStepToDepthCache();
            serializedObject.ApplyModifiedProperties();

            if (isMutated)
                EditorUtility.SetDirty(serializedObject.targetObject as UnityEngine.Object);

            RefreshItems();
        }

        private void BuildStepToDepthCache()
        {
            stepToDepth = new Dictionary<StateFunction.Step, int>();
            foreach (var s in stepList.EnumerateTreeStepsDFS())
            {
                stepToDepth[s.step] = s.depth;
            }
        }

        private void ReparentStepsIfNeeded() {
            var seenStepIds = new HashSet<int>();
            for (int i = 0; i < stepList.steps.Count; i++) {
                var step = stepList.steps[i];
                if (step.parent != -1 
                    && (// reparent if parent is not in hierarchy (not seen before me)
                        !seenStepIds.Contains(step.parent) 
                        // reparent if parent is not a condition
                        || stepList.steps.First(s => s != null && s.id == step.parent).type != StateFunction.Step.Type.Condition)) {
                    step.parent = FindParentID(i - 1);
                }
                seenStepIds.Add(step.id);
            }
        }

        private int FindParentID(int index) {
            var parent = -1;
            if (index != -1) {
                var parentCandidate = stepList.steps[index];
                if (parentCandidate.type == StateFunction.Step.Type.Condition) {
                    // when condition is chosen, the parent is the condition 
                    parent = parentCandidate.id;
                } else {
                    // when result is chosen, the parent is the result
                    parent = parentCandidate.parent;
                }
            }
            return parent;
        }

        private void HandleItemsAdded(IEnumerable<int> indices)
        {
            // the parent of those indices is determined by the selection
            var parentId = FindParentID(selectedIndex);
            var parentIndex = selectedIndex == -1
                // add to end
                ? stepList.steps.Count - 2
                // add after parent
                : stepList.steps.FindIndex(s => s != null && s.id == parentId);

            var parent = parentIndex > -1 ? stepList.steps[parentIndex] : null;

            // remove and re-insert the new steps
            var maxId = stepList.steps.Count > 0 ? stepList.steps.Max(s => s?.id ?? -1) : -1;
            var newIndex = Mathf.Max(parentIndex + 1, selectedIndex + 1);
            foreach (var i in indices.Reverse()) {
                stepList.steps.RemoveAt(i);
                stepList.steps.Insert(newIndex, 
                    new StateFunction.Step { 
                        id = ++maxId, 
                        parent = parentId, 
                        type = parent?.type ?? default,

                        condition_fieldName = parent?.condition_fieldName,
                        condition_fieldValue = parent?.condition_fieldValue ?? default,
                        condition_negate = parent?.condition_negate ?? false,

                        result_stateName = parent?.result_stateName,

                        reference_stateFunction = parent?.reference_stateFunction,
                    });
            }

            RebuildDataAndCache();
            schedule.Execute(() => SetSelection(newIndex));
        }

        private void HandleItemsRemoved(IEnumerable<int> indices)
        {
            RebuildDataAndCache();
        }

        private void HandleItemIndexChanged(int oldIndex, int newIndex)
        {
            // find new parent according to current index
            var parent = FindParentID(newIndex - 1);
            stepList.steps[newIndex].parent = parent;
            
            RebuildDataAndCache();
        }
    }
}
