using PranaTactics.Core;

namespace PranaTactics.GameFlow
{
    /// <summary>
    /// ゲーム進行における各状態の基底クラスです。
    /// </summary>
    public abstract class GameState : IState
    {
        /// <summary>
        /// この状態へ遷移したときの初期化処理を実行します。
        /// </summary>
        public abstract void Enter();

        /// <summary>
        /// この状態が有効な間に必要な更新処理を実行します。
        /// </summary>
        /// <remarks>
        /// 更新処理が不要な状態では、オーバーライドする必要はありません。
        /// </remarks>
        public virtual void UpdateState()
        {
        }

        /// <summary>
        /// この状態から別の状態へ遷移するときの終了処理を実行します。
        /// </summary>
        public abstract void Exit();
    }
}
