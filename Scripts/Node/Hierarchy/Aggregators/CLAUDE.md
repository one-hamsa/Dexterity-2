<!-- Last updated: 2026-05-13 -->

# HierarchyAggregator subclasses

Aggregators are branch nodes in a HierarchyNode tree. They implement `IHierarchyStateProvider` (so they look like a single provider to their parent container) **and** `IHierarchyContainer` (so their child providers self-register with them instead of with the root node).

To add a new aggregator: subclass `HierarchyAggregator`, override:

- `bool TryAggregate(IReadOnlyList<IHierarchyStateProvider> orderedChildren, out string result)` — strategy. Children are passed in transform-DFS order, with nested containers represented by themselves (their own subtrees are not flattened in).
- `IEnumerable<string> GetDeclaredStates()` — every state name this aggregator can emit. Used by `HierarchyNode.GetStateNames()` so modifiers know about them.

The base class handles registration with the parent container, child subscription (`onStateMayHaveChanged` propagation), and edit-time `OnValidate` change-firing.

## Built-ins

### `FirstMatchAggregator`

Pass-through wrapper. First active child in transform order wins; the child's state name is the aggregator's output. Same semantics as the root `HierarchyNode`'s built-in behavior. Useful for **grouping** providers without changing semantics (e.g. for prefab organization) or for **nested first-match priority** within a subtree.

### `AllOfAggregator`

Rule-based combiner. Each rule lists a set of required child state names; if every required state is currently active among the child providers, the rule fires and its output is the aggregator's result. Rules are evaluated in order; first match wins.

**`onNoMatch` policy** controls what happens when no rule fires:
- `PassthroughFirstActive` (default) — emit the first active child's state in sibling order. Lets you write *only the combination rules* — singletons fall through automatically. Sibling order becomes the singleton priority.
- `Nothing` — contribute no state; the parent container falls through to its next sibling.

Example UI button:

```
AllOfAggregator (onNoMatch = PassthroughFirstActive)
  rules:
    [Disabled, Hover] → "Disabled Hover"
  children (sibling order = priority):
    Disabled  (ConstantProvider)
    Pressed   (ConstantProvider)
    Hover     (ConstantProvider)
```

| Inputs active | Wins by | Output |
|---|---|---|
| Disabled + Hover | rule | `Disabled Hover` |
| Disabled | passthrough | `Disabled` |
| Pressed (± Hover) | passthrough | `Pressed` |
| Hover | passthrough | `Hover` |

Note that `"Hover"` is referenced *twice* across the configuration — once inside the rule's `required` list, once as a singleton passthrough fallback. There's only **one** Hover provider; reuse happens at the state-name level, not the provider level.

## See also

- `../CLAUDE.md` — HierarchyNode runtime architecture overview.
- `../HierarchyAggregator.cs` — the base class.
- `../../../Builtins/HierarchyProviders/CLAUDE.md` — provider catalogue for what feeds aggregators.
