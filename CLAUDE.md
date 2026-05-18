# Dexterity 2.0 — Agent Index

Declarative state-machine library for animation/visual states. Components declare states (Default, Hover, Pressed, Disabled, Hidden, …) and **Modifiers** translate each state into a visual property change (color, transform, alpha, …). A central **Manager** transitions between states.

This file is the entry point for agents working in Dexterity. Designers should read `README.md` instead.

## Two node families

Dexterity has two ways to compute a node's current state. Pick the one that matches the problem:

| | **FieldNode** (classic) | **GraphNode** (new) |
|---|---|---|
| **State source** | `Gate`s on the node wrap `BaseField`s; a `StateFunction` step tree maps field values → state | Anonymous provider/aggregator components on the SAME GameObject as the node, wired by serialized `DexterityEdge` lists |
| **Authoring** | One central inspector on the node — gates + step tree | Dexterity Graph window (`Tools → Dexterity → Graph`) — drag-to-connect edges, embedded node inspectors. Sources also live on the host GO but are `HideInInspector` by default, edited via the graph window |
| **Evaluation** | Field bitmask + DFS step tree | Topologically-ordered bool evaluation of all sources on host; first state-input port with any active source wins |
| **Edit-time** | Only runs inside the narrow `EditorTransitions` preview path | Always evaluable (host-local component scan + bool math); per-source override pills drive Modifier preview |
| **State names** | Auto-discovered from the StateFunction | Explicit `List<string> stateInputs` on the Out node, plus `initialState` (the fallback) |
| **Built-in inputs** | `BaseField` subclasses: hover, press, raycast, binding, enum, node-state, constant, parent, children, AND, OR, … | `GraphStateProvider` subclasses (anonymous bool sources): hover, press, raycast, binding, enum, node-state, constant |
| **Reuse pattern** | Wire same `BaseField` instance into multiple gates | Anonymous source can fan out via multiple edges; intermediate `GraphAggregator`s combine bools |
| **Best for** | Complex logic-driven nodes with reusable `NodeReference` assets | UI components with drop-in inputs and live edit-mode previewing |

Both node types share the rest of Dexterity: `Modifier`s, transitions, `Database` state-ID lookup, the inspector debug panel, etc.

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
    Graph/                             see Graph/CLAUDE.md
      IDexteritySource.cs              — common source interface (providers + aggregators)
      DexterityEdge.cs                 — source-side edge struct
      GraphStateProvider.cs        — anonymous leaf base
      GraphAggregator.cs           — anonymous intermediate base
      GraphNode.cs                 — the Out node with stateInputs list + topo evaluation
      GraphPreviewOverrides.cs     — global IsActive override registry
      GraphNodePreviewRoot.cs          — opt-in marker component to group GraphNodes for co-preview
      Aggregators/                     see Aggregators/CLAUDE.md
        AndAggregator.cs             — logical AND over connected inputs (was AllOfAggregator)
    Editor/                            see Editor/CLAUDE.md
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
    GraphProviders/                see GraphProviders/CLAUDE.md
      UIHoverProvider.cs               ↔ UIHoverField
      UIPressProvider.cs               ↔ UIPressField
      RaycastHoverProvider.cs          ↔ RaycastHoverField
      RaycastPressProvider.cs          ↔ RaycastPressField
      BindingProvider.cs               ↔ BindingField
      EnumProvider.cs                  ↔ EnumField
      NodeStateProvider.cs             ↔ NodeStateField
      ConstantProvider.cs              ↔ ConstantField
    Modifiers/                         — same modifiers serve both families
```

## Quick rules of thumb

- **Modifiers don't care which node family they bind to.** A `ColorModifier` under a `GraphNode` works the same as under a `FieldNode` — both walk up the hierarchy via `Modifier.TryFindNode()`.
- **GraphNode state evaluation is host-local.** All sources live on the same GameObject as the node — no transform walks, no nested-container plumbing.
- **Edge writes go through SerializedObject.** Direct reflection writes bypass Unity's prefab-override tracking (spike-verified).
- **Two query APIs on GraphNode:** `GetActiveState()` is priority-respecting; `GetRawInput(stateId)` is priority-independent (use for listeners that should react to masked inputs like press-under-disabled).

## Authoring surface

GraphNode authoring lives in the Dexterity Graph window (`Tools → Dexterity → Graph`, or "Open Graph" on a `GraphNode` inspector). Sources carry `HideFlags.HideInInspector` so the Inspector shows the GraphNode and its modifiers but not the underlying providers/aggregators — `DexterityGraphView.EnsureHideFlags` patches older scenes when they're opened.

The `DexterityEdge` property drawer remains as a fallback for inspector-side editing (debugging, or after manually clearing `hideFlags`).

## See also

- [README.md](README.md) — user-facing introduction (Dexterity as a whole).
- [Scripts/Node/Graph/CLAUDE.md](Scripts/Node/Graph/CLAUDE.md) — GraphNode runtime architecture.
- [Scripts/Node/Editor/CLAUDE.md](Scripts/Node/Editor/CLAUDE.md) — editor tooling: graph window, inspector, edge drawer, preview driver.
- [Scripts/Builtins/GraphProviders/CLAUDE.md](Scripts/Builtins/GraphProviders/CLAUDE.md) — built-in provider catalogue.
- `.claude/guides/systems/presentation/dexterity-graph.md` — designer-facing how-to.
