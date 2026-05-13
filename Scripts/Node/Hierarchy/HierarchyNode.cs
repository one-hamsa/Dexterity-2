using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// State node whose state is computed by walking a tree of MonoBehaviour
    /// providers in its sub-hierarchy. Order-dependent first-match at the root:
    /// the first active direct child (in transform order) wins.
    /// Pure string-in/string-out, so the chain is evaluable at edit time.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class HierarchyNode : BaseStateNode, IHierarchyContainer
    {
        private readonly HashSet<IHierarchyStateProvider> _registered = new();
        private readonly List<IHierarchyStateProvider> _scratch = new();
        private HashSet<string> _stateNames;

        #region IHierarchyContainer
        void IHierarchyContainer.RegisterProvider(IHierarchyStateProvider provider)
        {
            if (_registered.Add(provider))
            {
                provider.onStateMayHaveChanged += MarkStateDirty;
                MarkStateDirty();
            }
        }

        void IHierarchyContainer.UnregisterProvider(IHierarchyStateProvider provider)
        {
            if (_registered.Remove(provider))
            {
                provider.onStateMayHaveChanged -= MarkStateDirty;
                MarkStateDirty();
            }
        }
        #endregion

        private void MarkStateDirty() => stateDirty = true;

        public override HashSet<string> GetStateNames()
        {
            _stateNames ??= new HashSet<string>();
            _stateNames.Clear();
            _stateNames.Add(initialState);
            _stateNames.Add(StateFunction.kDefaultState);

            CollectDeclaredStatesRecursively(transform, _stateNames);

            return _stateNames;
        }

        private static void CollectDeclaredStatesRecursively(Transform root, HashSet<string> output)
        {
            var scratch = new List<IHierarchyStateProvider>();
            HierarchyUtils.CollectOrderedDirectProviders(root, scratch);
            foreach (var p in scratch)
            {
                if (p == null) continue;
                foreach (var s in p.GetDeclaredStates())
                    if (!string.IsNullOrEmpty(s))
                        output.Add(s);

                // recurse into aggregators so we collect their subtree's declared states too
                if (p is HierarchyAggregator agg)
                    CollectDeclaredStatesRecursively(agg.transform, output);
            }
        }

        public override HashSet<string> GetFieldNames() => IHasStates.emptySet;

        public override int GetNextStateWithoutOverride()
        {
            _scratch.Clear();
            HierarchyUtils.CollectOrderedDirectProviders(transform, _scratch);

            foreach (var p in _scratch)
            {
                if (p == null) continue;
                if (p.TryGetState(out var stateName) && !string.IsNullOrEmpty(stateName))
                    return Database.instance.GetStateID(stateName);
            }

            return initialStateId;
        }

        /// <summary>
        /// Edit-time evaluation. Returns the aggregated state string the node would
        /// produce right now — does not touch Database, Manager, or runtime fields.
        /// Returns null when no provider contributes.
        /// </summary>
        public string EvaluateTreeEditor()
        {
            _scratch.Clear();
            HierarchyUtils.CollectOrderedDirectProviders(transform, _scratch);
            foreach (var p in _scratch)
            {
                if (p == null) continue;
                if (p.TryGetState(out var stateName) && !string.IsNullOrEmpty(stateName))
                    return stateName;
            }
            return null;
        }
    }
}
