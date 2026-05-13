<!-- Last updated: 2026-05-13 -->

# HierarchyProviders — Built-in leaf provider catalogue

Each file here is a concrete `HierarchyStateProvider` subclass that ports an existing `BaseField` to the HierarchyNode system. Drop one on any GameObject under a `HierarchyNode` (or under an intermediate `HierarchyAggregator`) and it self-registers on enable.

Each provider has:
- A serialized `state` string (free text — the state name it reports when active).
- Subclass-specific input fields (raycast tag, binding target, target node, etc.).
- A `ComputeIsActive()` override that returns whether the provider is currently contributing its state.

## Catalogue

| Provider | Ports | When it's active | Edit-time |
|---|---|---|---|
| `UIHoverProvider` | `UIHoverField` | Unity EventSystem pointer is hovering this UI element (`IPointerEnterHandler`/`IPointerExitHandler`). | inactive (no pointer events fire) |
| `UIPressProvider` | `UIPressField` | Unity EventSystem pointer is pressed on this UI element (`IPointerDownHandler`/`IPointerUpHandler`). | inactive |
| `RaycastHoverProvider` | `RaycastHoverField` | Any registered `IRaycastController` with the configured tag is hovering this collider. | inactive |
| `RaycastPressProvider` | `RaycastPressField` | Any registered `IRaycastController` with the configured tag is pressing this collider. Has `stayPressedOutOfBounds` option. | inactive |
| `BindingProvider` | `BindingField` | A reflection-bound boolean property/method on any `UnityEngine.Object` evaluates to true (or false, with `negate`). | inactive until binding initializes (runtime) |
| `EnumProvider` | `EnumField` | A referenced `BindingEnumNode`'s current enum case equals the configured target case. | inactive (depends on binding initialization) |
| `NodeStateProvider` | `NodeStateField` | A referenced `BaseStateNode` is currently in the configured target state. **Cross-node dependency bridge.** | active iff target is a `HierarchyNode` and its `EvaluateTreeEditor()` matches the target state |
| `ConstantProvider` | `ConstantField` | Always-on or always-off, based on a serialized `active` bool. | honors `active` directly |

## Implementation patterns

**Event-driven providers** (UIHover/Press, NodeStateProvider): override `OnEnable`/`OnDisable` to subscribe/unsubscribe, internal state is updated by handler callbacks calling `MarkChanged()`.

**Polling providers** (Binding, Enum, Raycast): an internal `Update()` method compares current `ComputeIsActive()` against a cached `_lastActive` and fires `MarkChanged()` on diff. Necessary when the underlying source doesn't expose a change event.

**Constant providers**: trivial — `ComputeIsActive()` returns the serialized flag directly. Useful as terminal fallback at the end of a sibling list.

## Note on `NodeStateProvider`

Edit-time behavior is special. The base `IsActive` getter calls `ComputeIsActive()` whose implementation has two branches:
- Runtime: compares `targetNode.GetActiveState()` (int) against a cached state ID.
- Edit-time: compares `targetNode.EvaluateTreeEditor()` (string) against `targetState` — works for `HierarchyNode` targets without `Database`.

This means cross-node dependencies "just work" in the graph window: toggle a provider override in Node A, Node B's `NodeStateProvider`s that target Node A see the change immediately.

## Adding a new provider

1. Create a `HierarchyStateProvider` subclass.
2. Add an `[AddComponentMenu("Dexterity/Hierarchy/Providers/...")]` so designers can find it.
3. Override `ComputeIsActive()`. Be defensive at edit time — if your inputs aren't wired (raycast controllers, UI events, runtime services), return `false`. The override registry covers simulation, so you don't need to invent edit-mode mocks.
4. If your input has an event, subscribe in `OnEnable` and call `MarkChanged()` from the handler. If polled, add an `Update()` with a diff check.

## See also

- `../../Node/Hierarchy/CLAUDE.md` — HierarchyNode runtime architecture (where these plug in).
- `../../Node/Hierarchy/HierarchyStateProvider.cs` — base class.
- `../Fields/` — original `BaseField` implementations these ports mirror.
