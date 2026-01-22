using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    [System.Serializable]
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
        private static Queue<FieldNode> workQueue;
        private static IEqualityComparer<HashSet<FieldNode>> comparer;
        
        int fieldId;
        
        private FieldNode _parentContext;

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
            Profiler.BeginSample("ChildrenField: RefreshReferences (Union with)");
            prevChildren.Clear();
            if (children != null)
                prevChildren.UnionWith(children);
            Profiler.EndSample();

            Profiler.BeginSample("ChildrenField: RefreshReferences (Nodes Iter)");
            using var _ = ListPool<FieldNode>.Get(out var nodesList); 
            // check if children transforms stay the same
            GetNodesInChildrenRecursive(nodesList);
            Profiler.EndSample();

            Profiler.BeginSample("ChildrenField: RefreshReferences (Fill children)");
            if (children == null)
                children = new HashSet<FieldNode>();
            else
                children.Clear();
                
            foreach (var child in nodesList) 
                children.Add(child);
            Profiler.EndSample();

            Profiler.BeginSample("ChildrenField: RefreshReferences (Upstream Fields)");
            if (!comparer.Equals(children, prevChildren)) {
                ClearUpstreamFields();
                foreach (var child in children)
                    AddUpstreamField(child.GetOutputField(fieldId));

                // proxy might have changed - re-calculate node's outputs
                context.SetDirty();
            }
            Profiler.EndSample();
        }

        private void GetNodesInChildrenRecursive(List<FieldNode> output)
        {
            workQueue.Clear();
            workQueue.Enqueue(context);

            while (workQueue.Count > 0)
            {
                var cur = workQueue.Dequeue();
                foreach (var childNode in cur.childrenNodes)
                {
                    if (childNode is FieldNode childFieldNode)
                    {
                        output.Add(childFieldNode);

                        if (!recurseWhenFindingNode)
                            continue;
                        
                        workQueue.Enqueue(childFieldNode);
                    }
                }
            }
            
            workQueue.Clear();
        }

        protected override void Initialize(FieldNode context)
        {
            prevChildren = new();
            childrenPath = new();
            workQueue ??= new();
            comparer ??= HashSet<FieldNode>.CreateSetComparer();
            
            fieldId = Database.instance.GetFieldID(fieldName);

            base.Initialize(context);
        }

        public override void OnNodeEnabled()
        {
            base.OnNodeEnabled();
            _parentContext = context;
            if (parent == null)
                parent = context.transform; // Only so we can see in inspector which parent was chosen
            else
                _parentContext = parent.GetComponentInParent<FieldNode>();
            
            if (_parentContext == null)
                Debug.LogError("ChildrenField: cannot locate the FieldNode associated with given context", context);
            else
            {
                _parentContext.onChildNodesChanged += RefreshReferences;
                _parentContext.onEnabled += RefreshReferences;
            }
            RefreshReferences();
        }

        public override void OnNodeDisabled()
        {
            base.OnNodeDisabled();
            if (_parentContext != null)
            {
                _parentContext.onChildNodesChanged -= RefreshReferences;
                _parentContext.onEnabled -= RefreshReferences;
                _parentContext = null;
            }
        }

        private static string GetPath(Transform current) {
            if (current.parent == null)
                return "/" + current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}
