using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// ルールに試合の読み取り専用情報を提供し、Managerへの依存を防ぎます。
    /// </summary>
    public sealed class MatchRuleContext
    {
        public IReadOnlyDictionary<int, int> Scores { get; }
        public IReadOnlyDictionary<int, int> Survivors { get; }
        public int AliveTeamCount { get; private set; }
        public bool HasMultiplePlayers { get; private set; }

        public MatchRuleContext(IDictionary<int, int> scores, IDictionary<int, int> survivors)
        {
            Scores = new ReadOnlyDictionary<int, int>(scores ?? throw new ArgumentNullException(nameof(scores)));
            Survivors = new ReadOnlyDictionary<int, int>(survivors ?? throw new ArgumentNullException(nameof(survivors)));
        }

        /// <summary>
        /// 判定前に最新の参加条件を反映します。
        /// </summary>
        public void Refresh(int aliveTeamCount, bool hasMultiplePlayers)
        {
            if (aliveTeamCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(aliveTeamCount));
            }

            AliveTeamCount = aliveTeamCount;
            HasMultiplePlayers = hasMultiplePlayers;
        }
    }
}
