using PranaTactics.Core;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// 試合中の各フェイズで実行する状態処理の基底クラスです。
    /// </summary>
    public abstract class MatchPhaseState : IState
    {
        /// <summary>
        /// このフェイズへ遷移したときの初期化処理を実行します。
        /// </summary>
        public abstract void Enter();

        /// <summary>
        /// このフェイズ中のフレーム更新処理を実行します。
        /// </summary>
        /// <remarks>
        /// 更新処理が不要なフェイズでは、
        /// このメソッドをオーバーライドする必要はありません。
        /// </remarks>
        public virtual void UpdateState()
        {
        }

        /// <summary>
        /// このフェイズから別のフェイズへ遷移するときの
        /// 終了処理を実行します。
        /// </summary>
        public abstract void Exit();
    }
}
