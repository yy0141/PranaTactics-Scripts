using System;

namespace PranaTactics.Settings
{
    /// <summary>
    /// 入力の検証、依存項目の更新、変更の保存を仲介します。
    /// </summary>
    public sealed class SettingsPresenter
    {
        private readonly Func<UserSettings> _getSettings;

        private readonly Func<bool> _save;

        private readonly Action _apply;

        public SettingsCatalog Catalog
        {
            get;
        }

        public string Error
        {
            get; private set;
        }

        public event Action ValuesChanged;

        public SettingsPresenter(
            SettingsCatalog catalog,
            Func<UserSettings> getSettings,
            Func<bool> save,
            Action apply)
        {
            Catalog = catalog;
            _getSettings = getSettings;
            _save = save;
            _apply = apply;
        }

        public string Read(string id)
        {
            return Catalog.Get(id).Read(_getSettings());
        }

        public bool IsEnabled(SettingItemDefinition item)
        {
            return item.EnabledWhen == null ||
                Read(item.EnabledWhen.SettingId) == item.EnabledWhen.EqualsValue;
        }

        public bool Change(SettingItemDefinition item, string value, bool save)
        {
            if (!IsEnabled(item) || !Catalog.Get(item.SettingId).TryWrite(_getSettings(), value,
                    item.Control == "numberSlider" ? item.Min : null, item.Control == "numberSlider" ? item.Max : null))
            {
                Error = "入力値を確認してください。";
                ValuesChanged?.Invoke();

                return false;
            }

            _apply();
            bool success = !save || _save();

            Error = success ? null : "設定を保存できませんでした。";
            ValuesChanged?.Invoke();

            return success;
        }

        public bool Save()
        {
            bool success = _save();

            Error = success ? null : "設定を保存できませんでした。";
            ValuesChanged?.Invoke();

            return success;
        }
    }
}
