namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 合流と連携射撃の待機を制限し、交戦機会を優先します。
    /// </summary>
    public static class AiEngagementPolicy
    {
        private const float k_MaximumStrikeWaitSeconds = 0.25f;

        /// <summary>
        /// 接敵、索敵または一回の合流計画後は攻勢へ移行します。
        /// </summary>
        public static bool ShouldBeginAssault(
            bool hasGathered,
            bool hasNearbyEnemy,
            bool hasAttackableEnemy,
            bool mustEngage,
            int completedRegroupPlans)
        {
            return hasGathered || hasNearbyEnemy || !hasAttackableEnemy ||
                mustEngage || completedRegroupPlans >= 1;
        }

        /// <summary>
        /// 射撃可能な味方を、準備できない味方のために待たせ続けません。
        /// </summary>
        public static bool CanOpenFireWindow(int participantCount, int readyCount, int requiredCount, float readyWaitSeconds)
        {
            return readyCount > 0 && (participantCount == 1 || readyCount >= requiredCount ||
                readyWaitSeconds >= k_MaximumStrikeWaitSeconds);
        }
    }
}
