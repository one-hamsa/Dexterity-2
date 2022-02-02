# Dexterity 2.0

Dexterity is a Unity animation system that uses abstract, high-level concepts for defining transitions and behaviors between animation states.

It currently targets Unity 2020 and supports Unity 2019 with a more limited set of features.

# Concepts
## Fields & States
**Fields** are key-value items that store logical information. They are used to store a complex set of parameters about a subject. For instance, `visible` field can indicate whether the element should be shown on screen, where `hover` can indicate a mouse/controller hovering the subject.

Fields can either carry a boolean value (`visible`? yes/no) or an enum value (`attention`: low/medium/high).

**States** are strings that represent the desired animation status of a subject. While subjects can have many fields, they can only have one state at a time. Dexterity takes the field a subject holds, and translates them to a final state using user-defined transformations. 

States define priorities and disambiguate field. For instance, if a subject is not `visible`, it can immediately be given the `Hidden` state - no need to check if it's `disabled` or test its `attention` level, since its animation state should always be hidden. On the other hand, if it is visibile, has high `attention` AND being `hover`ed, maybe this should invoke a `AccentuatedHover` state.

These are just some random example to express how fields can be translated to different states. In the end of the day, when using Dexterity - users will define their own rules based on the developer/designer's decision.

## Nodes
Nodes are components that hold an animtion *state* for other components.
They have *fields* that represent their logical state, and define how to take those fields and turn them into a final *state*.

By default, everything under the hierarchy of a node will be considered a child of this node. For instance, node can be a parent of both a UI Image and a Text object - controlling their animation states as a single unit.

### Gates
The way to control what fields (and what field values) a specific node has, it has to declare a list of gates. Those gates indicate how the values of the fields are calculated.

For instance, if a button should always be visible, we can add a `Constant` gate and assign it to the `visible` field. If we want it to be hoverable (change its color whenever a mouse/controller hovers it), we can add a `UIHover` or a `RaycastHover` gate and assign it to the `hover` field. 

There are many gate types, such as a `Parent` gate (takes a field value from a parent node), `Node` gate (same as Parent but uses a reference), `UnityObject` gate (uses reflection to check a Unity Object's member value), and so forth. 
Gate types are easy to add, more on that later.

## Modifiers
Modifiers are components that specialize in perfoming transitions between animation *states*. 

Each modifier is assigned to a *node* (either automatically by hierarchy, or manually using a reference). This node will determine all the possible states this modifier can be at. The modifier will then define how each state looks like - and how would states transition between each other.

Modifiers are generally placed on the same GameObject as the component they're controlling.

For example, a `Color Modifier` can control an Image component's color. It reads the list of possible states from its parent *node*, and transitions between a `<Default>` state (white), `Hidden` state (invisible), `Hover` state (blue) and`Selected` state (red). 

### Transition Strategies
Each modifier is assigned with a transition strategy - a behavior type (along with a set of parameters) that defines how transitions between different states would look like.  

## Node References
TBD
## State Functions
TBD
# Editor

TBD

# Code APIs
## `Node.OutputField`s
TBD
## Listening to state changes
tbd
## Overrides
TBD
## Modifiers
TBD `ForceTransitionUpdate`

# Advanced Concepts
## Gate override types
TBD
## `TransitionBehaviour` class
TBD
## `Manager` class
TBD
## `RaycastController` class and `IRaycastController` interface
TBD

# Behind the Scenes
TBD

# Extending Dexterity
## `Modifier`s
TBD
## Field types and `BaseField`
TBD
## Transitions
TBD

# Examples
TBD
