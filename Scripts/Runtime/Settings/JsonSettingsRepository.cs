using System.IO;

namespace PranaTactics.Settings
{
    /// <summary>
    /// 設定ファイルの移行と一時ファイルを用いた置き換えを担当します。
    /// </summary>
    public sealed class JsonSettingsRepository : ISettingsRepository
    {
        private readonly string _path;

        public JsonSettingsRepository(string path)
        {
            _path = path;
        }

        /// <summary>
        /// 保存データを読み込み、旧形式はバックアップ後に移行します。
        /// </summary>
        public UserSettings Load()
        {
            if (!File.Exists(_path))
            {
                return new UserSettings();
            }

            UserSettings settings = SettingsJson.Deserialize(File.ReadAllText(_path), out bool migrated);

            if (migrated)
            {
                if (!File.Exists(_path + ".v1.bak"))
                {
                    File.Copy(_path, _path + ".v1.bak", false);
                }

                Save(settings);
            }

            return settings;
        }

        /// <summary>
        /// 書き込みが完了したファイルで保存先を置き換えます。
        /// </summary>
        public void Save(UserSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string temporaryPath = _path + ".tmp";

            File.WriteAllText(temporaryPath, SettingsJson.Serialize(settings));

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, _path + ".bak");
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
    }
}
