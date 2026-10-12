using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PranaTactics.Settings
{
    /// <summary>
    /// 保存された言語をUnity Localizationへ適用します。
    /// </summary>
    internal sealed class LanguageSettingsApplier
    {
        private string _requestedLanguageCode;

        private bool _isWaitingForInitialization;

        /// <summary>
        /// 初期化完了を待って最新の言語選択を反映します。
        /// </summary>
        public void Apply(string languageCode)
        {
            _requestedLanguageCode = languageCode;

            if (_isWaitingForInitialization)
            {
                return;
            }

            AsyncOperationHandle<LocalizationSettings> initialization = LocalizationSettings.InitializationOperation;

            if (initialization.IsDone)
            {
                OnInitializationCompleted(initialization);

                return;
            }

            _isWaitingForInitialization = true;
            initialization.Completed += OnInitializationCompleted;
        }

        /// <summary>
        /// ローカライズの初期化結果を確認し、選択言語の変更を通知します。
        /// </summary>
        private void OnInitializationCompleted(AsyncOperationHandle<LocalizationSettings> operation)
        {
            operation.Completed -= OnInitializationCompleted;
            _isWaitingForInitialization = false;

            if (operation.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("言語設定を反映できません。ローカライズの初期化に失敗しました。");

                return;
            }

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(_requestedLanguageCode);

            if (locale == null)
            {
                Debug.LogError("選択した言語のローカライズデータがありません: " + _requestedLanguageCode);

                return;
            }

            if (LocalizationSettings.SelectedLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }
    }
}
