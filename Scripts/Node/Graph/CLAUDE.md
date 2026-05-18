<!-- Last updated: 2026-05-17 (Phase 1 redesign — host-component model) -->

# GraphNode — Runtime

State node whose current state is decided by an explicit list of **named state inputs** on the node, fed by **bool-output sources** (providers and aggregators) that live as components on the **same GameObject** as the node. Wiring is via serialized `DexterityEdge` lists on each source, not via the transform tree.

## Why this exists alongside FieldNode

`FieldNode` centralizes everything on one component — its `customGates` list owns every input field, and a `StateFunction` step tree decides the output state. Powerful but:

- Authoring is concentrated on one inspector.
- The system relies on `Database` IDs and `Manager`-driven updates, so **nothing runs in edit mode** outside the narrow `EditorTransitions` preview path.

`GraphNode` flips it:

- **Decoupled inputs.** Each input is a separate MonoBehaviour (`GraphStateProvider` subclass), added to the host GameObject. Providers are anonymous bool sources — no state name on the provider.
- **Explicit state list.** The Out node serializes an ordered `List<string> stateInputs` (port names = state names). First port with any active connected source wins. Falls back to `initialState`.
- **Edges as references.** Each source has a `List<DexterityEdge> outputs`. Each edge = `{ Component target, string targetPort }`. Target is the Out node (with a port name) or another aggregator on the same GameObject.
- **Edit-time native.** Evaluation is pure host-local component scan + bool math — no `Database`, no `Manager`. `GraphPreviewOverrides` lets editor tooling force any source's `IsActive`.

## Mental model

```
GameObject "MyButton"
├─ GraphNode  (the Out node — stateInputs: ["Disabled", "Pressed", "Hover"])
├─ RaycastHoverProvider   ── edge → (Out, "Hover")
├─ RaycastPressProvider   ── edge → (Out, "Pressed")
├─ AndAggregator        ── edge → (Out, "Disabled")
├─ ConstantProvider       ── edge → (AndAggregator)
└─ BindingProvider        ── edge → (AndAggregator)
```

All sources implement `IDexteritySource`:

```csharp
bool IsActive { get; }
IReadOnlyList<DexterityEdge> Outputs { get; }
event Action onStateMayHaveChanged;
void NotifyExternalChanged();
```

**Evaluation: first port with any active source wins.** The Out node walks `stateInputs` in order. Aggregators are evaluated in **topological order** (their inputs are resolved first). Cycles fall back to `initialState` with an error log.

## Files in this folder

