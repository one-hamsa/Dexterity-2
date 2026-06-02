# Dexterity 2.0 — pointer

Declarative state-machine library for animation/visual states. Two node families (`FieldNode` classic, `GraphNode` new). Both share `Modifier`s, transitions, `Database`, `Manager`.

**Before editing any Dexterity source, read `ENGINEERING.md` in this folder.** It is the single source of truth for:

- FieldNode vs GraphNode (when to use which).
- GraphNode runtime architecture (edges, topo evaluation, lifecycle, override semantics, preview groups).
- Editor tooling (graph window, preview driver, edge drawer) and the patterns it enforces.
- Built-in operators and sources.

## Load-bearing rules (apply even before you've opened ENGINEERING.md)

- **Edge writes go through `SerializedObject` + `ApplyModifiedProperties`.** Direct reflection writes bypass Unity's prefab-override tracking — spike-verified.
- **GraphNode sources live on the host GameObject** with `HideFlags.HideInInspector`. Authoring happens in the Dexterity Graph window, not the Inspector.
- **`GetActiveState()` is priority-respecting; `GetRawInput(stateId)` is not.** Use the latter when a listener should react to a masked input (e.g. press-under-disabled).

## See also

- `README.md` — designer-facing introduction.
- `.claude/guides/systems/presentation/dexterity-graph.md` — designer's GraphNode guide.
