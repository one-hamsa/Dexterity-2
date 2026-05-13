using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Rule-based combination. Each rule lists a set of required child state names
    /// (free text); if every required state is currently active among the
    /// aggregator's direct child providers, the rule fires and its output is the
    /// aggregator's result. Rules are evaluated in order; first match wins.
    ///
    /// If no rule matches, behavior is controlled by <see cref="onNoMatch"/>:
    /// pass through the first active child's state (default — sibling order is
    /// the priority), or contribute nothing.
    /// </summary>
    /// <example>
    /// Rules:
    ///   { required: ["Disabled", "Hover"], output: "Disabled Hover" }
    ///   { required: ["Disabled"],          output: "Disabled" }
    /// onNoMatch = PassthroughFirstActive
    /// Children (in sibling order): Disabled, Pressed, Hover
    ///   Disabled + Hover  → "Disabled Hover"  (rule 1)
    ///   Disabled          → "Disabled"        (rule 2)
    ///   Pressed + Hover   → "Pressed"         (passthrough; Pressed precedes Hover)
    ///   Hover             → "Hover"           (passthrough)
    /// </example>
    [AddComponentMenu("Dexterity/Hierarchy/All-Of Aggregator")]
    public class AllOfAggregator : HierarchyAggregator
    {
        public enum NoMatchPolicy
        {
            /// <summary>Contribute no state — the parent container falls through to its next sibling.</summary>
            Nothing,
            /// <summary>Emit the first active child's state (sibling order = priority).</summary>
            PassthroughFirstActive,
        }

        [Serializable]
        public class Rule
        {
            [Tooltip("All of these state names must be currently active among the child providers.")]
            public List<string> required = new();

            [Tooltip("State name to emit when this rule fires.")]
            public string output;
        }

        [SerializeField]
        private List<Rule> rules = new();

        [SerializeField, Tooltip("What to emit when no rule matches.")]
        private NoMatchPolicy onNoMatch = NoMatchPolicy.PassthroughFirstActive;

        private readonly HashSet<string> _activeStateNames = new();

        protected override bool TryAggregate(IReadOnlyList<IHierarchyStateProvider> orderedChildren, out string result)
        {
            _activeStateNames.Clear();
            for (var i = 0; i < orderedChildren.Count; i++)
            {
                if (orderedChildren[i].TryGetState(out var s) && !string.IsNullOrEmpty(s))
                    _activeStateNames.Add(s);
            }

            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule == null || rule.required == null || rule.required.Count == 0)
                    continue;

                var allPresent = true;
                for (var j = 0; j < rule.required.Count; j++)
                {
                    if (!_activeStateNames.Contains(rule.required[j]))
                    {
                        allPresent = false;
                        break;
                    }
                }

                if (allPresent && !string.IsNullOrEmpty(rule.output))
                {
                    result = rule.output;
                    return true;
                }
            }

            if (onNoMatch == NoMatchPolicy.PassthroughFirstActive)
            {
                for (var i = 0; i < orderedChildren.Count; i++)
                {
                    if (orderedChildren[i].TryGetState(out var s) && !string.IsNullOrEmpty(s))
                    {
                        result = s;
                        return true;
                    }
                }
            }

            result = null;
            return false;
        }

        public override IEnumerable<string> GetDeclaredStates()
        {
            for (var i = 0; i < rules.Count; i++)
            {
                var r = rules[i];
                if (r != null && !string.IsNullOrEmpty(r.output))
                    yield return r.output;
            }

            if (onNoMatch == NoMatchPolicy.PassthroughFirstActive)
            {
                var set = new HashSet<string>();
                AppendChildrenDeclaredStates(set);
                foreach (var s in set) yield return s;
            }
        }
    }
}
