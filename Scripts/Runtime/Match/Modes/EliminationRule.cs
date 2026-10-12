using System;
using System.Collections.Generic;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 既存の撃破スコア、生存チーム数による終了条件と同点順位を維持します。
    /// </summary>
    public sealed class EliminationRule : IMatchRule
    {
        public int DefeatScore => 2;

        /// <summary>
        /// 複数人の試合で最後の一チームが残ったか確認します。
        /// </summary>
        public bool ShouldFinish(MatchRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.HasMultiplePlayers && context.AliveTeamCount == 1;
        }

        /// <summary>
        /// プレイヤー退出後の既存の即時終了条件を判定します。
        /// </summary>
        public bool ShouldFinishAfterDeparture(MatchRuleContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.AliveTeamCount <= 1;
        }

        /// <summary>
        /// 退出による終了時は生存チームを一位、それ以外を二位とします。
        /// </summary>
        public int CalculateDepartureRank(MatchRuleContext context, int teamId)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.Survivors.TryGetValue(teamId, out int survivors) && survivors > 0 ? 1 : 2;
        }

        /// <summary>
        /// スコアの降順で順位を決め、同点には同じ順位を割り当てます。
        /// </summary>
        /// <remarks>
        /// 既存処理と同じく、同点の次の順位は参加チーム数に応じて飛びます。
        /// </remarks>
        public IReadOnlyDictionary<int, int> CalculateRanks(IReadOnlyDictionary<int, int> scores)
        {
            if (scores == null)
            {
                throw new ArgumentNullException(nameof(scores));
            }

            var sortedTeams = new List<KeyValuePair<int, int>>(scores);
            sortedTeams.Sort((left, right) => right.Value.CompareTo(left.Value));
            var ranks = new Dictionary<int, int>();
            int rank = 1;
            for (int index = 0; index < sortedTeams.Count; index++)
            {
                if (index > 0 && sortedTeams[index].Value < sortedTeams[index - 1].Value)
                {
                    rank = index + 1;
                }

                ranks.Add(sortedTeams[index].Key, rank);
            }

            return new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(ranks);
        }
    }
}
