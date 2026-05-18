<!-- Last updated: 2026-05-17 (Phase 1 redesign — graph window deferred to Phase 2) -->

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
| `HierarchyNodeEditor.cs` | `HierarchyNode` | Inspector: aggregated-state banner, list of sources on the host with per-source 3-state override pill, live-preview toggle. |
| `DexterityEdgeDrawer.cs` | `DexterityEdge` | Property drawer for source `outputs` lists: target dropdown (Out node + aggregators on host) + port-name dropdown when target is the Out node. |
| `HierarchyEditorPreviewDriver.cs` | (static, `[InitializeOnLoadMethod]`) | Single global handler for `HierarchyPreviewOverrides.onChanged`. Walks every `HierarchyNode` in the scene, diffs against a per-node "rendered state" cache, and queues serialized Modifier transitions via `EditorTransitions`. |

## HierarchyEditorPreviewDriver

Why it exists: `EditorTransitions.TransitionAsync` owns the global `Database` singleton with a `using` scope. Two concurrent calls race over Database create/destroy. The driver serializes all preview transitions through one coroutine.

### Design

- Subscribes to `HierarchyPreviewOverrides.onChanged` once at editor load via `[InitializeOnLoadMethod]`.
- On every change: scans every `HierarchyNode` in the scene, evaluates each via `EvaluateTreeEditor()`, diffs against `s_renderedState` cache.
- Affected nodes' modifiers are queued in `s_pending` keyed by owner — coalesced (only the latest target state survives per owner).
- A single coroutine pumps the queue, running one `EditorTransitions.TransitionAsync` at a time.

### Consequences

- Preview works for **every** HierarchyNode in the scene whenever overrides change — even nodes whose inspector isn't open.
- Cross-node refs (`NodeStateProvider`) automatically cascade: change Node A → Node A repaints → Node B (depending on A) re-evaluates → Node B's modifiers also queue.
- `kPreviewSpeed = 6f` keeps perceived latency low when multiple transitions chain.

## DexterityEdgeDrawer

Phase 1 authoring UX before the new graph window lands.

- **target**: replaced with a dropdown of `HierarchyNode` + aggregators on the SAME GameObject as the source. ObjectField is hidden — designers can't accidentally point at a foreign GO.
- **targetPort**: only shown when target is a `HierarchyNode`. Dropdown of the node's declared `stateInputs` port names (read live from the SerializedObject).
- All writes go through `SerializedProperty` + `ApplyModifiedProperties` so Unity's prefab-override tracking sees them.

## What about HierarchyGraphWindow?

Removed in Phase 1. The previous read-only window was structurally tied to the transform-tree provider model. The new editable graph window — `UnityEditor.UIElements.GraphView`-based, with drag-to-connect edges and embedded component inspectors per node — lands in Phase 2. Until then, authoring happens entirely in the Inspector via the edge drawer.

## Patterns to follow

- **Static editor state belongs in the driver, not per-inspector.** The override registry is the single source of truth for edit-time state.
- **All field writes via SerializedObject + ApplyModifiedProperties.** Direct reflection writes bypass Unity's prefab-override tracking (spike-verified).
- **Source enumeration is host-local.** `node.GetComponents<HierarchyStateProvider>()` + `node.GetComponents<HierarchyAggregator>()`. No transform walks.

## See also

- `../Hierarchy/CLAUDE.md` — runtime architecture the editor wraps.
- `../Hierarchy/HierarchyPreviewOverrides.cs` — the override registry.
- `../../../README.md` — designer-facing introduction.
