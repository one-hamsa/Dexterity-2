# Node Editors

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
| `GraphNodeEditor.cs` | `GraphNode` | Inspector: aggregated-state banner, `stateInputs` list, "Open Graph" button. Per-source override pills live in the graph window, not the inspector. |
| `DexterityGraphWindow.cs` | (EditorWindow) | Host for a `DexterityGraphView`. Menu entry `Tools/Dexterity/Graph`; also opened per-node via `OpenFor(node)` from the inspector. Multiple windows can be open simultaneously. |
| `DexterityGraphView.cs` | (UIElements GraphView) | The graph itself: drag-to-connect edges, embedded provider/aggregator inspectors, Spacebar add-source. Enforces `HideFlags.HideInInspector` on sources via `EnsureHideFlags`. All edits commit through `SerializedObject`. |
| `DexterityAddSourceSearchProvider.cs` | (graph popup) | "Add Source" search popup — Spacebar or right-click in the graph. Enumerates `GraphStateProvider` / `GraphAggregator` subclasses. |
| `DexterityEdgeDrawer.cs` | `DexterityEdge` | Fallback property drawer for source `outputs` lists when something opens a hidden source in the inspector. Target dropdown (Out node + aggregators on host) + port-name dropdown when target is the Out node. |
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
- Cross-node refs (`NodeStateProvider`) automatically cascade: change Node A → Node A repaints → Node B (depending on A) re-evaluates → Node B's modifiers also queue.
- `kPreviewSpeed = 6f` keeps perceived latency low when multiple transitions chain.

## DexterityGraphWindow + DexterityGraphView

Primary authoring surface for GraphNodes. Open via `Tools → Dexterity → Graph`, or click "Open Graph" on a `GraphNode` inspector (each click opens a fresh window — multiple can be live at once).

- **Drag-to-connect edges.** GraphView native edge handling routes through `OnGraphViewChanged` → `CommitEdgeCreation` / removal / move. All commits go via `SerializedObject` + `ApplyModifiedProperties`.
- **Spacebar add-source.** `DexterityAddSourceSearchProvider` lists every `GraphStateProvider` / `GraphAggregator` subclass; selecting one adds the component to the host GO with `HideFlags.HideInInspector` already set.
- **Embedded node inspectors.** Each provider/aggregator/Out-node view embeds its own inspector body — edit ports, fields, and binding paths inline.
- **HideFlags enforcement.** `EnsureHideFlags` runs whenever a node opens or refreshes — patches older scenes/prefabs where a source's flags drifted to `None`.

## DexterityEdgeDrawer

Fallback drawer for `DexterityEdge` lists when something opens a source in the inspector (debugging, or after manually clearing `hideFlags`).

- **target**: dropdown of `GraphNode` + aggregators on the SAME GameObject as the source. ObjectField is hidden — can't accidentally point at a foreign GO.
- **targetPort**: only shown when target is a `GraphNode`. Dropdown of the node's declared `stateInputs` port names (read live from the SerializedObject).
- All writes go through `SerializedProperty` + `ApplyModifiedProperties` so Unity's prefab-override tracking sees them.

## Patterns to follow

- **Static editor state belongs in the driver, not per-inspector.** The override registry is the single source of truth for edit-time state.
- **All field writes via SerializedObject + ApplyModifiedProperties.** Direct reflection writes bypass Unity's prefab-override tracking (spike-verified).
- **Source enumeration is host-local.** `node.GetComponents<GraphStateProvider>()` + `node.GetComponents<GraphAggregator>()`. No transform walks.

## See also

- `../Graph/CLAUDE.md` — runtime architecture the editor wraps.
- `../Graph/GraphPreviewOverrides.cs` — the override registry.
- `../../../README.md` — designer-facing introduction.
