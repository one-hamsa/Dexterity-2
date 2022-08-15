namespace OneHamsa.Dexterity.Visual
{
    using System.Collections.Generic;
    using Gate = NodeReference.Gate;

    public interface IGateContainer
    {
        IEnumerable<string> GetStateNames();
        IEnumerable<string> GetFieldNames();
        Node node { get; }

        void AddGate(Gate gate);
        void RemoveGate(Gate gate);
        void NotifyGatesUpdate();
        int GetGateCount();
        Gate GetGateAtIndex(int i);
    }
}
