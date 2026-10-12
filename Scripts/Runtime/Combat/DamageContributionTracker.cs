using System;
using System.Collections.Generic;

namespace PranaTactics.Combat
{
    /// <summary>
    /// 実際に削った最大HP比率と致死攻撃の加点から最大貢献ユニットを決めます。
    /// </summary>
    public sealed class DamageContributionTracker
    {
        /// <summary>
        /// 致死攻撃へ加算する貢献ポイントです。
        /// </summary>
        private const double k_LethalHitBonus = 20.0;

        /// <summary>
        /// 実ダメージ割合を百分率のポイントへ変換する倍率です。
        /// </summary>
        private const double k_PercentageScale = 100.0;

        /// <summary>
        /// 貢献ポイントの計算誤差を同点として扱う許容幅です。
        /// </summary>
        private const double k_TieTolerance = 0.0001;

        /// <summary>
        /// 攻撃元ユニットごとの累積貢献です。
        /// </summary>
        private readonly Dictionary<int, double> _contributions = new();

        /// <summary>
        /// 攻撃元ユニットが所属するチームです。
        /// </summary>
        private readonly Dictionary<int, int> _attackerTeams = new();

        /// <summary>
        /// 致死攻撃を行ったユニットの識別子です。
        /// </summary>
        private int _finishingPlayerId = -1;

        /// <summary>
        /// 再生成時に以前の貢献を破棄します。
        /// </summary>
        public void Reset()
        {
            _contributions.Clear();
            _attackerTeams.Clear();
            _finishingPlayerId = -1;
        }

        /// <summary>
        /// 敵チームの有効ダメージだけを最大HPに対する割合で加算します。
        /// </summary>
        /// <param name="actualDamage">
        /// 残りHPを超える分を除いた、実際に削ったHPです。
        /// </param>
        /// <param name="maximumHealth">
        /// 攻撃を受けたユニットの最大HPです。
        /// </param>
        public void Record(
            int attackerPlayerId,
            int attackerTeamId,
            int victimTeamId,
            float actualDamage,
            float maximumHealth,
            bool isLethal)
        {
            if (attackerPlayerId < 0 || attackerTeamId < 0 || attackerTeamId == victimTeamId ||
                actualDamage <= 0.0f || maximumHealth <= 0.0f ||
                float.IsNaN(actualDamage) || float.IsInfinity(actualDamage) ||
                float.IsNaN(maximumHealth) || float.IsInfinity(maximumHealth))
            {
                return;
            }

            double contribution = CalculateContribution(actualDamage, maximumHealth, isLethal);
            if (isLethal)
            {
                _finishingPlayerId = attackerPlayerId;
            }

            _attackerTeams[attackerPlayerId] = attackerTeamId;
            _contributions[attackerPlayerId] =
                (_contributions.TryGetValue(attackerPlayerId, out double previous) ? previous : 0.0) + contribution;
        }

        /// <summary>
        /// 全攻撃者の最大貢献ユニットを選び、その所属チームを返します。
        /// </summary>
        public bool TryGetWinningTeam(out int teamId)
        {
            int playerId = SelectHighestContributor(_contributions, _finishingPlayerId);
            teamId = _attackerTeams.TryGetValue(playerId, out int winnerTeamId) ? winnerTeamId : -1;
            return teamId >= 0;
        }

        /// <summary>
        /// 実ダメージ割合を100倍し、致死攻撃者へ20ポイントを加算します。
        /// </summary>
        internal static double CalculateContribution(float actualDamage, float maximumHealth, bool isLethal)
        {
            return actualDamage / (double)maximumHealth * k_PercentageScale +
                (isLethal ? k_LethalHitBonus : 0.0);
        }

        /// <summary>
        /// 最大貢献者を選び、同点なら致死攻撃者、次に小さいIDを優先します。
        /// </summary>
        internal static int SelectHighestContributor(IReadOnlyDictionary<int, double> contributions, int finishingId)
        {
            int contributorId = -1;
            double maximum = double.NegativeInfinity;
            foreach (KeyValuePair<int, double> entry in contributions)
            {
                maximum = Math.Max(maximum, entry.Value);
            }

            foreach (KeyValuePair<int, double> entry in contributions)
            {
                if (Math.Abs(entry.Value - maximum) > k_TieTolerance)
                {
                    continue;
                }

                if (entry.Key == finishingId)
                {
                    return entry.Key;
                }

                if (contributorId < 0 || entry.Key < contributorId)
                {
                    contributorId = entry.Key;
                }
            }

            return contributorId;
        }
    }
}
