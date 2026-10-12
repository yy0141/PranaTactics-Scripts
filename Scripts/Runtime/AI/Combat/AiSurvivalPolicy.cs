using System;

namespace PranaTactics.AI.Solo
{
    public enum AiCombatStance
    {
        Conceal,
        DoubleAttack,
        AttackAndDefense,
        DoubleDefense
    }

    /// <summary>
    /// 観測済みの脅威と残りプラーナから攻防の組み合わせを選びます。
    /// </summary>
    public static class AiSurvivalPolicy
    {
        /// <summary>
        /// 射撃間隔や短い照準ずれで攻撃を中断しない継続時間です。
        /// </summary>
        private const float k_AttackContinuationSeconds = 3.0f;

        /// <summary>
        /// 交戦終了後にオブスクラを再使用する確率です。
        /// </summary>
        private const float k_PostCombatStealthProbability = 0.3f;

        /// <summary>
        /// 攻撃後にマップ表示を維持する時間です。
        /// </summary>
        private const float k_AttackRevealSeconds = 5.0f;

        /// <summary>
        /// 交戦終了時に一度だけ抽選し、次の交戦まで隠密を使用するか決定します。
        /// </summary>
        /// <param name="randomSample">
        /// 0以上1以下の一様乱数です。
        /// </param>
        public static bool ShouldUseStealthAfterCombat(float randomSample)
        {
            return randomSample >= 0.0f && randomSample < k_PostCombatStealthProbability;
        }

        /// <summary>
        /// 最後に成立した攻撃から5秒間は隠密へ戻ることを禁止します。
        /// </summary>
        public static bool ShouldRemainRevealed(float secondsSinceAttack)
        {
            return secondsSinceAttack >= 0.0f && secondsSinceAttack < k_AttackRevealSeconds;
        }

        /// <summary>
        /// 攻撃を開始した後、対象を攻撃できる間だけ短い発射待ちを許容します。
        /// </summary>
        public static bool CanContinueAttack(
            bool wasAttacking, bool canAttack, bool isAuthorized, float secondsSinceReady)
        {
            return wasAttacking && canAttack && isAuthorized &&
                secondsSinceReady >= 0.0f && secondsSinceReady <= k_AttackContinuationSeconds;
        }

        /// <summary>
        /// 隠密が許可されていても交戦・攻撃後の表示・両側防御を優先します。
        /// </summary>
        public static bool ShouldMaintainStealth(bool hasStealth, bool isAttackReady, bool needsDoubleDefense)
        {
            return hasStealth && !isAttackReady && !needsDoubleDefense;
        }

        /// <summary>
        /// 接敵前は隠密、通常交戦は攻撃、危険時は防御を優先します。
        /// </summary>
        public static AiCombatStance Evaluate(
            bool isEngaged,
            bool canAttack,
            int imminentThreatCount,
            int threatDirectionCount,
            bool wasRecentlyHit,
            float pranaRatio,
            float combatSeconds)
        {
            if (!isEngaged)
            {
                return AiCombatStance.Conceal;
            }

            bool isUnderAttack = imminentThreatCount > 0 || wasRecentlyHit;
            if (imminentThreatCount >= 12 ||
                (threatDirectionCount >= 2 && imminentThreatCount >= 8) ||
                (isUnderAttack && pranaRatio <= 0.15f))
            {
                return AiCombatStance.DoubleDefense;
            }

            float cycle = Math.Max(0.0f, combatSeconds) % 8.0f;
            if (imminentThreatCount >= 4 || (wasRecentlyHit && pranaRatio <= 0.4f) ||
                !canAttack || cycle >= 7.6f)
            {
                return AiCombatStance.AttackAndDefense;
            }

            return AiCombatStance.DoubleAttack;
        }
    }
}
