using System;
using System.Collections.Generic;

namespace PranaTactics.Match.Solo
{
    /// <summary>
    /// 終了時に生存人数が最多のチームへ2ポイントを加算します。
    /// </summary>
    public static class SoloSurvivalBonus
    {
        /// <summary>
        /// 生存者が存在する同率最多チームすべてへボーナスを加算します。
        /// </summary>
        public static void Apply(IDictionary<int, int> scores, IReadOnlyDictionary<int, int> survivors)
        {
            if (scores == null || survivors == null)
            {
                throw new ArgumentNullException(scores == null ? nameof(scores) : nameof(survivors));
            }

            int maximum = 0;
            foreach (int count in survivors.Values)
            {
                maximum = Math.Max(maximum, count);
            }

            foreach (KeyValuePair<int, int> team in survivors)
            {
                if (maximum > 0 && team.Value == maximum && scores.ContainsKey(team.Key))
                {
                    scores[team.Key] += 2;
                }
            }
        }
    }
}
