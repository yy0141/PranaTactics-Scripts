using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 公開されたプレイヤー位置と敵情報から味方の交戦支援を計画します。
    /// </summary>
    public static class AiPlayerSupportPlanner
    {
        private const float k_PlayerEngagementRadiusCells = 18.0f;

        /// <summary>
        /// プレイヤー付近の観測済み敵への交戦を味方AIに優先させます。
        /// </summary>
        /// <remarks>
        /// 未観測の敵やステルス敵の現在位置は参照しません。
        /// キャラクター別の射程、照準誤差と発射条件は維持します。
        /// </remarks>
        public static void ApplyDirective(
            AiUnitTacticDirective directive,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board,
            int controlledPlayerId)
        {
            if (directive == null || snapshot == null || board == null || controlledPlayerId < 0)
            {
                return;
            }

            AiPublicUnitState player = null;
            foreach (AiPublicUnitState ally in snapshot.Allies)
            {
                if (ally != null && ally.IsAlive && ally.PlayerId == controlledPlayerId)
                {
                    player = ally;
                    break;
                }
            }

            if (player == null)
            {
                return;
            }

            AiPublicUnitState threat = null;
            float nearestDistance = k_PlayerEngagementRadiusCells;
            foreach (AiPublicUnitState enemy in snapshot.Enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Unit == null ||
                    enemy.ContactState < AiContactState.MapTracked)
                {
                    continue;
                }

                float distance = board.EstimateCellDistance(player.MapCoordinate, enemy.MapCoordinate);
                if (distance < nearestDistance || (distance == nearestDistance &&
                    (threat == null || enemy.PlayerId < threat.PlayerId)))
                {
                    nearestDistance = distance;
                    threat = enemy;
                }
            }

            if (threat == null)
            {
                return;
            }

            directive.Target = threat.Unit;
            directive.TaskId = AiTeamTaskIds.EngagePrimary;
            directive.SuppressWeaponUse = false;
            directive.ForceReposition = false;
            directive.DangerWeightMultiplier = Mathf.Min(directive.DangerWeightMultiplier, 0.65f);
            directive.HasTargetCoordinate = true;
            directive.TargetCoordinate = threat.MapCoordinate;
            directive.HasReferenceCoordinate = false;
            directive.UseCombatRange = true;
            directive.RequireMutualCoverage = false;
            directive.FormationWeightMultiplier = Mathf.Min(directive.FormationWeightMultiplier, 0.35f);
        }
    }
}
