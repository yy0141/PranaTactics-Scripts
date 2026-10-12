using UnityEngine;

namespace PranaTactics.Match.Modes
{
    /// <summary>
    /// 既存の殲滅ルールを生成します。
    /// </summary>
    [CreateAssetMenu(menuName = "Prana Tactics/Game Modes/Elimination Rule")]
    public sealed class EliminationRuleFactory : GameRuleFactory
    {
        /// <summary>
        /// 状態を共有しない殲滅ルールを生成します。
        /// </summary>
        public override IMatchRule CreateRule()
        {
            return new EliminationRule();
        }
    }
}
