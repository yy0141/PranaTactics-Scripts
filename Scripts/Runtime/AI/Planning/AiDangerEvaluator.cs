using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 公開された敵位置・推定ロール・接触確信度だけから危険度を求めます。
    /// </summary>
    public sealed class AiDangerEvaluator
    {
        private readonly AiBoardAdapter _board;

        private readonly IReadOnlyList<AiPublicUnitState> _enemies;

        private readonly UnitBehavior _self;

        private readonly AiDifficultyProfile _difficulty;

        /// <summary>
        /// このUnitの計画中に評価済みの座標の危険度です。
        /// </summary>
        private readonly Dictionary<Vector2Int, float> _dangerByCoordinate = new Dictionary<Vector2Int, float>();

        public AiDangerEvaluator(
            AiBoardAdapter board,
            IReadOnlyList<AiPublicUnitState> enemies,
            UnitBehavior self = null,
            AiDifficultyProfile difficulty = null)
        {
            _board = board;
            _enemies = enemies;
            _self = self;
            _difficulty = difficulty ?? AiDifficultyProfile.Create(AiDifficulty.Normal);
        }

        /// <summary>
        /// 今回のUnit計画用Snapshotに対する危険度を座標ごとに再利用します。
        /// </summary>
        public float Evaluate(Vector2Int coordinate)
        {
            if (_dangerByCoordinate.TryGetValue(coordinate, out float cachedDanger))
            {
                return cachedDanger;
            }

            float danger = 0.0f;

            if (_enemies == null)
            {
                return danger;
            }

            foreach (AiPublicUnitState enemy in _enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.ContactState == AiContactState.Unknown)
                {
                    continue;
                }

                float distance = _board.EstimateCellDistance(coordinate, enemy.MapCoordinate);
                AiRoleDefinition role = AiDefinitionRegistry.GetRole(enemy.RoleId);
                float contactFactor = enemy.ContactState == AiContactState.LastKnown ? enemy.ContactConfidence * 0.55f : 1.0f;
                float roleThreat = role.EvaluateThreat(distance) * contactFactor;
                int heightDifference = _board.GetFloor(enemy.MapCoordinate) - _board.GetFloor(coordinate);

                if (heightDifference > 0)
                {
                    roleThreat += heightDifference * 2.5f * contactFactor;
                }

                danger += roleThreat;
            }

            if (_self != null && _self.GetUnitStatus() != null && _self.GetUnitStatus().MaxHealth > 0.001f)
            {
                float ownPranaRatio = _self.GetUnitStatus().CurrentHealth / _self.GetUnitStatus().MaxHealth;

                danger *= Mathf.Lerp(1.45f, 1.0f, Mathf.Clamp01(ownPranaRatio));
            }

            float adjustedDanger = _difficulty.ApplyMacroDanger(danger);
            _dangerByCoordinate[coordinate] = adjustedDanger;
            return adjustedDanger;
        }
    }
}
