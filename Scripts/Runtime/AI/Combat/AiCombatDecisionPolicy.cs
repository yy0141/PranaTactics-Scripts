using System;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 観測情報だけで評価する、Micro戦闘中の標的優先度です。
    /// 射程・射線・使用コストを満たす標的に対して使用します。
    /// </summary>
    public static class AiCombatDecisionPolicy
    {
        /// <remarks>
        /// <para>距離の小さな変動より集中攻撃と照準継続を重視します。</para>
        /// <para>信頼できる消耗の観測は、攻撃対象の切り替え理由になります。</para>
        /// </remarks>
        public static float EvaluateTarget(
            float normalizedDistance,
            float estimatedPranaRatio,
            float estimateConfidence,
            bool isAssignedTarget,
            bool isCurrentTarget)
        {
            float weakness = (1.0f - Clamp01(estimatedPranaRatio)) * Clamp01(estimateConfidence);

            return 20.0f / (1.0f + Math.Max(0.0f, normalizedDistance)) + weakness * 28.0f +
                (isAssignedTarget ? 12.0f : 0.0f) +
                (isCurrentTarget ? 8.0f : 0.0f);
        }

        public static bool ShouldKeepMoving(
            bool hasAttackOpportunity,
            bool forceReposition,
            bool suppressWeaponUse)
        {
            return !hasAttackOpportunity || forceReposition || suppressWeaponUse;
        }

        /// <summary>
        /// 同じ武器で攻撃を続けた時間に応じて、他の有効な武器を選びやすくします。
        /// </summary>
        public static float EvaluateWeaponRepetitionPenalty(float attackSeconds)
        {
            return Math.Max(0.0f, Math.Min(32.0f, (attackSeconds - 2.0f) * 8.0f));
        }

        /// <summary>
        /// 生成済み武器の射程、実データの射程、カタログ代替値の順で攻撃距離を求めます。
        /// </summary>
        /// <remarks>
        /// Diameterは扇形生成時に単位半径へ掛けるスケールとして使われます。
        /// </remarks>
        public static float ResolveAttackRange(
            float runtimeRange,
            float dataDiameter,
            float parentScale,
            float fallbackRange)
        {
            if (runtimeRange > 0.0f)
            {
                return runtimeRange;
            }

            return dataDiameter > 0.0f ? dataDiameter * Math.Abs(parentScale) : fallbackRange;
        }

        /// <summary>
        /// 射程内で射線を確保できる場合は危険回避を抑え、射撃位置を優先します。
        /// </summary>
        public static float ResolveEngagementDangerMultiplier(
            float configuredMultiplier,
            bool hasClearAttackPosition,
            bool forceReposition)
        {
            float multiplier = Math.Max(0.0f, configuredMultiplier);

            return hasClearAttackPosition && !forceReposition ? Math.Min(multiplier, 0.45f) : multiplier;
        }

        /// <summary>
        /// 武器の射界に余裕を残した照準誤差と発射許容角度の上限を求めます。
        /// </summary>
        public static float ResolveAimLimit(float configuredDegrees, float attackArcDegrees)
        {
            return Math.Max(0.0f, Math.Min(configuredDegrees, Math.Max(0.0f, attackArcDegrees) * 0.25f));
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0.0f, Math.Min(1.0f, value));
        }
    }
}
