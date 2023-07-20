namespace OneHamsa.Dexterity.Utilities
{
    public interface IPrefabEditingHooks
    {
        void OnPrefabStageClosing()
        {
        }

        void OnPrefabSaving()
        {
        }

        void OnPrefabSaved()
        {
        }

        void OnPrefabStageOpened()
        {
        }

        void OnSelected()
        {
        }

        void OnDeselected()
        {
        }
    }
}