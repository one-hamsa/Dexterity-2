# Dexterity 2.0 — Engineering Reference

Implementation reference for engineers working inside Dexterity. Read this before modifying anything under `Assets/Standard Assets/Dexterity 2.0/`. For the designer-facing how-to, see `.claude/guides/systems/presentation/dexterity-graph.md` (GraphNode) and the in-folder `README.md`.

Dexterity is a declarative state-machine library for animation/visual states. Components declare states (Default, Hover, Pressed, Disabled, Hidden, …) and **Modifiers** translate each state into a visual property change (color, transform, alpha, …). A central **Manager** transitions between states.

---

## Two node families

Dexterity has two ways to compute a node's current state. Pick the one that matches the problem:

| | **FieldNode** (classic) | **GraphNode** (new) |
|---|---|---|
| **State source** | `Gate`s on the node wrap `BaseField`s; a `StateFunction` step tree maps field values → state | Anonymous source/operator components on the SAME GameObject as the node, wired by serialized `DexterityEdge` lists |
| **Authoring** | One central inspector on the node — gates + step tree | Dexterity Graph window (`Tools → Dexterity → Graph`) — drag-to-connect edges, embedded node inspectors. Sources also live on the host GO but are `HideInInspector` by default, edited via the graph window |
| **Evaluation** | Field bitmask + DFS step tree | Topologically-ordered bool evaluation of all sources on host; first state-input port with any active source wins |
| **Edit-time** | Only runs inside the narrow `EditorTransitions` preview path | Always evaluable (host-local component scan + bool math); per-source override pills drive Modifier preview |
| **State names** | Auto-discovered from the StateFunction | Explicit `List<string> states` (+ a raw `inputs` list) on the Out node, plus `initialState` (the fallback) |
| **Built-in inputs** | `BaseField` subclasses: hover, press, raycast, binding, enum, node-state, constant, parent, children, AND, OR, … | `GraphSource` subclasses (anonymous bool sources): hover, press, raycast, binding, enum, node-state, constant |
| **Reuse pattern** | Wire same `BaseField` instance into multiple gates | Anonymous source can fan out via multiple edges; intermediate `GraphOperator`s combine bools |
| **Best for** | Complex logic-driven nodes with reusable `NodeReference` assets | UI components with drop-in inputs and live edit-mode previewing |

Both node types share the rest of Dexterity: `Modifier`s, transitions, `Database` state-ID lookup, the inspector debug panel, etc.

---

## Folder map

```
Scripts/
  Node/
    BaseStateNode.cs                   — abstract base for both families
    FieldNode.cs                       — classic node
    StateProxyNode.cs                  — re-emits another node's states
    BaseEnumStateNode.cs               — SimpleEnumNode / BindingEnumNode parents
    NodeReference.cs                   — shared gate/step-tree asset
    OutputField.cs                     — internal FieldNode field result
    Graph/
      IDexteritySource.cs              — common source interface (sources + operators)
      DexterityEdge.cs                 — source-side edge struct
      GraphSource.cs            — anonymous leaf base
      GraphOperator.cs               — anonymous intermediate base
      GraphNode.cs                     — the Out node with states + inputs lists + topo evaluation
      GraphPreviewOverrides.cs         — global IsActive override registry
      GraphNodePreviewRoot.cs          — opt-in marker component to group GraphNodes for co-preview
      Operators/
        AndOperator.cs               — logical AND over connected inputs (was AllOfAggregator)
    Editor/
      ... (existing FieldNode editors)
      GraphNodeEditor.cs               — inspector with state banner + "Open Graph" button
      DexterityGraphWindow.cs          — EditorWindow host for the graph view
      DexterityGraphView.cs            — `UIElements.GraphView` impl: drag-to-connect edges, embedded node inspectors, hideFlags enforcement
      DexterityAddSourceSearchProvider.cs — Spacebar / right-click "Add Source" search popup inside the graph
      DexterityEdgeDrawer.cs           — property drawer for source outputs (fallback for FieldNode-style inspectors)
      GraphEditorPreviewDriver.cs      — global edit-time transition driver
      DexterityPreview.cs              — modifier preview helpers
  Builtins/
    Fields/                            — classic BaseField subclasses
    GraphSources/
      UIHoverSource.cs               ↔ UIHoverField
      UIPressSource.cs               ↔ UIPressField
      RaycastHoverSource.cs          ↔ RaycastHoverField
      RaycastPressSource.cs          ↔ RaycastPressField
      BindingSource.cs               ↔ BindingField
      EnumSource.cs                  ↔ EnumField
      NodeStateSource.cs             ↔ NodeStateField
      ConstantSource.cs              ↔ ConstantField
    Modifiers/                         — same modifiers serve both families
```

