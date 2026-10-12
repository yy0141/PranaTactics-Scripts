using System;

namespace PranaTactics.Combat
{
    public enum CombatFeedbackKind
    {
        PranaHit,
        DefenseHit,
        UnitHit,
        Elimination
    }

    public enum CombatFeedbackSide
    {
        Unknown,
        Main,
        Sub
    }

    public readonly struct CombatFeedbackResult
    {
        public int AttackerId { get; }
        public int TargetId { get; }
        public CombatFeedbackKind Kind { get; }
        public CombatFeedbackSide Side { get; }

        public CombatFeedbackResult(
            int attackerId,
            int targetId,
            CombatFeedbackKind kind,
            CombatFeedbackSide side)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            Kind = kind;
            Side = side;
        }
    }

    /// <summary>
    /// 確定した攻撃結果を音声とHUDへ通知します。
    /// </summary>
    public static class CombatFeedbackEvents
    {
        public static event Action<CombatFeedbackResult> ResultConfirmed;

        /// <summary>
        /// 通信を行わず、確定済みの攻撃結果を通知します。
        /// </summary>
        public static void Publish(CombatFeedbackResult result)
        {
            if (result.AttackerId < 0 || result.TargetId < 0 ||
                result.AttackerId == result.TargetId)
            {
                return;
            }

            ResultConfirmed?.Invoke(result);
        }
    }
}
