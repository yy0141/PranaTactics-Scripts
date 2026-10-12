using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace PranaTactics.Settings
{
    /// <summary>
    /// JSON形式の変換と旧設定データの移行を担当します。
    /// </summary>
    public static class SettingsJson
    {
        private static readonly JsonSerializerSettings s_options = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>
        /// 読み込んだ設定を新形式へ復元します。
        /// </summary>
        public static UserSettings Deserialize(string json, out bool migrated)
        {
            JObject document = JObject.Parse(json);

            migrated = document["schemaVersion"] == null;

            if (!migrated)
            {
                if (document.Value<int>("schemaVersion") != 2)
                {
                    throw new InvalidOperationException("未対応の設定ファイルバージョンです。");
                }

                UserSettings settings = document.ToObject<UserSettings>(JsonSerializer.Create(s_options));

                if (document["controls"] is JObject controls && controls["holdMovementSpeed"] != null)
                {
                    bool oldHold = controls.Value<bool>("holdMovementSpeed");

                    if (controls["holdWalk"] == null)
                    {
                        settings.Controls.HoldWalk = oldHold;
                    }

                    if (controls["holdStop"] == null)
                    {
                        settings.Controls.HoldStop = oldHold;
                    }

                    migrated = true;
                }

                settings.Validate();

                return settings;
            }

            var legacy = new UserSettings();

            legacy.MouseSensitivity = document.Value<float?>("_mouseSensitivity") ?? 1.0f;
            legacy.KeyboardSensitivity = document.Value<float?>("_keyboardSensitivity") ?? 1.0f;
            legacy.BgmVolume = document.Value<float?>("_bgmVolume") ?? document.Value<float?>("BGMVolume") ?? 1.0f;
            legacy.SeVolume = document.Value<float?>("_seVolume") ?? document.Value<float?>("SEVolume") ?? 1.0f;
            legacy.IsFullScreen = document.Value<bool?>("_isFullScreen") ?? document.Value<bool?>(
                "FullScreen") ?? true;
            legacy.HoldMovementSpeed = document.Value<bool?>("_holdMovementSpeed") ?? false;
            legacy.HasFinishedTutorial = document.Value<bool?>("_hasFinishedTutorial") ?? false;
            legacy.IsMinimapVariable = document.Value<bool?>("_isMinimapVariable") ?? false;
            legacy.MinimapZoom = document.Value<float?>("_minimapZoom") ?? 1.0f;
            legacy.MinimapUiScale = document.Value<float?>("_minimapUiScale") ?? 1.0f;
            legacy.Validate();

            return legacy;
        }

        /// <summary>
        /// カテゴリ別の形式へ保存用文字列を生成します。
        /// </summary>
        public static string Serialize(UserSettings settings)
        {
            settings.Validate();

            return JsonConvert.SerializeObject(settings, s_options);
        }
    }
}
