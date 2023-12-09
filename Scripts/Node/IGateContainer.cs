namespace OneHamsa.Dexterity
{
    using System.Collections.Generic;
    using Gate = NodeReference.Gate;

    public interface IGateContainer
    {
        IEnumerable<FieldDefinition> GetInternalFieldDefinitions();
        
        /// <summary>
        /// Returns a list of field names that are allowed to be used in the context of this node,
        /// or null if all fields are allowed.
        /// </summary>
        /// <returns></returns>
        HashSet<string> GetWhitelistedFieldNames() => null;
        FieldNode node { get; }

        void AddGate(Gate gate);
        void RemoveGate(Gate gate);
        void NotifyGatesUpdate();
        int GetGateCount();
        Gate GetGateAtIndex(int i);
    }
}
