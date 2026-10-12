namespace PranaTactics.Settings
{
    /// <summary>
    /// カテゴリに属するユーザー設定値を保持します。
    /// </summary>
    public sealed class GeneralSettings
    {
        public string LanguageCode { get; set; } = "ja";

        public bool IsSoloPlay { get; set; } = true;

        public string GameModeId { get; set; } = "elimination";

        public string MinimapMode { get; set; } = "fixed";

        public float MinimapZoom { get; set; } = 1.0f;

        public float MinimapUiScale { get; set; } = 1.0f;

        public bool IsFullScreen { get; set; } = true;
    }
}