---

## Cross-cutting rules of thumb

- **Modifiers don't care which node family they bind to.** A `ColorModifier` under a `GraphNode` works the same as under a `FieldNode` — both walk up the hierarchy via `Modifier.TryFindNode()`.
- **GraphNode state evaluation is host-local.** All sources live on the same GameObject as the node — no transform walks, no nested-container plumbing.
- **Edge writes go through SerializedObject.** Direct reflection writes bypass Unity's prefab-override tracking (spike-verified).
- **Two query APIs on GraphNode:** `GetActiveState()` is priority-respecting; `GetRawInput(stateId)` is priority-independent (use for listeners that should react to masked inputs like press-under-disabled).

## Authoring surface

GraphNode authoring lives in the Dexterity Graph window (`Tools → Dexterity → Graph`, or "Open Graph" on a `GraphNode` inspector). Sources carry `HideFlags.HideInInspector` so the Inspector shows the GraphNode and its modifiers but not the underlying sources/operators — `DexterityGraphView.EnsureHideFlags` patches older scenes when they're opened.

The `DexterityEdge` property drawer remains as a fallback for inspector-side editing (debugging, or after manually clearing `hideFlags`).

---

# GraphNode — Runtime

State node whose current state is decided by an explicit list of **named state inputs** on the node, fed by **bool-output sources** (sources and operators) that live as components on the **same GameObject** as the node. Wiring is via serialized `DexterityEdge` lists on each source, not via the transform tree.

## Why this exists alongside FieldNode

`FieldNode` centralizes everything on one component — its `customGates` list owns every input field, and a `StateFunction` step tree decides the output state. Powerful but:

- Authoring is concentrated on one inspector.
- The system relies on `Database` IDs and `Manager`-driven updates, so **nothing runs in edit mode** outside the narrow `EditorTransitions` preview path.

`GraphNode` flips it:

- **Decoupled inputs.** Each input is a separate MonoBehaviour (`GraphSource` subclass), added to the host GameObject. Sources are anonymous bool sources — no state name on the source.
- **Explicit state list.** The Out node serializes an ordered `List<string> states` (port names = state names) plus a parallel `List<string> inputs` of raw signal ports. First state port with any active connected source wins. Falls back to `initialState`. Input ports are readable via `GetRawInput` but never become the active state.
- **Edges as references.** Each source has a `List<DexterityEdge> outputs`. Each edge = `{ Component target, string targetPort }`. Target is the Out node (with a port name) or another operator on the same GameObject.
- **Edit-time native.** Evaluation is pure host-local component scan + bool math — no `Database`, no `Manager`. `GraphPreviewOverrides` lets editor tooling force any source's `IsActive`.

## Mental model

```
GameObject "MyButton"
├─ GraphNode  (the Out node — states: ["Disabled", "Pressed", "Hover"])
├─ RaycastHoverSource   ── edge → (Out, "Hover")
├─ RaycastPressSource   ── edge → (Out, "Pressed")
├─ AndOperator          ── edge → (Out, "Disabled")
├─ ConstantSource       ── edge → (AndOperator)
└─ BindingSource        ── edge → (AndOperator)
```

