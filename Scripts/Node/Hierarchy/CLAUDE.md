<!-- Last updated: 2026-05-13 -->

# HierarchyNode — Runtime

State node whose current state is computed by walking a tree of MonoBehaviour components in its transform subtree. Designed for UI-style stateful visuals where each input signal lives on its own GameObject (hover, press, "is selected", "is disabled", …) and can be authored as drop-in prefabs.

## Why this exists alongside FieldNode

`FieldNode` centralizes everything on one component — its `customGates` list owns every input field, and a `StateFunction` step tree decides the output state. Powerful but:

- Authoring is concentrated on one inspector.
- The system relies on `Database` IDs and `Manager`-driven updates, so **nothing runs in edit mode** outside the narrow `EditorTransitions` preview path.
- Cross-cutting concerns (e.g. "this prefab adds Hover behavior") have to be re-wired into the gate list every time.

`HierarchyNode` flips it:

- **Decoupled inputs.** Each input is a separate MonoBehaviour (`HierarchyStateProvider` subclass). Drop a `HoverProvider` prefab on any child GameObject — it self-registers with the nearest enclosing container on enable.
- **Free-text states.** No `StateFunction` asset. State *names* are plain strings declared per provider; the active state is computed by walking the tree.
- **Edit-time native.** Evaluation is pure string-in / string-out — no `Database`, no `Manager`. The graph window and inspector evaluate the tree synchronously whenever needed.
- **Override-friendly.** A static `HierarchyPreviewOverrides` registry lets editor tooling pin any provider's `IsActive` to true/false/none for live preview.

## Mental model

```
HierarchyNode  (root of the tree, the BaseStateNode subclass)
  ├── HierarchyStateProvider (leaf — declares one state, decides if it's active)
  ├── HierarchyAggregator    (branch — combines its descendants into one state)
  │     ├── HierarchyStateProvider
  │     └── HierarchyStateProvider
  └── HierarchyStateProvider
```

Both leaves and aggregators implement `IHierarchyStateProvider`:

```
bool TryGetState(out string state);
IEnumerable<string> GetDeclaredStates();
event Action onStateMayHaveChanged;
```

Evaluation is **first-match in transform-sibling order**: the node walks its direct children, the first whose `TryGetState` returns `true` wins. Aggregators interpose composite logic (rules, all-of combinations, …) before participating as if they were a single provider to their parent.

## Files in this folder

| File | Purpose |
|---|---|
| `IHierarchyStateProvider.cs` | Provider interface — implemented by leaves and aggregators. |
| `IHierarchyContainer.cs` | Internal interface — implemented by `HierarchyAggregator` and `HierarchyNode`. Just the register/unregister surface used by self-registering providers. |
| `HierarchyUtils.cs` | Two helpers: `FindNearestContainer(transform)` (walk-up, mirrors `Modifier.TryFindNode`) and `CollectOrderedDirectProviders(root, output)` (DFS scan with stop-at-nested-container). |
| `HierarchyStateProvider.cs` | Abstract leaf base. Holds the serialized `state` string. Subclasses override `bool ComputeIsActive()`. |
| `HierarchyAggregator.cs` | Abstract branch base. Subclasses override `bool TryAggregate(orderedChildren, out result)` and `IEnumerable<string> GetDeclaredStates()`. |
| `HierarchyNode.cs` | The `BaseStateNode` subclass. Owns root-level container registration, evaluates the tree, exposes `EvaluateTreeEditor()` for edit-time tooling. |
| `HierarchyPreviewOverrides.cs` | Static override registry — `Set` / `Clear` / `ClearAll`, with an `onChanged` event the editor driver subscribes to globally. Lives in the runtime asmdef so overrides work in Play mode too. |
| `Aggregators/` | Concrete aggregator subclasses (see that folder's `CLAUDE.md`). |

## Lifecycle

### Runtime

1. `HierarchyStateProvider.OnEnable` calls `HierarchyUtils.FindNearestContainer(transform)` and registers itself. Same for `HierarchyAggregator` (which is both a provider and a container).
2. `HierarchyNode.Initialize` (inherited from `BaseStateNode`) registers the node and all its state names with `Database`, picks `initialStateId`, sets `stateDirty = true`.
3. `Manager` calls `RefreshInternal` once per frame on subscribed nodes. When `stateDirty`, the node evaluates and possibly transitions.
4. When a provider's value changes, it fires `onStateMayHaveChanged`. Containers subscribed to it forward the event up; the node sets `stateDirty`. Next `Refresh` picks it up.

### Edit time

1. `OnEnable` doesn't run on plain MonoBehaviours in edit mode → no self-registration.
2. `HierarchyGraphWindow` and `HierarchyEditorPreviewDriver` scan transform trees directly via `HierarchyUtils.CollectOrderedDirectProviders` whenever they need fresh state.
3. `HierarchyNode.EvaluateTreeEditor()` returns the would-be state string by walking the tree — no `Database` needed.
4. `HierarchyPreviewOverrides.Set/Clear` fires its `onChanged` event; the driver picks up and queues a Modifier transition for every node whose state shifted.

## State name discovery

`HierarchyNode.GetStateNames()` returns:

- The `initialState` field's value.
- `StateFunction.kDefaultState` (`"<Default>"`).
- Every string returned by `IHierarchyStateProvider.GetDeclaredStates()` for every descendant provider / aggregator (recursive across aggregator subtrees too).

`Modifier.SyncStates` reads this set, so `ColorModifier` / `TransformModifier` / etc. get one `Property` per discovered state automatically.

## Override semantics

`HierarchyStateProvider.IsActive` checks `HierarchyPreviewOverrides.TryGet(this, out var ov)` first. If an override exists, that value is returned regardless of `ComputeIsActive`. Otherwise falls through to subclass logic. Subclasses are responsible for being edit-time-safe — most concrete providers return `false` at edit time because their inputs aren't wired (no raycast controllers, no UI events) and that's fine; the override mechanism is how designers simulate state.

Overrides apply at runtime too, which lets you cheat live game state from the graph window during testing.

## Cross-node dependencies

`NodeStateProvider` (in `Builtins/HierarchyProviders/`) is the bridge — it's a `HierarchyStateProvider` that reports active when *another* `BaseStateNode` is in a named state. At runtime it subscribes to `targetNode.onStateChanged`; at edit time it compares strings via `targetNode.EvaluateTreeEditor()` (HierarchyNode targets only).

Graph window visualizes this as a "ghost" box of the target node, with an orange edge from the `NodeStateProvider` to it. Click the ghost to open the target's graph in a new window.

## See also

- `Aggregators/CLAUDE.md` — concrete aggregator subclasses.
- `../Editor/CLAUDE.md` — inspector, graph window, preview driver.
- `../../Builtins/HierarchyProviders/CLAUDE.md` — provider catalogue with `BaseField` ↔ provider mapping.
- `../BaseStateNode.cs` — parent class. Read this if you're adding a new node type.
- `../FieldNode.cs` — compare against the classic node implementation.
