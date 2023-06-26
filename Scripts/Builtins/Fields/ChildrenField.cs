using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ChildrenField : BaseField
    {
        public enum TakeValueWhen {
            AnyEqualsTrue = 0,
            AnyEqualsFalse = 1,
            AllEqual = 2,
        }


        [Field]
        public string fieldName;
        public Transform parent;
        public bool recursive = true;
        public TakeValueWhen takeValueWhen = TakeValueWhen.AnyEqualsTrue;
        public bool negate;
        public bool updateChildrenReference;

        HashSet<Node> children, prevChildren = new();
        HashSet<string> childrenPath = new();
        private static Queue<Transform> workQueue = new();
        private static IEqualityComparer<HashSet<Node>> comparer = HashSet<Node>.CreateSetComparer();
        int fieldId;

        // only proxy when children are found
        public override bool proxy => children != null && children.Count > 0;

        public override int GetValue()
        {
            if (children == null)
                return 0;

            var value = GetValueBeforeNegation();
            return negate ? (value + 1) % 2 : value;
        }

        private int GetValueBeforeNegation() {
            switch (takeValueWhen)
            {
                case TakeValueWhen.AnyEqualsTrue:
                    foreach (var child in children)
                    {
                        if (child == null)
                            continue;
                        if (child.GetOutputField(fieldId).GetBooleanValue())
                            return 1;
                    }
                    return 0;
                case TakeValueWhen.AnyEqualsFalse:
                    foreach (var child in children)
                    {
                        if (child == null)
                            return 0;
                        if (!child.GetOutputField(fieldId).GetBooleanValue())
                            return 0;
                    }
                    return 1;
                case TakeValueWhen.AllEqual:
                    int? prevValue = null;
                    foreach (var child in children) {
                        var value = child == null ? 0 : child.GetOutputField(fieldId).GetValue();
                        if (prevValue.HasValue && prevValue.Value != value)
                            return 0;

                        prevValue = value;
                    }
                    return prevValue.HasValue ? prevValue.Value : 0;
            }
            return 0;
        }

        public override void RefreshReferences()
        {
            // find if any child is destroyed - if so, refresh all children
            if (children != null) {
                var anyIsNull = false;
                foreach (var child in children) {
                    if (child == null) {
                        anyIsNull = true;
                        break;
                    }
                }
                if (anyIsNull) {
                    children = null;
                }
            }

            prevChildren.Clear();
            if (children != null)
                foreach (var child in children) {
                    prevChildren.Add(child);
                }

            // check if children transforms stay the same
            if (children == null || updateChildrenReference) {
                var nodesIter = recursive 
                    ? GetNodesInChildrenRecursive() 
                    : GetNodesInImmediateChildren();

                // recalculate paths
                childrenPath.Clear();
                foreach (var child in nodesIter) {
                    childrenPath.Add(GetPath(child.transform));
                }

                if (children != null && childrenPath.Count == children.Count) {
                    var matched = true;
                    foreach (var child in children) {
                        if (child == null) {
                            matched = false;
                            break;
                        }
                        if (!childrenPath.Contains(GetPath(child.transform))) {
                            matched = false;
                            break;
                        }
                    }
                    if (matched)
                        // transforms stayed the same, no need to update
                        return;
                }

                if (children == null)
                    children = new HashSet<Node>();

                children.Clear();
                foreach (var child in nodesIter) {
                    children.Add(child);
                }
            }

            if (!comparer.Equals(children, prevChildren)) {
                ClearUpstreamFields();
                foreach (var child in children)
                    AddUpstreamField(child.GetOutputField(fieldId));

                // proxy might have changed - re-calculate node's outputs
                context.SetDirty();
            }
        }

        private IEnumerable<Node> GetNodesInChildrenRecursive()
        {
            // all children, but stop recursing when finding nodes
            workQueue.Clear();
            workQueue.Enqueue(parent);

            while (workQueue.Count > 0) {
                var transform = workQueue.Dequeue();
                var node = transform.GetComponent<Node>();
                if (transform != parent && node != null)
                    yield return node;
                else
                    foreach (Transform child in transform)
                        workQueue.Enqueue(child);
            }
        }

        private IEnumerable<Node> GetNodesInImmediateChildren()
        {
            for (var i = 0; i < parent.childCount; i++) {
                var child = parent.GetChild(i).GetComponent<Node>();
                if (child != null)
                    yield return child;
            }
        }

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            fieldId = Database.instance.GetFieldID(fieldName);
            if (parent == null)
                parent = context.transform;
            RefreshReferences();
        }

        public override void RebuildCache() {
            children = null;
        }

        private static string GetPath(Transform current) {
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}
