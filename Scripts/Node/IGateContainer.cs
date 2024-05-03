namespace OneHamsa.Dexterity
{
    using Gate = NodeReference.Gate;

    public interface IGateContainer
    {
        FieldNode node { get; }

        void AddGate(Gate gate);
        void RemoveGate(Gate gate);
        void NotifyGatesUpdate();
        int GetGateCount();
        Gate GetGateAtIndex(int i);
    }
}
