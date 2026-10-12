using System.Collections.Generic;

namespace PranaTactics.Match.Modes
{
    public interface IMatchRule
    {
        int DefeatScore { get; }

        /// <summary>
        /// 最新の試合情報から通常の終了条件を副作用なしで判定します。
        /// </summary>
        bool ShouldFinish(MatchRuleContext context);

        /// <summary>
        /// プレイヤー退出後に即時終了するか判定します。
        /// </summary>
        bool ShouldFinishAfterDeparture(MatchRuleContext context);

        /// <summary>
        /// 退出によって終了した試合の指定チームの順位を決めます。
        /// </summary>
        int CalculateDepartureRank(MatchRuleContext context, int teamId);

        /// <summary>
        /// 通常終了とターン制限終了で使用する各チームの順位を決めます。
        /// </summary>
        IReadOnlyDictionary<int, int> CalculateRanks(IReadOnlyDictionary<int, int> scores);
    }
}