| File | Purpose |
|---|---|
| `DexterityEdge.cs` | Source-side edge struct: `{ Component target, string targetPort }`. |
| `IDexteritySource.cs` | Common interface for anything with a bool output (providers + aggregators). |
| `GraphStateProvider.cs` | Abstract leaf base. Anonymous bool source. Subclasses override `bool ComputeIsActive()`. |
| `GraphAggregator.cs` | Abstract intermediate base. Anonymous bool source. Subclasses override `bool ComputeOutput(IReadOnlyList<bool> inputs)`. |
| `GraphNode.cs` | The `BaseStateNode` subclass. Owns `stateInputs`, topo-sorted evaluation, `EvaluateTreeEditor()` for edit-time, and `GetRawInput(stateId)` for priority-independent input queries. |
| `GraphPreviewOverrides.cs` | Static override registry — `Set` / `Clear` / `ClearAll` on `IDexteritySource`, with an `onChanged` event the editor driver subscribes to. Runtime asmdef so overrides work in Play mode too. |
| `Aggregators/` | Concrete aggregator subclasses (see that folder's `CLAUDE.md`). |

## Lifecycle

### Source attach/detach

Each provider/aggregator's `OnEnable` calls `GetComponent<GraphNode>().AttachSource(this)`, which subscribes the node's `MarkStateDirty` handler to the source's `onStateMayHaveChanged`. Topology cache invalidates. `OnDisable` calls `DetachSource`.

Execution order ensures the host node enables first (`Manager.nodeExecutionPriority`) before its sources (`Manager.nodeExecutionPriority + 1`).

### Runtime

1. `GraphNode` initializes via `BaseStateNode.OnEnable → Initialize`. Registers states with `Database`, picks `initialStateId`.
2. `Manager` calls `RefreshInternal` once per frame on subscribed nodes. When `stateDirty`, the node evaluates and possibly transitions.
3. When a source's value changes, it fires `onStateMayHaveChanged`. The node sets `stateDirty = true`. Next `Refresh` re-evaluates: `EnsureCachesValid` (topo sort if dirty) → `EvaluateSources` (walks topo order) → first port with any active source wins.

### Edit time

1. Sources don't run `OnEnable` at edit time, but `OnValidate` fires `onStateMayHaveChanged` to push edit-time changes (and the inspector subscribes directly for live preview).
2. Editor tools call `node.EvaluateTreeEditor()` to get the current state string without touching `Database`.
3. `GraphPreviewOverrides.Set/Clear` fires `onChanged`; `GraphEditorPreviewDriver` queues Modifier transitions for every node whose state shifted.

## Two query APIs on `GraphNode`

- `GetActiveState() / activeState` — priority-respecting. The current effective state after first-match resolution. Inherited from `BaseStateNode`.
- `GetRawInput(int stateId)` / `GetRawInput(string portName)` — **priority-independent**. Returns `true` iff any source feeding the port is currently active, even if a higher-priority state masks it. Use this when you want to react to a masked input (e.g. a click listener that fires on-press even when `Disabled` wins).

Also: `HasInputPort(string)` reports whether the node declares a port with that name.

## State name discovery

`GraphNode.GetStateNames()` returns the union of:

- The `initialState` field's value (fallback state).
- Every string in `stateInputs`.

`Modifier.SyncStates` reads this set, so modifiers get one property per state automatically.

> Note: prior versions auto-added `StateFunction.kDefaultState` ("<Default>"). That
> produced a duplicate "default-ish" property whenever the designer set `initialState`
> to anything else. Dropped — initialState is now the single fallback name.

## Override semantics

`IDexteritySource.IsActive` checks `GraphPreviewOverrides.TryGet(this, out var ov)` first. If an override exists, that value is returned regardless of `ComputeIsActive` / `ComputeOutput`. Overrides work on aggregators too — useful for debugging mid-graph signals.

## Cross-node dependencies

`NodeStateProvider` (in `Builtins/GraphProviders/`) is still the bridge — a source that reports active when *another* `BaseStateNode` is in a named state. Runtime: subscribes to `targetNode.onStateChanged`; edit time: compares against `targetNode.EvaluateTreeEditor()` (GraphNode targets only).

## Preview groups (`GraphNodePreviewRoot`)

Edit-time preview is opt-in per node (via a graph window's Preview toggle). To preview *several* related nodes together, add a `GraphNodePreviewRoot` component to a common ancestor GameObject. When any node under that ancestor is previewed, every GraphNode in the subtree joins the animatable set.

Lookup is "topmost root wins": walking up from a node, the outermost `GraphNodePreviewRoot` ancestor defines the group. Nested roots are supported (an inner "Section" root inside an outer "Page" root → the Page is what previews).

Without a preview root, previewing a node only animates that single node. The component is purely declarative — no fields, no upstream-walking. Cross-node deps (`NodeStateProvider`) still evaluate correctly via on-demand `EvaluateTreeEditor` calls; the preview root just decides what *gets animated*, not what gets *computed*.

## Phase 1 caveats

- **No graph window yet.** Phase 1 ships with inspector-only authoring. The `DexterityEdge` custom drawer (under `Scripts/Node/Editor/`) restricts target dropdowns to same-host components and shows port-name dropdowns when targeting the Out node — usable but utilitarian. Phase 2 adds the new graph window.
- **Sources are visible in the Inspector.** No `hideFlags = HideInInspector` in Phase 1 (transparency for debugging). Phase 3 hides them once authoring shifts entirely to the graph window.

## See also

- `Aggregators/CLAUDE.md` — concrete aggregator subclasses.
- `../Editor/CLAUDE.md` — inspector, edge drawer, preview driver.
- `../../Builtins/GraphProviders/CLAUDE.md` — provider catalogue.
- `../BaseStateNode.cs` — parent class. Read this if you're adding a new node type.
- `../FieldNode.cs` — compare against the classic node implementation.
