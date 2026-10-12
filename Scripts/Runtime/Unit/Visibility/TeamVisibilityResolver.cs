using System.Collections.Generic;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 生存している同一チームの観測情報を集約します。
    /// </summary>
    public static class TeamVisibilityResolver
    {
        /// <summary>
        /// 指定チームの誰か一人でも対象を視認しているか判定します。
        /// </summary>
        /// <remarks>
        /// 観測者ごとの集合を参照するため、重複通知や一人の視認解除では他の味方の情報を失いません。
        /// </remarks>
        public static bool IsVisibleToTeam(
            IEnumerable<UnitStatus> observers,
            int observerTeamId,
            int targetPlayerId)
        {
            if (observers == null || observerTeamId < 0 || targetPlayerId < 0)
            {
                return false;
            }

            foreach (UnitStatus observer in observers)
            {
                if (observer != null && observer.IsAlive && observer.TeamId == observerTeamId &&
                    observer.ContainsVisiblePlayer(targetPlayerId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
