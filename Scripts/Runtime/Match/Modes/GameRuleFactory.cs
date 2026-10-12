using UnityEngine;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// Inspector上の設定から試合ごとに独立したルールStrategyを生成します。
    /// </summary>
    public abstract class GameRuleFactory : ScriptableObject
    {
        /// <summary>
        /// 設定に対応した新しい試合ルールを生成します。
        /// </summary>
        public abstract IMatchRule CreateRule();
    }
}
