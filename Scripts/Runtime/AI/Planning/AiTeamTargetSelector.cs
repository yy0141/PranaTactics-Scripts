using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 公開Snapshotだけからチーム標的と個別標的を選択します。
    /// 敵UnitStatusや敵の確定Macro経路は参照しません。
    /// </summary>
    public sealed class AiTeamTargetSelector
    {
        private const float k_CurrentTargetBonus = 8.0f;

        private const float k_PrimaryTargetBonus = 12.0f;

        public AiPublicUnitState AssignTargets(
            IReadOnlyList<SoloAiAgent> agents,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board)
        {
            if (agents == null || snapshot == null || board == null)
            {
                return null;
            }

            AiPublicUnitState primary = SelectPrimaryTarget(agents, snapshot, board);
            int validAgentCount = CountValidAgents(agents);
            int attackableEnemyCount = CountAttackableEnemies(snapshot);
            int normalCommitmentLimit = Mathf.Max(
                1,
                Mathf.CeilToInt(
                validAgentCount / (float)Mathf.Max(
                1,
                attackableEnemyCount)));
            Dictionary<int, int> commitmentCounts = new Dictionary<int, int>();

            foreach (SoloAiAgent agent in agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                AiPublicUnitState best = null;
                float bestUtility = float.NegativeInfinity;

                foreach (AiPublicUnitState enemy in snapshot.Enemies)
                {
                    if (!IsAttackTarget(enemy))
                    {
                        continue;
                    }

                    int count = commitmentCounts.TryGetValue(enemy.PlayerId, out int assigned) ? assigned : 0;
                    float utility = EvaluateForAgent(agent, enemy, board);

                    if (primary != null && primary.PlayerId == enemy.PlayerId)
                    {
                        utility += k_PrimaryTargetBonus;
                    }

                    if (agent.Target != null && agent.Target.PlayerId == enemy.PlayerId)
                    {
                        utility += k_CurrentTargetBonus;
                    }

                    utility -= count * 6.0f;

                    if (count >= normalCommitmentLimit)
                    {
                        utility -= 18.0f;
                    }

                    if (utility > bestUtility)
                    {
                        bestUtility = utility;
                        best = enemy;
                    }
                }

                agent.SetTarget(best != null ? best.Unit : null);
                agent.SetTeamTask(AiTeamTaskIds.EngagePrimary);

                if (best != null)
                {
                    commitmentCounts[best.PlayerId] = commitmentCounts.TryGetValue(
                        best.PlayerId,
                        out int oldCount) ? oldCount +
                        1 : 1;
                }
            }

            return primary;
        }

        /// <summary>
        /// 可変人数のチーム全体から最も適した主目標を選択します。
        /// </summary>
        public AiPublicUnitState SelectPrimaryTarget(
            IReadOnlyList<SoloAiAgent> agents,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board)
        {
            AiPublicUnitState best = null;
            float bestUtility = float.NegativeInfinity;

            foreach (AiPublicUnitState enemy in snapshot.Enemies)
            {
                if (!IsAttackTarget(enemy))
                {
                    continue;
                }

                float utility = EvaluateForTeam(agents, enemy, snapshot, board);

                if (utility > bestUtility ||
                    (Mathf.Approximately(utility, bestUtility) && best != null &&
                    enemy.PlayerId < best.PlayerId))
                {
                    bestUtility = utility;
                    best = enemy;
                }
            }

            return best;
        }

        private static float EvaluateForTeam(
            IReadOnlyList<SoloAiAgent> agents,
            AiPublicUnitState enemy,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board)
        {
            float threatTotal = 0.0f;
            float attackableCount = 0.0f;
            float distanceCost = 0.0f;
            int validAgentCount = 0;
            AiRoleDefinition enemyRole = AiDefinitionRegistry.GetRole(enemy.RoleId);

            foreach (SoloAiAgent agent in agents)
            {
                if (agent == null || agent.Unit == null || agent.Unit.GetTacticalMapUnitBehavior() == null)
                {
                    continue;
                }

                Vector2Int selfCoordinate = agent.Unit.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
                float distance = board.EstimateCellDistance(selfCoordinate, enemy.MapCoordinate);
                AiRoleProfile profile = agent.RoleProfile;

                threatTotal += enemyRole.EvaluateThreat(distance);
                distanceCost += distance;

                if (distance <= profile.PreferredMaximumCells + 5.0f)
                {
                    attackableCount += 1.0f;
                }

                validAgentCount++;
            }

            if (validAgentCount == 0)
            {
                return float.NegativeInfinity;
            }

            float attackableRatio = attackableCount / validAgentCount;
            float averageThreat = threatTotal / validAgentCount;
            float averageDistance = distanceCost / validAgentCount;
            float weakness = (1.0f - enemy.EstimatedPranaRatio) * enemy.PranaEstimateConfidence;
            float isolation = EvaluateIsolation(enemy, snapshot, board);
            AiObjectiveContext objective = snapshot.Objective;
            float objectiveValue = objective.AggressionNeed * 25.0f + (objective.MustEngage ? 20.0f : 0.0f) +
                (objective.CanForceWin ? 30.0f : 0.0f);

            return objectiveValue + Mathf.Clamp01(averageThreat / 45.0f) * 20.0f + attackableRatio * 20.0f +
                isolation * 10.0f +
                weakness * 10.0f - Mathf.Clamp01(
                averageDistance / 12.0f) * 15.0f;
        }

        private static float EvaluateForAgent(
            SoloAiAgent agent,
            AiPublicUnitState enemy,
            AiBoardAdapter board)
        {
            if (agent.Unit.GetTacticalMapUnitBehavior() == null)
            {
                return float.NegativeInfinity;
            }

            Vector2Int selfCoordinate = agent.Unit.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
            float distance = board.EstimateCellDistance(selfCoordinate, enemy.MapCoordinate);
            AiRoleProfile profile = agent.RoleProfile;
            float middle = (profile.PreferredMinimumCells + profile.PreferredMaximumCells) * 0.5f;
            float rangeUtility = 30.0f - Mathf.Abs(distance - middle) * 4.0f;
            float weakness = (1.0f - enemy.EstimatedPranaRatio) * enemy.PranaEstimateConfidence * 10.0f;

            return rangeUtility + weakness;
        }

        private static float EvaluateIsolation(
            AiPublicUnitState target,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board)
        {
            float nearest = float.MaxValue;

            foreach (AiPublicUnitState other in snapshot.Enemies)
            {
                if (other == null || other.PlayerId == target.PlayerId || other.TeamId != target.TeamId ||
                    other.ContactState < AiContactState.MapTracked)
                {
                    continue;
                }

                nearest = Mathf.Min(
                    nearest,
                    board.EstimateCellDistance(
                    target.MapCoordinate,
                    other.MapCoordinate));
            }

            return nearest == float.MaxValue ? 1.0f : Mathf.Clamp01(nearest / 6.0f);
        }

        private static bool IsAttackTarget(AiPublicUnitState enemy)
        {
            return enemy != null && enemy.IsAlive && enemy.Unit != null &&
                enemy.ContactState >= AiContactState.MapTracked;
        }

        private static int CountValidAgents(IReadOnlyList<SoloAiAgent> agents)
        {
            int count = 0;

            if (agents == null)
            {
                return count;
            }

            foreach (SoloAiAgent agent in agents)
            {
                if (agent != null && agent.Unit != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountAttackableEnemies(AiWorldSnapshot snapshot)
        {
            int count = 0;

            if (snapshot == null)
            {
                return count;
            }

            foreach (AiPublicUnitState enemy in snapshot.Enemies)
            {
                if (IsAttackTarget(enemy))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
