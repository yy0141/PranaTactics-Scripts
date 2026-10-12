using PranaTactics.Core;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトへ適用される状態の基底クラスです。
    /// </summary>
    /// <remarks>
    /// <see cref="UIStateMachine"/>によって管理され、
    /// 状態の開始時に<see cref="Enter"/>、
    /// 更新時に<see cref="Update"/>、
    /// 終了時に<see cref="Exit"/>が呼び出されます。
    /// </remarks>
    public abstract class UIState : IState
    {
        /// <summary>
        /// この状態へ遷移したときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// UI表示の変更、入力受付の開始、イベント登録など、
        /// 状態開始時に一度だけ必要な処理を実装します。
        /// </remarks>
        public abstract void Enter();

        /// <summary>
        /// この状態が有効な間、状態機械から更新時に呼び出されます。
        /// </summary>
        /// <remarks>
        /// 毎フレームの処理が不要な状態では、
        /// オーバーライドする必要はありません。
        /// </remarks>
        public virtual void Update()
        {
        }

        /// <summary>
        /// この状態から別の状態へ遷移するときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// UI表示の復元、入力受付の停止、イベント解除など、
        /// 状態終了時に必要な後処理を実装します。
        /// </remarks>
        public abstract void Exit();
    }
}
