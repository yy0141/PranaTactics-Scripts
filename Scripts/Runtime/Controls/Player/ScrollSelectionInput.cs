using System;

using PranaTactics.Settings;

namespace PranaTactics.Controls
{
    /// <summary>
    /// ホイール量を蓄積し、感度と方向に応じたスロット移動数へ変換します。
    /// </summary>
    public sealed class ScrollSelectionInput
    {
        /// <summary>
        /// InputManagerのMouse ScrollWheel軸で通常のホイール1段に相当する量です。
        /// </summary>
        private const double k_ScrollPerStep = 0.1;

        private double _remainder;

        private float _previousSensitivity;

        private bool _wasInverted;

        /// <summary>
        /// 端数は次の入力へ繰り越し、方向や設定の変更時には破棄します。
        /// 一度に要求できる移動数を制限し、極端な入力による整数オーバーフローを防ぎます。
        /// </summary>
        public int ReadOffset(float delta, float sensitivity, bool inverted, int maximumSteps)
        {
            sensitivity = MinimapViewMath.Clamp(sensitivity, 0.1f, 5.0f, 1.0f);

            if (sensitivity != _previousSensitivity || inverted != _wasInverted)
            {
                _remainder = 0;
                _previousSensitivity = sensitivity;
                _wasInverted = inverted;
            }

            if (float.IsNaN(delta) || float.IsInfinity(delta) || maximumSteps <= 0)
            {
                _remainder = 0;

                return 0;
            }

            if (delta == 0)
            {
                return 0;
            }

            double scaled = delta * (double)sensitivity / k_ScrollPerStep;

            if (_remainder * scaled < 0)
            {
                _remainder = 0;
            }

            double total = _remainder + scaled;
            double steps = Math.Floor(Math.Abs(total) + 0.000001);

            _remainder = Math.Sign(total) * Math.Max(0, Math.Abs(total) - steps);
            int offset = (int)Math.Min(maximumSteps, steps) * Math.Sign(total);

            return inverted ? offset : -offset;
        }
    }
}
