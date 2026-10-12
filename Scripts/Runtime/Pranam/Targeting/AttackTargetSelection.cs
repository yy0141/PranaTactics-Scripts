using System.Collections.Generic;

using PranaTactics.Unit;

namespace PranaTactics.Pranam.Targeting
{
    /// <summary>
    /// 攻撃可能な全候補を選択します。
    /// </summary>
    public sealed class AllAttackTargetSelection : IAttackTargetSelection
    {
        public static AllAttackTargetSelection Instance { get; } = new();

        /// <summary>
        /// 呼び出し元が検証済みの候補を選択結果へ追加します。
        /// </summary>
        public void SelectTargets(
            IReadOnlyList<AttackTargetCandidate> candidates,
            ICollection<UnitBehavior> selectedTargets)
        {
            foreach (AttackTargetCandidate candidate in candidates)
            {
                selectedTargets.Add(candidate.Unit);
            }
        }
    }

    /// <summary>
    /// 攻撃可能な候補から最も近い一体を選択します。
    /// </summary>
    public sealed class NearestAttackTargetSelection : IAttackTargetSelection
    {
        public static NearestAttackTargetSelection Instance { get; } = new();

        /// <summary>
        /// 同距離ではプレイヤーIDの小さい対象を選び、走査順による対象の揺れを防ぎます。
        /// </summary>
        public void SelectTargets(
            IReadOnlyList<AttackTargetCandidate> candidates,
            ICollection<UnitBehavior> selectedTargets)
        {
            UnitBehavior nearest = null;
            float minimumSquaredDistance = float.MaxValue;

            foreach (AttackTargetCandidate candidate in candidates)
            {
                if (candidate.SquaredDistance > minimumSquaredDistance ||
                    (candidate.SquaredDistance == minimumSquaredDistance &&
                    nearest != null && candidate.Unit.PlayerId >= nearest.PlayerId))
                {
                    continue;
                }

                nearest = candidate.Unit;
                minimumSquaredDistance = candidate.SquaredDistance;
            }

            if (nearest != null)
            {
                selectedTargets.Add(nearest);
            }
        }
    }
}
