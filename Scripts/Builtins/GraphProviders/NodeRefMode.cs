namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// How a state-sampling provider chooses which target node(s) to sample.
    /// Mirrors the parent/children gate semantics that FieldNode had with classic
    /// gates, so a single provider can ask "is the surrounding context in state X?"
    /// without having to wire <c>targetNode</c> explicitly.
    /// </summary>
    public enum NodeRefMode
    {
        /// <summary>Use the explicit <c>targetNode</c> field.</summary>
        Reference,
        /// <summary>Walk up from the host node and pick the first BaseStateNode ancestor.</summary>
        FirstParent,
        /// <summary>Pick all direct-descendant BaseStateNodes (stops at nested nodes' boundaries). True if ANY matches.</summary>
        AnyChild,
        /// <summary>Same set as <see cref="AnyChild"/>. True only if ALL match.</summary>
        AllChildren,
    }
}
