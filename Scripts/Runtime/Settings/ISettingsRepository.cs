namespace PranaTactics.Settings
{
    public interface ISettingsRepository
    {
        UserSettings Load();

        void Save(UserSettings settings);
    }
}
