<!-- Last updated: 2026-05-17 (Phase 1 redesign — anonymous providers) -->

# GraphProviders — Built-in leaf provider catalogue

Each file here is a concrete `GraphStateProvider` subclass that ports an existing `BaseField` to the GraphNode system. Add one to the same GameObject as your `GraphNode`, then wire its `outputs` list to feed a state-input port on the Out node (or to an aggregator).

Each provider has:
- A `List<DexterityEdge> outputs` (inherited from base) — where its bool output is fed.
- Subclass-specific input fields (raycast tag, binding target, target node, etc.).
- A `ComputeIsActive()` override that returns the bool this provider currently contributes.

Providers are **anonymous** — no state name on the provider. The state name is determined by which port the output edge feeds.

## Catalogue

| Provider | Ports | When it's active | Edit-time |
|---|---|---|---|
| `UIHoverProvider` | `UIHoverField` | Unity EventSystem pointer is hovering this UI element (`IPointerEnterHandler`/`IPointerExitHandler`). | inactive (no pointer events fire) |
| `UIPressProvider` | `UIPressField` | Unity EventSystem pointer is pressed on this UI element (`IPointerDownHandler`/`IPointerUpHandler`). | inactive |
| `RaycastHoverProvider` | `RaycastHoverField` | Any registered `IRaycastController` with the configured tag is hovering this collider. | inactive |
| `RaycastPressProvider` | `RaycastPressField` | Any registered `IRaycastController` with the configured tag is pressing this collider. Has `stayPressedOutOfBounds` option. | inactive |
| `BindingProvider` | `BindingField` | A reflection-bound boolean property/method on any `UnityEngine.Object` evaluates to true (or false, with `negate`). | inactive until binding initializes (runtime) |
| `EnumProvider` | `EnumField` | A referenced `BindingEnumNode`'s current enum case equals the configured target case. | inactive (depends on binding initialization) |
| `NodeStateProvider` | `NodeStateField` | A referenced `BaseStateNode` is currently in the configured target state. **Cross-node dependency bridge.** | active iff target is a `GraphNode` and its `EvaluateTreeEditor()` matches the target state |
| `ConstantProvider` | `ConstantField` | Always-on or always-off, based on a serialized `active` bool. | honors `active` directly |

## Implementation patterns

**Event-driven providers** (UIHover/Press, NodeStateProvider): override `OnEnable`/`OnDisable` to subscribe/unsubscribe, internal state is updated by handler callbacks calling `MarkChanged()`.

**Polling providers** (Binding, Enum, Raycast): an internal `Update()` method compares current `ComputeIsActive()` against a cached `_lastActive` and fires `MarkChanged()` on diff. Necessary when the underlying source doesn't expose a change event.

**Constant providers**: trivial — `ComputeIsActive()` returns the serialized flag directly. Useful as a terminal fallback at the end of an aggregator's inputs.

## Note on `NodeStateProvider`

Edit-time behavior is special. The base `IsActive` getter calls `ComputeIsActive()` whose implementation has two branches:
- Runtime: compares `targetNode.GetActiveState()` (int) against a cached state ID.
- Edit-time: compares `targetNode.EvaluateTreeEditor()` (string) against `targetState` — works for `GraphNode` targets without `Database`.

This means cross-node dependencies "just work" at edit time: toggle a provider override in Node A, Node B's `NodeStateProvider`s that target Node A see the change immediately via the driver.

## Adding a new provider

1. Create a `GraphStateProvider` subclass.
2. Add an `[AddComponentMenu("Dexterity/Graph/Providers/...")]` so designers can find it.
3. Override `ComputeIsActive()`. Be defensive at edit time — if your inputs aren't wired (raycast controllers, UI events, runtime services), return `false`. The override registry covers simulation, so you don't need to invent edit-mode mocks.
4. If your input has an event, subscribe in `OnEnable` and call `MarkChanged()` from the handler. If polled, add an `Update()` with a diff check.

## See also

- `../../Node/Graph/CLAUDE.md` — GraphNode runtime architecture (where these plug in).
- `../../Node/Graph/GraphStateProvider.cs` — base class.
- `../Fields/` — original `BaseField` implementations these ports mirror.
