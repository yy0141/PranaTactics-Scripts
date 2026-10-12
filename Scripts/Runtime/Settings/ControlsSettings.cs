namespace PranaTactics.Settings
{
    /// <summary>
    /// カテゴリに属するユーザー設定値を保持します。
    /// </summary>
    public sealed class ControlsSettings
    {
        public float MouseSensitivity { get; set; } = 1.0f;

        public float ScrollSensitivity { get; set; } = 1.0f;

        public bool IsScrollInverted
        {
            get; set;
        }

        /// <summary>
        /// 現在の通常・反転設定をホイール入力へ適用します。
        /// </summary>
        public float ApplyScrollDirection(float delta)
        {
            return IsScrollInverted ? -delta : delta;
        }

        public float KeyboardSensitivity { get; set; } = 1.0f;

        public bool HoldWalk
        {
            get; set;
        }

        public bool HoldStop
        {
            get; set;
        }

        public bool HoldMapDisplay
        {
            get; set;
        }

        public string MacroViewResetKey { get; set; } = "R";

        public string MapKey { get; set; } = "M";

        public string WalkKey { get; set; } = "Shift";

        public string StopKey { get; set; } = "Ctrl";
    }
}
