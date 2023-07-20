namespace OneHamsa.Dexterity.Utilities
{
    public interface ISceneEditingHooks
    {
        void OnSceneClosing(bool duringBuild)
        {
        }

        void OnSceneSaving(bool duringBuild)
        {
        }

        void OnSceneSaved(bool duringBuild)
        {
        }

        void OnSceneOpened(bool duringBuild)
        {
        }
    }
}