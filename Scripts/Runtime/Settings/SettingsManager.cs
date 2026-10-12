using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace PranaTactics.Settings
{
    /// <summary>
    /// 既存コードへ設定サービスを提供する互換窓口です。
    /// </summary>
    public static class SettingsManager
    {
        private const float k_KeyboardSensitivityDisplayScale = 100.0f;

        private const string k_DisplayNumberFormat = "0.###";

        private static UserSettings s_current;

        private static ISettingsRepository s_repository;

        private static bool s_canSave = true;

        private static readonly LanguageSettingsApplier s_languageApplier = new LanguageSettingsApplier();

        public static event Action SettingsChanged;

        public static string LastError
        {
            get; private set;
        }

        public static UserSettings Current
        {
            get
            {
                EnsureLoaded();
                if (GameBuildSettings.IsSoloOnly)
                {
                    s_current.General.IsSoloPlay = true;
                }

                return s_current;
            }
        }

        /// <summary>
        /// 設定を読み込み、破損時は元ファイルを保持します。
        /// </summary>
        public static void Load()
        {
            s_repository = new JsonSettingsRepository(
                Path.Combine(
                Application.persistentDataPath,
                "settings.json"));

            try
            {
                s_current = s_repository.Load();
                s_canSave = true;
                LastError = null;
            }
            catch (Exception exception)
            {
                s_current = new UserSettings();
                s_canSave = false;
                LastError = "設定ファイルを読み込めません。元ファイルを保護するため保存を停止しています。";
                Debug.LogError(LastError + "\n" + exception);
            }

            if (!Application.isEditor && GameBuildSettings.StartInJapanese)
            {
                s_current.General.LanguageCode = "ja";
            }

            ApplyAppearanceLayout();
            NotifyChanged();
        }

        /// <summary>
        /// 現在の設定を保存します。
        /// </summary>
        public static void Save()
        {
            TrySave();
        }

        /// <summary>
        /// 保存結果を返し、失敗は画面へ伝えます。
        /// </summary>
        public static bool TrySave()
        {
            EnsureLoaded();
            if (GameBuildSettings.IsSoloOnly)
            {
                s_current.General.IsSoloPlay = true;
            }

            if (!s_canSave)
            {
                return false;
            }

            try
            {
                s_repository.Save(s_current);
                LastError = null;
                NotifyChanged();

                return true;
            }
            catch (Exception exception)
            {
                LastError = "設定を保存できませんでした。";
                Debug.LogError(LastError + "\n" + exception);

                return false;
            }
        }

        /// <summary>
        /// 保存せずに設定変更をゲームへ通知します。
        /// </summary>
        public static void NotifyChanged()
        {
            s_languageApplier.Apply(Current.General.LanguageCode);
            SettingsChanged?.Invoke();
        }

        /// <summary>
        /// チュートリアル履歴を保持して設定を初期化します。
        /// </summary>
        public static void ResetToDefaults()
        {
            Current.ResetToDefaults();
            ApplyAppearanceLayout();
            Save();
        }

        /// <summary>
        /// 配色の登録数を画面定義JSONから読み込みます。
        /// </summary>
        private static void ApplyAppearanceLayout()
        {
            TextAsset asset = Resources.Load<TextAsset>("Settings/settings-view");
            if (asset == null)
            {
                return;
            }

            try
            {
                SettingsViewDefinition definition = JsonConvert.DeserializeObject<SettingsViewDefinition>(asset.text);
                definition.Validate(new SettingsCatalog());
                definition.ApplyAppearanceLayout(s_current);
            }
            catch (Exception exception)
            {
                Debug.LogError("配色の表示定義を読み込めませんでした。\n" + exception);
            }
        }

        /// <summary>
        /// 設定を初回アクセス時に読み込みます。
        /// </summary>
        private static void EnsureLoaded()
        {
            if (s_current == null)
            {
                Load();
            }
        }

        /// <summary>
        /// 既存の数値入力部品へ表示値を返します。
        /// </summary>
        public static string GetDisplayValue(
            SettingType settingType)
        {
            EnsureLoaded();

            float value;

            switch (settingType)
            {
                case SettingType.MinimapZoom:
                    value = s_current.MinimapZoom;
                    break;

                case SettingType.MinimapUiScale:
                    value = s_current.MinimapUiScale;
                    break;

                case SettingType.MouseSensitivity:
                    value =
                        s_current.MouseSensitivity;

                    break;

                case SettingType.KeyboardSensitivity:
                    value =
                        s_current.KeyboardSensitivity /
                        k_KeyboardSensitivityDisplayScale;

                    break;

                case SettingType.BgmVolume:
                    value =
                        s_current.BgmVolume;

                    break;

                case SettingType.SeVolume:
                    value =
                        s_current.SeVolume;

                    break;

                default:
                    Debug.LogWarning(
                        $"{nameof(SettingsManager)}: " +
                        "表示に対応していない設定項目です。" +
                        $" SettingType={settingType}");

                    return string.Empty;
            }

            return value.ToString(
                k_DisplayNumberFormat,
                CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 入力された文字列を指定された設定項目へ適用し、
        /// 設定ファイルへ保存します。
        /// </summary>
        /// <param name="settingType">
        /// 変更する設定項目です。
        /// </param>
        /// <param name="value">
        /// 設定する値を表す文字列です。
        /// </param>
        public static void SetValue(
            SettingType settingType,
            string value)
        {
            TrySetValue(
                settingType,
                value);
        }

        /// <summary>
        /// 入力された文字列を指定された設定項目へ適用します。
        /// </summary>
        /// <param name="settingType">
        /// 変更する設定項目です。
        /// </param>
        /// <param name="value">
        /// 設定する値を表す文字列です。
        /// </param>
        /// <returns>
        /// 値を正常に設定して保存できた場合は<c>true</c>です。
        /// </returns>
        public static bool TrySetValue(
            SettingType settingType,
            string value)
        {
            EnsureLoaded();

            if (!TryParseFloat(
                    value,
                    out float parsedValue))
            {
                Debug.LogWarning(
                    $"{nameof(SettingsManager)}: " +
                    "設定値を数値として解析できません。" +
                    $" SettingType={settingType}, Value={value}");

                return false;
            }

            switch (settingType)
            {
                case SettingType.MinimapZoom:
                    s_current.MinimapZoom = parsedValue;
                    break;

                case SettingType.MinimapUiScale:
                    s_current.MinimapUiScale = parsedValue;
                    break;

                case SettingType.MouseSensitivity:
                    s_current.MouseSensitivity =
                        parsedValue;

                    break;

                case SettingType.KeyboardSensitivity:
                    s_current.KeyboardSensitivity =
                        parsedValue *
                        k_KeyboardSensitivityDisplayScale;

                    break;

                case SettingType.BgmVolume:
                    s_current.BgmVolume =
                        parsedValue;

                    break;

                case SettingType.SeVolume:
                    s_current.SeVolume =
                        parsedValue;

                    break;

                default:
                    Debug.LogWarning(
                        $"{nameof(SettingsManager)}: " +
                        "変更に対応していない設定項目です。" +
                        $" SettingType={settingType}");

                    return false;
            }

            Save();

            return true;
        }

        /// <summary>
        /// 文字列を数値として解析します。
        /// </summary>
        private static bool TryParseFloat(
            string value,
            out float parsedValue)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                parsedValue = 0.0f;

                return false;
            }

            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out parsedValue))
            {
                return true;
            }

            return float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out parsedValue);
        }

    }
}
