using Newtonsoft.Json;

namespace PranaTactics.Settings
{
    /// <summary>
    /// カテゴリ別の設定と既存コード用の互換アクセサーを保持します。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class UserSettings
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 2;

        [JsonProperty("general")]
        public GeneralSettings General { get; set; } = new GeneralSettings();

        [JsonProperty("appearance")]
        public AppearanceSettings Appearance { get; set; } = new AppearanceSettings();

        [JsonProperty("controls")]
        public ControlsSettings Controls { get; set; } = new ControlsSettings();

        [JsonProperty("audio")]
        public AudioSettings Audio { get; set; } = new AudioSettings();

        [JsonProperty("hasFinishedTutorial")]
        public bool HasFinishedTutorial
        {
            get; set;
        }

        public bool IsMinimapVariable
        {
            get => General.MinimapMode == "variable";
            set => General.MinimapMode = value ? "variable" : "fixed";
        }

        public float MinimapZoom
        {
            get => General.MinimapZoom;
            set => General.MinimapZoom = MinimapViewMath.Clamp(value, 0.25f, 2.5f, 1.0f);
        }

        public float MinimapUiScale
        {
            get => General.MinimapUiScale;
            set => General.MinimapUiScale = MinimapViewMath.Clamp(value, 0.5f, 2.0f, 1.0f);
        }

        public bool IsFullScreen
        {
            get => General.IsFullScreen;
            set => General.IsFullScreen = value;
        }

        public bool HoldMovementSpeed
        {
            get => Controls.HoldWalk && Controls.HoldStop;
            set
            {
                Controls.HoldWalk = value;
                Controls.HoldStop = value;
            }
        }

        public float MouseSensitivity
        {
            get => Controls.MouseSensitivity;
            set => Controls.MouseSensitivity = MinimapViewMath.Clamp(value, 0.0f, float.MaxValue, 1.0f);
        }

        public float KeyboardSensitivity
        {
            get => Controls.KeyboardSensitivity;
            set => Controls.KeyboardSensitivity = MinimapViewMath.Clamp(value, 0.0f, float.MaxValue, 1.0f);
        }

        public float BgmVolume
        {
            get => Audio.BgmVolume;
            set => Audio.BgmVolume = MinimapViewMath.Clamp(value, 0.0f, 1.0f, 1.0f);
        }

        public float SeVolume
        {
            get => Audio.SeVolume;
            set => Audio.SeVolume = MinimapViewMath.Clamp(value, 0.0f, 1.0f, 1.0f);
        }

        /// <summary>
        /// 設定値を既定値へ戻し、チュートリアルの履歴は維持します。
        /// </summary>
        public void ResetToDefaults()
        {
            General = new GeneralSettings();
            Appearance = new AppearanceSettings();
            Controls = new ControlsSettings();
            Audio = new AudioSettings();
        }

        /// <summary>
        /// 欠落したカテゴリと範囲外の値を補正します。
        /// </summary>
        public void Validate()
        {
            General ??= new GeneralSettings();
            Appearance ??= new AppearanceSettings();
            Appearance.Validate();

            if (General.LanguageCode != "ja" && General.LanguageCode != "en")
            {
                General.LanguageCode = "ja";
            }

            Controls ??= new ControlsSettings();
            Audio ??= new AudioSettings();
            Controls.MacroViewResetKey = KeyboardBinding.Validate(Controls.MacroViewResetKey, "R");
            Controls.MapKey = KeyboardBinding.Validate(Controls.MapKey, "M");
            Controls.WalkKey = KeyboardBinding.Validate(Controls.WalkKey, "Shift");
            Controls.StopKey = KeyboardBinding.Validate(Controls.StopKey, "Ctrl");
            IsMinimapVariable = IsMinimapVariable;
            MinimapZoom = MinimapZoom;
            MinimapUiScale = MinimapUiScale;
            Controls.ScrollSensitivity = MinimapViewMath.Clamp(Controls.ScrollSensitivity, 0.1f, 5.0f, 1.0f);
            MouseSensitivity = MouseSensitivity;
            KeyboardSensitivity = KeyboardSensitivity;
            BgmVolume = BgmVolume;
            SeVolume = SeVolume;
        }
    }
}
