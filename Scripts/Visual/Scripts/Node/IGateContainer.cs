namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    public interface IGateContainer
    {
        StateFunctionGraph stateFunctionAsset { get; }
        Node node { get; }

        void AddGate(Gate gate);
        void RemoveGate(Gate gate);
        void NotifyGatesUpdate();
        Gate GetGateAtIndex(int i);
    }
}
