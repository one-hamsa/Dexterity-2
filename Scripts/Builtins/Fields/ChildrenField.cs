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

        protected override void OnUpstreamsChanged(List<BaseField> upstreams = null)
        {
            base.OnUpstreamsChanged(upstreams);
            
            if (children == null)
                SetValue(0, upstreams);
            else
            {
                var v = GetValueBeforeNegation();
                SetValue(negate ? (v + 1) % 2 : v, upstreams);
            }
        }

        private int GetValueBeforeNegation() {
            switch (takeValueWhen)
            {
                case TakeValueWhen.AnyEqualsTrue:
                    foreach (var child in children)
                    {
                        if (!child.initialized)
                            continue;
                        if (child.GetOutputField(fieldId).GetBooleanValue())
                            return 1;
                    }
                    return 0;
                case TakeValueWhen.AnyEqualsFalse:
                    foreach (var child in children)
                    {
                        if (!child.initialized)
                            return 0;
                        if (!child.GetOutputField(fieldId).GetBooleanValue())
                            return 0;
                    }
                    return 1;
                case TakeValueWhen.AllEqual:
                    int? prevValue = null;
                    foreach (var child in children) {
                        var value = !child.initialized ? 0 : child.GetOutputField(fieldId).value;
                        if (prevValue.HasValue && prevValue.Value != value)
                            return 0;

                        prevValue = value;
                    }
                    return prevValue ?? 0;
            }
            return 0;
        }

        private void RefreshReferences()
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
            
            fieldId = Database.instance.GetFieldID(fieldName);
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
