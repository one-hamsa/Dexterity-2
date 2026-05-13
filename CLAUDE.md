<!-- Last updated: 2026-05-13 -->

# Dexterity 2.0 — Agent Index

Declarative state-machine library for animation/visual states. Components declare states (Default, Hover, Pressed, Disabled, Hidden, …) and **Modifiers** translate each state into a visual property change (color, transform, alpha, …). A central **Manager** transitions between states.

This file is the entry point for agents working in Dexterity. Designers should read `README.md` instead.

## Two node families

Dexterity has two ways to compute a node's current state. Pick the one that matches the problem:

| | **FieldNode** (classic) | **HierarchyNode** (new) |
|---|---|---|
| **State source** | `Gate`s on the node wrap `BaseField`s; a `StateFunction` step tree maps field values → state | MonoBehaviour `HierarchyStateProvider`s spread through the transform subtree, combined by `HierarchyAggregator`s |
| **Authoring** | One central inspector on the node — gates + step tree | One component per signal, dropped anywhere under the node — drop a `HoverProvider` on a child to add hover |
| **Evaluation** | Field bitmask + DFS step tree | Walk transform tree, first active provider wins (with rule-based combining via aggregators) |
| **Edit-time** | Only runs inside the narrow `EditorTransitions` preview path | Always evaluable (pure string-in/string-out); designer-friendly graph window with override pills |
| **State names** | Auto-discovered from the StateFunction | Free text per provider; aggregator rules and `initialState` define the full set |
| **Built-in inputs** | `BaseField` subclasses: hover, press, raycast, binding, enum, node-state, constant, parent, children, AND, OR, … | `HierarchyStateProvider` subclasses: ports of the same hover/press/raycast/binding/enum/node-state/constant inputs |
| **Reuse pattern** | Wire same `BaseField` instance into multiple gates | One provider per signal; aggregator **rules query by state name**, so the same state can be referenced many places |
| **Best for** | Complex logic-driven nodes with reusable `NodeReference` assets | UI components with prefab-droppable inputs and live edit-mode previewing |

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
    Hierarchy/                         ← NEW   see Hierarchy/CLAUDE.md
      IHierarchyStateProvider.cs
      IHierarchyContainer.cs
      HierarchyUtils.cs
      HierarchyStateProvider.cs        — leaf provider base
      HierarchyAggregator.cs           — branch / composite base
      HierarchyNode.cs                 — the Node
      HierarchyPreviewOverrides.cs     — global IsActive override registry
      Aggregators/                     ← NEW   see Aggregators/CLAUDE.md
        FirstMatchAggregator.cs
        AllOfAggregator.cs
    Editor/                            ← see Editor/CLAUDE.md
      ... (existing FieldNode editors)
      HierarchyNodeEditor.cs           ← NEW
      HierarchyGraphWindow.cs          ← NEW   graph window with override pills
      HierarchyEditorPreviewDriver.cs  ← NEW   global edit-time transition driver
  Builtins/
    Fields/                            — classic BaseField subclasses
    HierarchyProviders/                ← NEW   see HierarchyProviders/CLAUDE.md
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

- **Modifiers don't care which node family they bind to.** A `ColorModifier` under a `HierarchyNode` works the same as under a `FieldNode` — both walk up the hierarchy via `Modifier.TryFindNode()`.
- **When the user opens the Hierarchy Graph window**, the override registry takes precedence over real provider logic. Closing the last graph window clears overrides.
- **HierarchyNode state evaluation is pure string-based** — it doesn't use `Database` IDs internally. That's why it works in edit mode without infrastructure setup.

## See also

- [README.md](README.md) — user-facing introduction (Dexterity as a whole).
- [Scripts/Node/Hierarchy/CLAUDE.md](Scripts/Node/Hierarchy/CLAUDE.md) — HierarchyNode runtime architecture.
- [Scripts/Node/Editor/CLAUDE.md](Scripts/Node/Editor/CLAUDE.md) — editor tooling: inspector, graph window, preview driver.
- [Scripts/Builtins/HierarchyProviders/CLAUDE.md](Scripts/Builtins/HierarchyProviders/CLAUDE.md) — built-in provider catalogue.
- `.claude/guides/systems/presentation/dexterity-hierarchy.md` — designer-facing how-to with real UI scenarios.