All sources implement `IDexteritySource`:

```csharp
bool IsActive { get; }
IReadOnlyList<DexterityEdge> Outputs { get; }
event Action onStateMayHaveChanged;
void NotifyExternalChanged();
```

**Evaluation: first port with any active source wins.** The Out node walks `states` in order. Operators are evaluated in **topological order** (their inputs are resolved first). Cycles fall back to `initialState` with an error log.

## Files in `Scripts/Node/Graph/`

| File | Purpose |
|---|---|
| `DexterityEdge.cs` | Source-side edge struct: `{ Component target, string targetPort }`. |
| `IDexteritySource.cs` | Common interface for anything with a bool output (sources + operators). |
| `GraphSource.cs` | Abstract leaf base. Anonymous bool source. Subclasses override `bool ComputeIsActive()`. |
| `GraphOperator.cs` | Abstract intermediate base. Anonymous bool source. Subclasses override `bool ComputeOutput(IReadOnlyList<bool> inputs)`. |
| `GraphNode.cs` | The `BaseStateNode` subclass. Owns the `states` + `inputs` port lists, topo-sorted evaluation, `EvaluateTreeEditor()` for edit-time, and `GetRawInput(stateId)` for priority-independent input queries. |
| `GraphPreviewOverrides.cs` | Static override registry — `Set` / `Clear` / `ClearAll` on `IDexteritySource`, with an `onChanged` event the editor driver subscribes to. Runtime asmdef so overrides work in Play mode too. |
| `Operators/` | Concrete operator subclasses (see Operators section below). |

## Lifecycle

### Source attach/detach

Each source/operator's `OnEnable` calls `GetComponent<GraphNode>().AttachSource(this)`, which subscribes the node's `MarkStateDirty` handler to the source's `onStateMayHaveChanged`. Topology cache invalidates. `OnDisable` calls `DetachSource`.

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
- Every string in `states` (the `inputs` list is excluded).

`Modifier.SyncStates` reads this set, so modifiers get one property per state automatically.

> Note: prior versions auto-added `StateFunction.kDefaultState` ("<Default>"). That
> produced a duplicate "default-ish" property whenever the designer set `initialState`
> to anything else. Dropped — initialState is now the single fallback name.

## Override semantics

`IDexteritySource.IsActive` checks `GraphPreviewOverrides.TryGet(this, out var ov)` first. If an override exists, that value is returned regardless of `ComputeIsActive` / `ComputeOutput`. Overrides work on operators too — useful for debugging mid-graph signals.

## Cross-node dependencies

`NodeStateSource` (in `Builtins/GraphSources/`) is still the bridge — a source that reports active when *another* `BaseStateNode` is in a named state. Runtime: subscribes to `targetNode.onStateChanged`; edit time: compares against `targetNode.EvaluateTreeEditor()` (GraphNode targets only).

## Preview groups (`GraphNodePreviewRoot`)

