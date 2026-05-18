# GraphAggregator subclasses

Aggregators are intermediate sources in a Dexterity graph: they consume the bool outputs of several upstream sources, combine them into a single bool, and feed that bool to either the Out node or another aggregator.

To add a new aggregator: subclass `GraphAggregator` and override:

```csharp
protected abstract bool ComputeOutput(IReadOnlyList<bool> inputs);
```

`inputs` is the IsActive value of every source whose `DexterityEdge` targets this aggregator, in stable but unspecified topological order. The base class handles override-aware `IsActive`, edge management, host attach/detach, and edit-time `OnValidate` signaling.

Aggregators have no named input ports — they consume their incoming sources as a multiset of bools. (If a future use case needs labeled inputs, the schema can grow then.)

## Built-ins

### `AndAggregator`

Outputs `true` iff every connected input is active. Logical AND.

```csharp
protected override bool ComputeOutput(IReadOnlyList<bool> inputs)
{
    if (inputs.Count == 0) return false;
    foreach (var b in inputs) if (!b) return false;
    return true;
}
```

Example: a "Disabled" state that requires both a `ConstantProvider` (forced disable) AND a `BindingProvider` (data-driven disable) to be active simultaneously.

## First-match priority

The Out node's ordered `stateInputs` is the first-match mechanism — the first port with any active source wins. Aggregators don't carry priority themselves; if you need nested priority within a sub-graph, compose with multiple aggregators feeding ordered ports.

## See also

- `../CLAUDE.md` — GraphNode runtime architecture overview.
- `../GraphAggregator.cs` — the base class.
- `../../../Builtins/GraphProviders/CLAUDE.md` — provider catalogue for what feeds aggregators.
