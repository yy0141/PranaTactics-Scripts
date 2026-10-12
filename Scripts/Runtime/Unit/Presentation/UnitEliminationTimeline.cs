using System;

namespace PranaTactics.Unit.Presentation
{
    public readonly struct UnitEliminationFrame
    {
        public float Radius { get; }
        public float LineScale { get; }
        public float Opacity { get; }
        public bool IsComplete { get; }

        /// <summary>
        /// 指定時刻の描画状態を保持します。
        /// </summary>
        public UnitEliminationFrame(float radius, float lineScale, float opacity, bool isComplete)
        {
            Radius = radius;
            LineScale = lineScale;
            Opacity = opacity;
            IsComplete = isComplete;
        }
    }

    /// <summary>
    /// 出現、急膨張、保持、収縮の時間から球体と細線の状態を計算します。
    /// </summary>
    public static class UnitEliminationTimeline
    {
        /// <summary>
        /// 指定時刻の半径、細線の伸長率、透明度を取得します。
        /// </summary>
        /// <param name="elapsed">
        /// 出現から経過した秒数です。
        /// </param>
        /// <param name="initialRadius">
        /// 出現時のワールド空間での半径です。
        /// </param>
        /// <param name="expandedRadius">
        /// 急膨張後のワールド空間での半径です。
        /// </param>
        public static UnitEliminationFrame Evaluate(
            double elapsed,
            float initialRadius,
            float expandedRadius,
            float expansionDuration,
            float holdDuration,
            float collapseDuration)
        {
            initialRadius = Sanitize(initialRadius);
            expandedRadius = Math.Max(initialRadius, Sanitize(expandedRadius));
            expansionDuration = Sanitize(expansionDuration);
            holdDuration = Sanitize(holdDuration);
            collapseDuration = Sanitize(collapseDuration);
            elapsed = double.IsNaN(elapsed) ? 0.0 : Math.Max(0.0, elapsed);

            if (elapsed < expansionDuration)
            {
                float progress = (float)(elapsed / expansionDuration);
                float eased = 1.0f - (float)Math.Pow(1.0f - progress, 3.0);
                return new UnitEliminationFrame(
                    initialRadius + (expandedRadius - initialRadius) * eased, eased, 1.0f, false);
            }

            if (elapsed < expansionDuration + holdDuration)
            {
                return new UnitEliminationFrame(expandedRadius, 1.0f, 1.0f, false);
            }

            double collapseElapsed = elapsed - expansionDuration - holdDuration;
            if (collapseDuration <= 0.0f || collapseElapsed >= collapseDuration)
            {
                return new UnitEliminationFrame(0.0f, 0.0f, 0.0f, true);
            }

            float collapse = (float)(collapseElapsed / collapseDuration);
            float remaining = 1.0f - collapse * collapse;
            return new UnitEliminationFrame(expandedRadius * remaining, remaining, 1.0f, false);
        }

        /// <summary>
        /// 負値と非有限値を除外します。
        /// </summary>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0.0f : Math.Max(0.0f, value);
        }
    }
}