Edit-time preview is opt-in per node (via a graph window's Preview toggle). To preview *several* related nodes together, add a `GraphNodePreviewRoot` component to a common ancestor GameObject. When any node under that ancestor is previewed, every GraphNode in the subtree joins the animatable set.

Lookup is "topmost root wins": walking up from a node, the outermost `GraphNodePreviewRoot` ancestor defines the group. Nested roots are supported (an inner "Section" root inside an outer "Page" root → the Page is what previews).

Without a preview root, previewing a node only animates that single node. The component is purely declarative — no fields, no upstream-walking. Cross-node deps (`NodeStateSource`) still evaluate correctly via on-demand `EvaluateTreeEditor` calls; the preview root just decides what *gets animated*, not what gets *computed*.

## Authoring path

GraphNode authoring lives in the graph window (`Tools → Dexterity → Graph`, or the "Open Graph" button on a `GraphNode` inspector). Drag-to-connect edges, Spacebar to add a source/operator, embedded inspectors per node.

Sources carry `HideFlags.HideInInspector` (enforced by `DexterityGraphView.EnsureHideFlags` whenever the window opens a node). They still serialize normally and still show up in component reflection — they just don't appear in the Inspector. The `DexterityEdge` property drawer (`Scripts/Node/Editor/`) remains as a fallback path, used by debug inspectors and by anyone who removes the hideFlags manually.

---

# Node Editors (`Scripts/Node/Editor/`)

Editor scripts for both node families. This folder is mapped into `Dexterity.Editor` via `Dexterity.Editor.asmref`.

## Files

| File | Targets | Purpose |
|---|---|---|
| `BaseStateNodeEditor.cs` | all `BaseStateNode` subclasses | Base inspector. Shared chrome: initial-state picker, delays, override state, debug section, preview-state dropdown, modifiers list, multi-target handling. |
| `FieldNodeEditor.cs` | `FieldNode` | Gates/fields tab, step-tree viewer (`StepListView`), output-field debug. |
| `NodeReferenceEditor.cs` / `NodeReferenceEditorWindow.cs` | `NodeReference` | Gate / step-tree authoring on the shared asset. |
| `BindingEnumNodeEditor.cs` | `BindingEnumNode` | Enum binding picker. |
| `SimpleEnumNodeEditor.cs` | `SimpleEnumNode` | Manual state list. |
| `StateProxyNodeEditor.cs` | `StateProxyNode` | Source-node → state-name remapping list. |
| `GraphNodeEditor.cs` | `GraphNode` | Inspector: aggregated-state banner, `states` + `inputs` lists, "Open Graph" button. Per-source override pills live in the graph window, not the inspector. |
| `DexterityGraphWindow.cs` | (EditorWindow) | Host for a `DexterityGraphView`. Menu entry `Tools/Dexterity/Graph`; also opened per-node via `OpenFor(node)` from the inspector. Multiple windows can be open simultaneously. |
| `DexterityGraphView.cs` | (UIElements GraphView) | The graph itself: drag-to-connect edges, embedded source/operator inspectors, Spacebar add-source. Enforces `HideFlags.HideInInspector` on sources via `EnsureHideFlags`. All edits commit through `SerializedObject`. |
| `DexterityAddSourceSearchProvider.cs` | (graph popup) | "Add Source" search popup — Spacebar or right-click in the graph. Enumerates `GraphSource` / `GraphOperator` subclasses. |
| `DexterityEdgeDrawer.cs` | `DexterityEdge` | Fallback property drawer for source `outputs` lists when something opens a hidden source in the inspector. Target dropdown (Out node + operators on host) + port-name dropdown when target is the Out node. |
| `GraphEditorPreviewDriver.cs` | (static, `[InitializeOnLoadMethod]`) | Single global handler for `GraphPreviewOverrides.onChanged`. Walks every `GraphNode` in the scene, diffs against a per-node "rendered state" cache, and queues serialized Modifier transitions via `EditorTransitions`. |
| `DexterityPreview.cs` | (static) | Modifier preview helpers shared by inspector + graph window. |

## GraphEditorPreviewDriver

Why it exists: `EditorTransitions.TransitionAsync` owns the global `Database` singleton with a `using` scope. Two concurrent calls race over Database create/destroy. The driver serializes all preview transitions through one coroutine.

### Design

- Subscribes to `GraphPreviewOverrides.onChanged` once at editor load via `[InitializeOnLoadMethod]`.
- On every change: scans every `GraphNode` in the scene, evaluates each via `EvaluateTreeEditor()`, diffs against `s_renderedState` cache.
- Affected nodes' modifiers are queued in `s_pending` keyed by owner — coalesced (only the latest target state survives per owner).
- A single coroutine pumps the queue, running one `EditorTransitions.TransitionAsync` at a time.

### Consequences

- Preview works for **every** GraphNode in the scene whenever overrides change — even nodes whose inspector isn't open.
- Cross-node refs (`NodeStateSource`) automatically cascade: change Node A → Node A repaints → Node B (depending on A) re-evaluates → Node B's modifiers also queue.
- `kPreviewSpeed = 6f` keeps perceived latency low when multiple transitions chain.

## DexterityGraphWindow + DexterityGraphView

Primary authoring surface for GraphNodes. Open via `Tools → Dexterity → Graph`, or click "Open Graph" on a `GraphNode` inspector (each click opens a fresh window — multiple can be live at once).

- **Drag-to-connect edges.** GraphView native edge handling routes through `OnGraphViewChanged` → `CommitEdgeCreation` / removal / move. All commits go via `SerializedObject` + `ApplyModifiedProperties`.
- **Spacebar add-source.** `DexterityAddSourceSearchProvider` lists every `GraphSource` / `GraphOperator` subclass; selecting one adds the component to the host GO with `HideFlags.HideInInspector` already set.
- **Embedded node inspectors.** Each source/operator/Out-node view embeds its own inspector body — edit ports, fields, and binding paths inline.
- **HideFlags enforcement.** `EnsureHideFlags` runs whenever a node opens or refreshes — patches older scenes/prefabs where a source's flags drifted to `None`.

## DexterityEdgeDrawer

Fallback drawer for `DexterityEdge` lists when something opens a source in the inspector (debugging, or after manually clearing `hideFlags`).

- **target**: dropdown of `GraphNode` + operators on the SAME GameObject as the source. ObjectField is hidden — can't accidentally point at a foreign GO.
- **targetPort**: only shown when target is a `GraphNode`. Dropdown of the node's declared port names — both `states` and `inputs` (read live from the SerializedObject); input ports are tagged `(input)`.
- All writes go through `SerializedProperty` + `ApplyModifiedProperties` so Unity's prefab-override tracking sees them.

## Editor patterns to follow

- **Static editor state belongs in the driver, not per-inspector.** The override registry is the single source of truth for edit-time state.
- **All field writes via SerializedObject + ApplyModifiedProperties.** Direct reflection writes bypass Unity's prefab-override tracking (spike-verified).
- **Source enumeration is host-local.** `node.GetComponents<GraphSource>()` + `node.GetComponents<GraphOperator>()`. No transform walks.

---

# GraphOperator subclasses (`Scripts/Node/Graph/Operators/`)

Operators are intermediate sources in a Dexterity graph: they consume the bool outputs of several upstream sources, combine them into a single bool, and feed that bool to either the Out node or another operator.

To add a new operator: subclass `GraphOperator` and override:

```csharp
protected abstract bool ComputeOutput(IReadOnlyList<bool> inputs);
```

`inputs` is the IsActive value of every source whose `DexterityEdge` targets this operator, in stable but unspecified topological order. The base class handles override-aware `IsActive`, edge management, host attach/detach, and edit-time `OnValidate` signaling.

Operators have no named input ports — they consume their incoming sources as a multiset of bools. (If a future use case needs labeled inputs, the schema can grow then.)

## Built-ins

### `AndOperator`

Outputs `true` iff every connected input is active. Logical AND.

```csharp
protected override bool ComputeOutput(IReadOnlyList<bool> inputs)
{
    if (inputs.Count == 0) return false;
    foreach (var b in inputs) if (!b) return false;
    return true;
}
```

Example: a "Disabled" state that requires both a `ConstantSource` (forced disable) AND a `BindingSource` (data-driven disable) to be active simultaneously.

## First-match priority

The Out node's ordered `states` list is the first-match mechanism — the first port with any active source wins. Operators don't carry priority themselves; if you need nested priority within a sub-graph, compose with multiple operators feeding ordered ports.

---

# GraphSources — Built-in leaf catalogue (`Scripts/Builtins/GraphSources/`)

Each file here is a concrete `GraphSource` subclass that ports an existing `BaseField` to the GraphNode system. Add one to the same GameObject as your `GraphNode`, then wire its `outputs` list to feed a state-input port on the Out node (or to an operator).

Each source has:
- A `List<DexterityEdge> outputs` (inherited from base) — where its bool output is fed.
- Subclass-specific input fields (raycast tag, binding target, target node, etc.).
- A `ComputeIsActive()` override that returns the bool this source currently contributes.

Sources are **anonymous** — no state name on the source. The state name is determined by which port the output edge feeds.

## Catalogue

| Source | Ports | When it's active | Edit-time |
|---|---|---|---|
| `UIHoverSource` | `UIHoverField` | Unity EventSystem pointer is hovering this UI element (`IPointerEnterHandler`/`IPointerExitHandler`). | inactive (no pointer events fire) |
| `UIPressSource` | `UIPressField` | Unity EventSystem pointer is pressed on this UI element (`IPointerDownHandler`/`IPointerUpHandler`). | inactive |
| `RaycastHoverSource` | `RaycastHoverField` | Any registered `IRaycastController` with the configured tag is hovering this collider. | inactive |
| `RaycastPressSource` | `RaycastPressField` | Any registered `IRaycastController` with the configured tag is pressing this collider. Has `stayPressedOutOfBounds` option. | inactive |
| `BindingSource` | `BindingField` | A reflection-bound boolean property/method on any `UnityEngine.Object` evaluates to true (or false, with `negate`). | inactive until binding initializes (runtime) |
| `EnumSource` | `EnumField` | A referenced `BindingEnumNode`'s current enum case equals the configured target case. | inactive (depends on binding initialization) |
| `NodeStateSource` | `NodeStateField` | A referenced `BaseStateNode` is currently in the configured target state. **Cross-node dependency bridge.** | active iff target is a `GraphNode` and its `EvaluateTreeEditor()` matches the target state |
| `ConstantSource` | `ConstantField` | Always-on or always-off, based on a serialized `active` bool. | honors `active` directly |

## Implementation patterns

**Event-driven sources** (UIHover/Press, NodeStateSource): override `OnEnable`/`OnDisable` to subscribe/unsubscribe, internal state is updated by handler callbacks calling `MarkChanged()`.

**Polling sources** (Binding, Enum, Raycast): an internal `Update()` method compares current `ComputeIsActive()` against a cached `_lastActive` and fires `MarkChanged()` on diff. Necessary when the underlying source doesn't expose a change event.

**Constant sources**: trivial — `ComputeIsActive()` returns the serialized flag directly. Useful as a terminal fallback at the end of an operator's inputs.

## Note on `NodeStateSource`

Edit-time behavior is special. The base `IsActive` getter calls `ComputeIsActive()` whose implementation has two branches:
- Runtime: compares `targetNode.GetActiveState()` (int) against a cached state ID.
- Edit-time: compares `targetNode.EvaluateTreeEditor()` (string) against `targetState` — works for `GraphNode` targets without `Database`.

This means cross-node dependencies "just work" at edit time: toggle a source override in Node A, Node B's `NodeStateSource`s that target Node A see the change immediately via the driver.

## Adding a new source

1. Create a `GraphSource` subclass.
2. Add an `[AddComponentMenu("Dexterity/Graph/Sources/...")]` so designers can find it.
3. Override `ComputeIsActive()`. Be defensive at edit time — if your inputs aren't wired (raycast controllers, UI events, runtime services), return `false`. The override registry covers simulation, so you don't need to invent edit-mode mocks.
4. If your input has an event, subscribe in `OnEnable` and call `MarkChanged()` from the handler. If polled, add an `Update()` with a diff check.

---

## See also

- `README.md` — user-facing introduction (Dexterity as a whole).
- `.claude/guides/systems/presentation/dexterity-graph.md` — designer-facing how-to for GraphNode.
- `.claude/skills/ui/SKILL.md` — the `/ui` workflow for building a widget end-to-end with agentic edit-time validation.
- `Samples/Scenes/HierarchyNode Showcase.unity` — four demos in one scene: order priority, AllOf combination, realistic button states, cross-node refs.
