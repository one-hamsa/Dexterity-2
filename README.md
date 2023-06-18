# Dexterity 2.0
[https://omerpp.itch.io/dexterity-demo](https://omerpp.itch.io/dexterity-demo)

Dexterity is a declarative visual library that takes the complexity out of managing your animation states. 

It utilizes a system of States (like Hidden or Visible), Modifiers (e.g., hidden translates to 0% opacity, hover implies x1.1 scale), and Transitions between states, doing away with the need for extensive manual tweaking and maintaining of your animations.

## Getting Started
Creating a component with Dexterity requires you to define its visual states. 

For instance, a button may have Default, Disabled, Hover, and Pressed states, while another might not need the Disabled state, and yet another may include a Hidden state. At any given moment, a component can exist in only one state. Dexterity seamlessly manages the transitions, allowing you to focus on the final design.

To create a component possessing unique states, Dexterity uses Nodes. Nodes are available in various flavors but operate on the same principle: every Modifier under a given node shares the same state list and responds identically to state changes.

Dexterity is also equipped with full editor support, and edit-time transitions via EditorCoroutines.

# Modifiers
Dexterity comes packed with a variety of most useful modifiers:

[list]

## Modifier Bindings
Modifiers allow binding their state properties to global values, like a specific color that is consistently used throughout your project. 
Simply right-click a property in the editor and choose from the binding menu. The bindings are stored in Dexterity Settings and can be shared among Modifiers of the same type.

# Transitions
Dexterity offers three transition strategies:

1. **Simple** - uses Regular Lerp, Continuous Lerp or Discrete (no interpolation) to move between states.
2. **Velocity** - uses SmoothDamp for transitions.
3. **Matrix** - detailed in the Advanced section.

# Nodes
Dexterity provides several types of State Nodes:

## Simple Enum Node
The most basic node, manually configured with a list of states, and provides a `SetState(string state)` method for code-based state control.

## Object Source Enum Node
Uses Reflection to read an enum from a Unity Object, automatically reflecting code changes to a visual state.

## Field Node
The Field Node is the most complex variant in Dexterity. It introduces an additional layer termed "fields," which encapsulate the logical state of a component, as opposed to its visual state. These fields can coexist, meaning they're not mutually exclusive. A State Function, which maps combinations of fields to a specific state, is used to decide the final State.

Field Nodes make use of Gates. Gates serve as junctions, combining a list of sources into an Output Field. To illustrate this, if you have a component that should become visible when its parent is hovered over, you'd add a Parent Field to the node’s gates. The Parent Field, in turn, produces a 'true' output to the visible field whenever the parent is being hovered over.

Field Nodes may initially appear complex due to their feature-rich nature, but they provide a robust means to manage your component's visuals. The API of Field Nodes outmatches other types due to its fine-grained control over state (i.e. leveraging OutputFields), and additional customization options like Internal Field Definitions. This makes Field Nodes a versatile tool for handling the diverse and dynamic visual states of your components.

# Field Types
Dexterity includes several built-in field types for Field Nodes:

[list]

# Dexterity Settings
## Field Definitions
TBD

# Advanced Features

## State Functions
State Functions are sequences of instructions that navigate the fields of a Field Node to determine its final State. They facilitate multiple actions:
1. **If** - Tests the value of a specific field. By left-clicking, you can switch the comparison (==) operator to an inequality (!=) operator.
2. **Go To** - Makes a conclusive decision on a state. This action shortcircuits further instructions and is added by left-clicking the "If" button.
3. **Run** - Executes another State Function, allowing references to other assets. Use it by right-clicking the "If" or "Go To" button.

## Node References
Node References are configuration assets that empower you to share definitions across various components, including state functions and gates.

## Matrix Definitions
Matrix Definitions are assets that enable the most detailed and robust transitions. They allow you to specify individual animation curves, timings, and delays when transitioning between visual states, offering granular control over the behavior of your animations.

# Debugging
The Dexterity editor is rich with debugging features. By selecting a node, you can view all its associated modifiers. In play time, the selection of a node also reveals its dependencies, enabling you to troubleshoot any configuration issues.

Additionally, the Dexterity Manager component is equipped with an editor that provides comprehensive details on the current status of all active nodes and fields. This feature, coupled with other useful debugging information, is an advanced powerful tool to diagnose and resolve issues.

# API
## `FieldNode.OutputField`s
TBD
## Listening to state changes
tbd
## Overrides
TBD
## Modifiers
TBD `ForceTransitionUpdate`
## `RaycastController` class and `IRaycastController` interface
TBD

# Extending Dexterity
## Modifiers
Creating new Modifiers in Dexterity is straightforward. Extend `Modifier` or `ComponentModifier<Component>` (if your modifier changes a specific component), and implement your property data and `Refresh()` (transition) logic. Dexterity Editor takes care of the heavy-lifting.

## Field Types and `BaseField`
Creating a new field type is fairly easy. Upon implementation (inheriting from `BaseField`), it will automatically appear in the Dexterity Editor with all its custom options.
Consult the BaseField documentation for guidance.

## Transitions
Implementing Transition Strategies is quite straightforward and will appear automatically when you inherit from BaseStrategy. 
Copy an existing strategy as a baseline and modify it to your needs.

# Examples
- [Online Demo](https://omerpp.itch.io/dexterity-demo)
- Samples directory in source code

# Architecture / Behind The Scenes
The Dexterity Manager efficiently manages a dependency graph, updating only the relevant components as needed. Thus, if field A in node X is dependent on field B in node Y, they would form a linked cluster that updates in unison.

Dexterity is engineered from the ground up with a focus on efficiency, involving almost no allocations, and producing an extremely minimal garbage collection footprint. It only triggers updates to the visual state of objects when required, and reverts to a dormant state once transitions are completed.

One point to note is that the code leans heavily on the [SerializeReference] functionality. This dependency introduces a level of fragility when it comes to renaming types. So, if you plan on creating your own modifiers, strategies, or fields, take caution when renaming them to avoid potential issues.
