using System;
using System.Collections.Generic;
using System.Globalization;

namespace PranaTactics.Settings
{
    /// <summary>
    /// 設定の読み書きと入力制約を一つのIDに結び付けます。
    /// </summary>
    public sealed class SettingBinding
    {
        private readonly Func<UserSettings, string> _read;

        private readonly Action<UserSettings, string> _write;

        public bool IsNumber
        {
            get;
        }

        public bool IsColor { get; set; }

        public bool IsKey
        {
            get; set;
        }

        public float Minimum
        {
            get;
        }

        public float Maximum
        {
            get;
        }

        public IReadOnlyList<string> AllowedValues
        {
            get;
        }

        public SettingBinding(Func<UserSettings, string> read, Action<UserSettings, string> write,
            bool isNumber, float minimum = 0.0f, float maximum = 1.0f, params string[] allowedValues)
        {
            _read = read;
            _write = write;
            IsNumber = isNumber;
            Minimum = minimum;
            Maximum = maximum;
            AllowedValues = allowedValues;
        }

        public string Read(UserSettings settings)
        {
            return _read(settings);
        }

        /// <summary>
        /// 有限の数値または登録済みの選択肢であるかを判定します。
        /// </summary>
        public bool Accepts(string value)
        {
            if (IsColor)
            {
                return AppearanceSettings.TryNormalizeColor(value, out _);
            }

            if (IsNumber)
            {
                return TryParse(value, out float number) && number >= Minimum && number <= Maximum;
            }

            foreach (string allowed in AllowedValues)
            {
                if (allowed == value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 不正入力を拒否し、数値は仕様上の範囲へ補正します。
        /// </summary>
        public bool TryWrite(
            UserSettings settings,
            string value,
            float? minimum = null,
            float? maximum = null)
        {
            if (IsNumber)
            {
                if (!TryParse(value, out float number))
                {
                    return false;
                }

                float lower = minimum ?? Minimum;
                float upper = maximum ?? Maximum;

                if (float.IsNaN(lower) || float.IsInfinity(lower) || float.IsNaN(upper) ||
                    float.IsInfinity(
                    upper) ||
                    lower > upper || lower < Minimum || upper > Maximum)
                {
                    return false;
                }

                value = Math.Max(lower, Math.Min(upper, number)).ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
            }
            else if (!Accepts(value))
            {
                return false;
            }

            if (IsColor)
            {
                AppearanceSettings.TryNormalizeColor(value, out value);
            }

            _write(settings, value);

            return true;
        }

        private static bool TryParse(string value, out float number)
        {
            bool parsed = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                || float.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);

            return parsed && !float.IsNaN(number) && !float.IsInfinity(number);
        }
    }
}
