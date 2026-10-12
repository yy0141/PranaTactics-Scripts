using System;
using System.Collections.Generic;
using System.Globalization;

namespace PranaTactics.Settings
{
    /// <summary>
    /// ゲームの設定と安定した項目IDの対応を定義します。
    /// </summary>
    public sealed class SettingsCatalog
    {
        private readonly Dictionary<string, SettingBinding> _bindings = new Dictionary<string, SettingBinding>();

        public SettingsCatalog()
        {
            AddColor("appearance.confirmedTargetColor", s => s.Appearance.ConfirmedTargetColor,
                (s, v) => s.Appearance.ConfirmedTargetColor = v);
            AddColor("appearance.allyTeamColor", s => s.Appearance.AllyTeamColor,
                (s, v) => s.Appearance.AllyTeamColor = v);
            for (int index = 0; index < 6; index++)
            {
                int slot = index;
                AddColor("appearance.enemyTeamColors." + slot, s => s.Appearance.EnemyTeamColors[slot],
                    (s, v) => s.Appearance.EnemyTeamColors[slot] = v);
            }

            for (int index = 0; index < 5; index++)
            {
                int slot = index;
                AddColor("appearance.allyRouteColors." + slot + ".line", s => s.Appearance.AllyRouteColors[slot].Line,
                    (s, v) => s.Appearance.AllyRouteColors[slot].Line = v);
                AddColor("appearance.allyRouteColors." + slot + ".arrow", s => s.Appearance.AllyRouteColors[slot].Arrow,
                    (s, v) => s.Appearance.AllyRouteColors[slot].Arrow = v);
            }

            _bindings.Add("general.languageCode", new SettingBinding(s => s.General.LanguageCode,
                (s, v) => s.General.LanguageCode = v, false, allowedValues: new[] { "ja", "en" }));
            AddKey("controls.macroViewResetKey", s => s.Controls.MacroViewResetKey,
                (s, v) => s.Controls.MacroViewResetKey = v);
            AddKey("controls.mapKey", s => s.Controls.MapKey, (s, v) => s.Controls.MapKey = v);
            AddKey("controls.walkKey", s => s.Controls.WalkKey, (s, v) => s.Controls.WalkKey = v);
            AddKey("controls.stopKey", s => s.Controls.StopKey, (s, v) => s.Controls.StopKey = v);
            AddNumber("general.minimapZoom", s => s.MinimapZoom, (s, v) => s.MinimapZoom = v, 0.25f, 2.5f);
            AddNumber(
                "general.minimapUiScale",
                s => s.MinimapUiScale,
                (s, v) => s.MinimapUiScale = v,
                0.5f,
                2.0f);
            AddNumber(
                "controls.scrollSensitivity",
                s => s.Controls.ScrollSensitivity,
                (s, v) => s.Controls.ScrollSensitivity = v,
                0.1f,
                5.0f);
            _bindings.Add("controls.scrollDirection", new SettingBinding(s => s.Controls.IsScrollInverted ? "inverted" : "normal",
                (s, v) => s.Controls.IsScrollInverted = v == "inverted", false, allowedValues: new[] { "normal", "inverted" }));
            AddNumber(
                "controls.mouseSensitivity",
                s => s.MouseSensitivity,
                (s, v) => s.MouseSensitivity = v,
                0.0f,
                100.0f);
            AddNumber(
                "controls.keyboardSensitivity",
                s => s.KeyboardSensitivity / 100.0f,
                (s, v) => s.KeyboardSensitivity = v * 100.0f,
                0.0f,
                100.0f);
            AddNumber("audio.bgmVolume", s => s.BgmVolume, (s, v) => s.BgmVolume = v, 0.0f, 1.0f);
            AddNumber("audio.seVolume", s => s.SeVolume, (s, v) => s.SeVolume = v, 0.0f, 1.0f);
            _bindings.Add("general.minimapMode", new SettingBinding(s => s.General.MinimapMode,
                (s, v) => s.General.MinimapMode = v, false, allowedValues: new[] { "fixed", "variable" }));
            _bindings.Add("controls.walkMode", new SettingBinding(s => s.Controls.HoldWalk ? "hold" : "toggle",
                (s, v) => s.Controls.HoldWalk = v == "hold", false, allowedValues: new[] { "toggle", "hold" }));
            _bindings.Add("controls.stopMode", new SettingBinding(s => s.Controls.HoldStop ? "hold" : "toggle",
                (s, v) => s.Controls.HoldStop = v == "hold", false, allowedValues: new[] { "toggle", "hold" }));
            _bindings.Add("controls.mapDisplayMode", new SettingBinding(s => s.Controls.HoldMapDisplay ? "hold" : "toggle",
                (s, v) => s.Controls.HoldMapDisplay = v == "hold", false, allowedValues: new[] { "toggle", "hold" }));
        }

        public SettingBinding Get(string id)
        {
            if (id == null || !_bindings.TryGetValue(id, out SettingBinding binding))
            {
                throw new InvalidOperationException("未登録の設定IDです: " + id);
            }

            return binding;
        }

        /// <summary>
        /// RGB色の保存項目を登録します。
        /// </summary>
        private void AddColor(string id, Func<UserSettings, string> read, Action<UserSettings, string> write)
        {
            _bindings.Add(id, new SettingBinding(read, write, false) { IsColor = true });
        }

        private void AddKey(string id, Func<UserSettings, string> read, Action<UserSettings, string> write)
        {
            _bindings.Add(id, new SettingBinding(read, write, false,
                allowedValues: new List<string>(KeyboardBinding.Values).ToArray())
            {
                IsKey = true
            });
        }

        private void AddNumber(
            string id,
            Func<UserSettings, float> read,
            Action<UserSettings, float> write,
            float minimum,
            float maximum)
        {
            _bindings.Add(id, new SettingBinding(s => read(s).ToString("0.###", CultureInfo.InvariantCulture),
                (s, v) => write(s, float.Parse(v, CultureInfo.InvariantCulture)), true, minimum, maximum));
        }
    }
}
