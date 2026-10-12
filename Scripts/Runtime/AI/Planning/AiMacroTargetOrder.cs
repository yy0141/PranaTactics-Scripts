using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// マクロフェイズの優先ターゲット指定に使う公開情報と時間条件を判定します。
    /// </summary>
    public static class AiMacroTargetOrder
    {
        /// <summary>
        /// 指定したヘックスにいる観測済みの敵を取得します。
        /// </summary>
        /// <remarks>
        /// 同じヘックスに複数の敵がいる場合はプレイヤーIDの小さい敵を選びます。
        /// 最終観測地点だけが残っている敵は指定できません。
        /// </remarks>
        public static AiPublicUnitState FindTarget(
            IReadOnlyList<AiPublicUnitState> enemies,
            Vector2Int coordinate)
        {
            AiPublicUnitState selected = null;
            if (enemies == null)
            {
                return null;
            }

            foreach (AiPublicUnitState enemy in enemies)
            {
                if (CanOrderTarget(enemy) && enemy.MapCoordinate == coordinate &&
                    (selected == null || enemy.PlayerId < selected.PlayerId))
                {
                    selected = enemy;
                }
            }

            return selected;
        }

        /// <summary>
        /// 指定した敵が引き続き観測可能な場合に、その公開状態を取得します。
        /// </summary>
        public static AiPublicUnitState FindTarget(
            IReadOnlyList<AiPublicUnitState> enemies,
            int playerId)
        {
            if (enemies == null)
            {
                return null;
            }

            foreach (AiPublicUnitState enemy in enemies)
            {
                if (CanOrderTarget(enemy) && enemy.PlayerId == playerId)
                {
                    return enemy;
                }
            }

            return null;
        }

        /// <summary>
        /// 未完了のAI全員へ最低探索時間とフェーズ終了の余裕を確保できるか判定します。
        /// </summary>
        public static bool HasPlanningTime(
            float remainingSeconds,
            float finishMarginSeconds,
            float minimumSecondsPerAgent,
            int agentCount)
        {
            if (agentCount <= 0 || float.IsNaN(remainingSeconds) ||
                float.IsInfinity(remainingSeconds) || float.IsNaN(finishMarginSeconds) ||
                float.IsInfinity(finishMarginSeconds) || float.IsNaN(minimumSecondsPerAgent) ||
                float.IsInfinity(minimumSecondsPerAgent))
            {
                return false;
            }

            float requiredSeconds = Mathf.Max(0.1f, finishMarginSeconds) +
                Mathf.Max(0.1f, minimumSecondsPerAgent) * agentCount;
            return remainingSeconds >= requiredSeconds;
        }

        /// <summary>
        /// 公開状態が現在位置を指定できる生存中の敵か判定します。
        /// </summary>
        private static bool CanOrderTarget(AiPublicUnitState enemy)
        {
            return enemy != null && enemy.Unit != null && enemy.IsAlive && !enemy.IsAlly &&
                enemy.ContactState >= AiContactState.MapTracked;
        }
    }
}
