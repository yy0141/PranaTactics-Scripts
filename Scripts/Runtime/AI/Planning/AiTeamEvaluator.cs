using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 公開済みの味方位置から孤立と過密を評価します。
    /// 距離条件はAiRoleDefinitionから取得します。
    /// </summary>
    public sealed class AiTeamEvaluator
    {
        private readonly AiBoardAdapter _board;

        private readonly IReadOnlyList<AiPublicUnitState> _allies;

        public AiTeamEvaluator(AiBoardAdapter board, IReadOnlyList<AiPublicUnitState> allies)
        {
            _board = board;
            _allies = allies;
        }

        /// <remarks>
        /// <para>人数が増えるほど密集加点が際限なく増えないよう、</para>
        /// <para>味方人数に対する割合で正規化します。</para>
        /// </remarks>
        public float Evaluate(Vector2Int coordinate, UnitBehavior self, string roleId)
        {
            if (_allies == null || _allies.Count <= 1)
            {
                return 0.0f;
            }

            float nearest = float.MaxValue;
            int allyCount = 0;
            int supportCount = 0;
            float crowdingPenalty = 0.0f;
            const float crowdingRadiusCells = 2.5f;
            const float supportMaximumCells = 7.0f;

            foreach (AiPublicUnitState ally in _allies)
            {
                if (ally == null || !ally.IsAlive || ally.Unit == null || ally.Unit == self)
                {
                    continue;
                }

                float distance = _board.EstimateCellDistance(coordinate, ally.MapCoordinate);

                allyCount++;
                nearest = Mathf.Min(nearest, distance);

                if (distance < crowdingRadiusCells)
                {
                    crowdingPenalty += (crowdingRadiusCells - distance) * 8.0f;
                }
                else if (distance <= supportMaximumCells)
                {
                    supportCount++;
                }
            }

            if (nearest == float.MaxValue)
            {
                return 0.0f;
            }

            float supportRatio = allyCount > 0 ? supportCount / (float)allyCount : 0.0f;
            float utility = supportRatio * 14.0f;

            if (allyCount > 0)
            {
                utility -= crowdingPenalty / allyCount;
            }

            AiRoleDefinition role = AiDefinitionRegistry.GetRole(roleId);

            if (nearest > role.IsolationDistance)
            {
                utility -= (nearest - role.IsolationDistance) * 5.0f;
            }

            if (nearest < role.MinimumAllyDistance)
            {
                utility -= (role.MinimumAllyDistance - nearest) * 4.0f;
            }

            return utility;
        }

        public float Evaluate(Vector2Int coordinate, UnitBehavior self, AiUnitRole legacyRole)
        {
            return Evaluate(coordinate, self, AiRoleResolver.ToRoleId(legacyRole));
        }

        /// <summary>
        /// 対象地点から生存中の味方全員を相互カバー範囲へ
        /// 収められるかを、人数で正規化して評価します。
        /// </summary>
        public float EvaluateMutualCoverage(
            Vector2Int coordinate,
            UnitBehavior self,
            float minimumCells,
            float maximumCells)
        {
            if (_allies == null || _allies.Count <= 1)
            {
                return 0.0f;
            }

            float safeMinimum = Mathf.Max(0.0f, minimumCells);
            float safeMaximum = Mathf.Max(safeMinimum + 0.1f, maximumCells);
            int allyCount = 0;
            int coveredCount = 0;
            float outsidePenalty = 0.0f;

            foreach (AiPublicUnitState ally in _allies)
            {
                if (ally == null || !ally.IsAlive || ally.Unit == null || ally.Unit == self)
                {
                    continue;
                }

                allyCount++;
                float distance = _board.EstimateCellDistance(coordinate, ally.MapCoordinate);

                if (distance >= safeMinimum && distance <= safeMaximum)
                {
                    coveredCount++;
                    continue;
                }

                if (distance < safeMinimum)
                {
                    outsidePenalty += (safeMinimum - distance) * 8.0f;
                }
                else
                {
                    outsidePenalty += Mathf.Min((distance - safeMaximum) * 4.0f, 20.0f);
                }
            }

            if (allyCount == 0)
            {
                return 0.0f;
            }

            float coverageRatio = coveredCount / (float)allyCount;

            return coverageRatio * 35.0f - outsidePenalty / allyCount;
        }
    }
}
