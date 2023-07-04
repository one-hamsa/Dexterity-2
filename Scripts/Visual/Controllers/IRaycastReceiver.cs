namespace OneHamsa.Dexterity
{
    public interface IRaycastReceiver
    {
        void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent);
        void ClearHit(IRaycastController controller);
        IRaycastReceiver Resolve() => this;
    }
}
