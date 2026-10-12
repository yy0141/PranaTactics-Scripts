using System;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// マクロ計画全体の期限を保持し、残り時間を未計画のUnitへ再配分します。
    /// </summary>
    public sealed class AiMacroPlanningBudget
    {
        /// <summary>
        /// 全体の計画期限です。
        /// </summary>
        private readonly double _deadline;

        public double StartedAt { get; }

        /// <summary>
        /// ゲーム時間を基準として目標時間と最大時間の短い方を採用します。
        /// </summary>
        public AiMacroPlanningBudget(double startedAt, double targetSeconds, double maximumSeconds)
        {
            StartedAt = startedAt;
            _deadline = startedAt + Math.Min(NonNegative(targetSeconds), NonNegative(maximumSeconds));
        }

        /// <summary>
        /// 全体期限までの残り時間を取得します。
        /// </summary>
        public double GetRemainingSeconds(double now)
        {
            return Math.Max(0.0, _deadline - now);
        }

        /// <summary>
        /// フェイズ残り時間と全体期限の両方を守る1体分の期限を求めます。
        /// </summary>
        /// <param name="remainingAgents">
        /// 今から探索するUnitを含む未計画のUnit数です。
        /// </param>
        /// <param name="phaseRemainingSeconds">
        /// 経路反映用の余白を引いたフェイズ残り時間です。
        /// </param>
        public double GetAgentDeadline(double now, int remainingAgents, double phaseRemainingSeconds)
        {
            double available = Math.Min(GetRemainingSeconds(now), NonNegative(phaseRemainingSeconds));
            return now + available / Math.Max(1, remainingAgents);
        }

        /// <summary>
        /// 非有限値と負値を除外します。
        /// </summary>
        private static double NonNegative(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : Math.Max(0.0, value);
        }
    }
}
