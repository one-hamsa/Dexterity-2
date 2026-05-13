<!-- Last updated: 2026-05-13 -->

# Node Editors

Editor scripts for both node families. This folder is mapped into `Dexterity.Editor` via `Dexterity.Editor.asmref`.

## Files

| File | Targets | Purpose |
|---|---|---|
| `BaseStateNodeEditor.cs` | all `BaseStateNode` subclasses | Base inspector. Owns the shared chrome: initial-state picker, delays, override state, debug section, preview-state dropdown for edit-mode modifier preview, modifiers list, multi-target handling. |
| `FieldNodeEditor.cs` | `FieldNode` | Gates/fields tab, step-tree viewer (`StepListView`), output-field debug. |
| `NodeReferenceEditor.cs` / `NodeReferenceEditorWindow.cs` | `NodeReference` | Gate / step-tree authoring on the shared asset. |
| `BindingEnumNodeEditor.cs` | `BindingEnumNode` | Enum binding picker. |
| `SimpleEnumNodeEditor.cs` | `SimpleEnumNode` | Manual state list. |
| `StateProxyNodeEditor.cs` | `StateProxyNode` | Source-node → state-name remapping list. |
| **`HierarchyNodeEditor.cs`** | `HierarchyNode` | New. Inspector shows aggregated state banner, descendant provider tree with per-leaf 3-state override pill, "Open Hierarchy Graph" button. |
| **`HierarchyGraphWindow.cs`** | (EditorWindow) | New. Codecierge-UML-style interactive graph of a HierarchyNode's tree: left-to-right layout, shoulder edges, override pills, ghost boxes for cross-referenced nodes. |
| **`HierarchyEditorPreviewDriver.cs`** | (static, `[InitializeOnLoadMethod]`) | New. Single global handler for `HierarchyPreviewOverrides.onChanged`. Walks every `HierarchyNode` in the scene, diffs against a per-node "rendered state" cache, and queues serialized Modifier transitions via `EditorTransitions`. |

## HierarchyGraphWindow

Open from menu (`Tools/Dexterity/Hierarchy Graph`) or from a HierarchyNode inspector ("Open Hierarchy Graph" button). Auto-follows `Selection` unless locked.

### Layout

- **Left-to-right tree.** Root on the left, leaves cascading right.
- **Subtree-height recursion** centers each parent on the vertical extent of its descendants.
- **Shoulder edges** (Manhattan) — three orthogonal segments via a midpoint between source-right and target-left.

### Box types

- **HierarchyNode** (root): blue.
- **HierarchyAggregator**: amber.
- **HierarchyStateProvider** (leaf): green, with a 3-state override pill (`—` / `ON` / `OFF`) in the bottom-right.
- **External ghost**: dim fill, orange border. Placed one column past the deepest leaf, vertically centered on its referrers' average y. Created when a `NodeStateProvider` in the tree targets a different `HierarchyNode`. Click the ghost to open the target's graph **in a new window**.

### Event-handling order in `DrawBox`

Pill click is checked **before** box selection, so clicking the pill never fires selection. Box-selection's hit test also excludes the pill rect for safety.

### Multiple windows

`s_openCount` tracks open instances. `HierarchyPreviewOverrides.ClearAll()` runs only when the last window closes — opening a satellite window via a ghost click doesn't wipe the state another window is showing.

## HierarchyEditorPreviewDriver

Why it exists: `EditorTransitions.TransitionAsync` owns the global `Database` singleton with a `using` scope. Two concurrent calls race over Database create/destroy. Previously every graph window started its own transition coroutine, which deadlocked or ate state when multiple nodes were affected by a single override change.

### Design

- Subscribes to `HierarchyPreviewOverrides.onChanged` once at editor load via `[InitializeOnLoadMethod]`.
- On every change: scans every `HierarchyNode` in the scene, evaluates each via `EvaluateTreeEditor()`, diffs against `s_renderedState` cache.
- Affected nodes' modifiers are queued in `s_pending` keyed by owner — coalesced (only the latest target state survives per owner).
- A single coroutine pumps the queue, running one `EditorTransitions.TransitionAsync` at a time. On completion, dequeues the next.

### Consequences

- Preview works for **every** HierarchyNode in the scene whenever overrides change — even nodes whose graph window isn't open.
- Cross-node refs (`NodeStateProvider`) automatically cascade: change Node A → Node A repaints → Node B (depending on A) reevaluates → Node B's modifiers also queue.
- `kPreviewSpeed = 6f` (faster than the normal 1×) keeps the perceived latency low even when multiple transitions chain.

### Graph window decoupling

After this refactor, `HierarchyGraphWindow` no longer owns transition state. It just calls `Repaint()` when overrides or provider events fire. Subscriptions remain so the visual graph updates promptly; the driver handles modifiers.

## Patterns to follow

- **Static editor state belongs in the driver, not the window.** If two windows can affect the same scene element, the driver is the right home (so closing one window doesn't strand the other).
- **The override registry is the single source of truth for edit-time state.** Don't add parallel mechanisms (no `editorPreviewActive`-style per-component bools — that was a previous design that we removed).
- **`HierarchyUtils.CollectOrderedDirectProviders` is the canonical tree walk.** Use it from any editor tool that needs to enumerate a node's direct children — it honors aggregator boundaries correctly.

## See also

- `../Hierarchy/CLAUDE.md` — runtime architecture the editor wraps.
- `../Hierarchy/HierarchyPreviewOverrides.cs` — the override registry.
- `../../../README.md` — designer-facing introduction.
- `.claude/guides/systems/presentation/dexterity-hierarchy.md` — UI-focused how-to for designers.
