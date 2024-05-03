using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
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
        public bool recurseWhenFindingNode;
        public TakeValueWhen takeValueWhen = TakeValueWhen.AnyEqualsTrue;
        public bool negate;

        HashSet<FieldNode> children, prevChildren;
        HashSet<string> childrenPath;
        private static Queue<Transform> workQueue;
        private static IEqualityComparer<HashSet<FieldNode>> comparer;
        
        int fieldId;

        // only proxy when children are found
        public override bool proxy => children != null && children.Count > 0;

        public override bool GetValue()
        {
            if (children == null)
                return false;

            var value = GetValueBeforeNegation();
            return negate ? !value : value;
        }

        private bool GetValueBeforeNegation() {
            switch (takeValueWhen)
            {
                case TakeValueWhen.AnyEqualsTrue:
                    foreach (var child in children)
                    {
                        if (child == null)
                            continue;
                        if (child.GetOutputField(fieldId).GetValue())
                            return true;
                    }
                    return false;
                case TakeValueWhen.AnyEqualsFalse:
                    foreach (var child in children)
                    {
                        if (child == null)
                            return false;
                        if (!child.GetOutputField(fieldId).GetValue())
                            return false;
                    }
                    return true;
                case TakeValueWhen.AllEqual:
                    bool? prevValue = null;
                    foreach (var child in children) {
                        var value = child != null && child.GetOutputField(fieldId).GetValue();
                        if (prevValue.HasValue && prevValue.Value != value)
                            return false;

                        prevValue = value;
                    }
                    return prevValue.HasValue && prevValue.Value;
            }
            return false;
        }

        public override void RefreshReferences()
        {
            prevChildren.Clear();
            if (children != null)
                prevChildren.UnionWith(children);

            // check if children transforms stay the same
            var nodesIter = recursive 
                ? GetNodesInChildrenRecursive() 
                : GetNodesInImmediateChildren();

            if (children == null)
                children = new HashSet<FieldNode>();
            else
                children.Clear();
                
            foreach (var child in nodesIter) 
                children.Add(child);

            if (!comparer.Equals(children, prevChildren)) {
                ClearUpstreamFields();
                foreach (var child in children)
                    AddUpstreamField(child.GetOutputField(fieldId));

                // proxy might have changed - re-calculate node's outputs
                context.SetDirty();
            }
        }

        private IEnumerable<FieldNode> GetNodesInChildrenRecursive()
        {
            // all children, but stop recursing when finding nodes
            workQueue.Clear();
            workQueue.Enqueue(parent);

            while (workQueue.Count > 0) {
                var transform = workQueue.Dequeue();
                var node = transform.GetComponent<FieldNode>();
                if (transform != parent && node != null)
                {
                    yield return node;
                    
                    if (!recurseWhenFindingNode)
                        continue;
                }

                foreach (Transform child in transform)
                    workQueue.Enqueue(child);
            }
        }

        private IEnumerable<FieldNode> GetNodesInImmediateChildren()
        {
            for (var i = 0; i < parent.childCount; i++) {
                var child = parent.GetChild(i).GetComponent<FieldNode>();
                if (child != null)
                    yield return child;
            }
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            prevChildren = new();
            childrenPath = new();
            workQueue ??= new();
            comparer = HashSet<FieldNode>.CreateSetComparer();
            
            fieldId = Database.instance.GetStateID(fieldName);
            var activeContext = context;
            if (parent == null)
                parent = context.transform;
            else
                activeContext = parent.GetComponentInParent<FieldNode>();
            
            if (activeContext == null)
                Debug.LogError("ChildrenField: cannot locate the FieldNode associated with given context", context);
            else
            {
                activeContext.onChildTransformChanged += RefreshReferences;
                activeContext.onEnabled += RefreshReferences;
            }
            RefreshReferences();
            
        }
        
        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);

            var activeContext = context;
            if (parent != null)
                activeContext = parent.GetComponentInParent<FieldNode>();

            if (activeContext != null)
            {
                activeContext.onChildTransformChanged -= RefreshReferences;
                activeContext.onEnabled -= RefreshReferences;
            }
        }

        private static string GetPath(Transform current) {
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}
