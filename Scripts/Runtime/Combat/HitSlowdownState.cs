using System;
using System.Collections.Generic;

namespace PranaTactics.Combat
{
    /// <summary>
    /// 同じ減速率の時間を更新し、有効な効果のうち最も強い減速を適用します。
    /// </summary>
    public sealed class HitSlowdownState
    {
        /// <summary>
        /// 減速率ごとのゲーム時間上の有効期限です。
        /// </summary>
        private readonly Dictionary<float, double> _expiresByRatio = new();

        /// <summary>
        /// 辞書の走査後に除去する期限切れの減速率です。
        /// </summary>
        private readonly List<float> _expiredRatios = new();

        /// <summary>
        /// 有効な減速効果を追加し、同じ率の再ヒットで期限を延長します。
        /// </summary>
        public void Apply(float ratio, float durationSeconds, double currentTime)
        {
            if (float.IsNaN(ratio) || float.IsInfinity(ratio) ||
                float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) ||
                ratio <= 0.0f || durationSeconds <= 0.0f)
            {
                return;
            }

            ratio = Math.Min(1.0f, ratio);
            double expires = currentTime + durationSeconds;
            _expiresByRatio[ratio] = _expiresByRatio.TryGetValue(ratio, out double previous)
                ? Math.Max(previous, expires)
                : expires;
        }

        /// <summary>
        /// 期限切れ効果を除去し、通常・歩行速度へ掛ける倍率を返します。
        /// </summary>
        public float GetMovementMultiplier(double currentTime)
        {
            float strongest = 0.0f;
            _expiredRatios.Clear();
            foreach (KeyValuePair<float, double> effect in _expiresByRatio)
            {
                if (effect.Value <= currentTime)
                {
                    _expiredRatios.Add(effect.Key);
                }
                else
                {
                    strongest = Math.Max(strongest, effect.Key);
                }
            }

            foreach (float ratio in _expiredRatios)
            {
                _expiresByRatio.Remove(ratio);
            }

            return 1.0f - strongest;
        }

        /// <summary>
        /// 再生成時に残っている効果を解除します。
        /// </summary>
        public void Reset()
        {
            _expiresByRatio.Clear();
            _expiredRatios.Clear();
        }
    }
}
