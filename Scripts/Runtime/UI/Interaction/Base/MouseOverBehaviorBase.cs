using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのマウスオーバー状態における処理を実装する
    /// MonoBehaviourの基底クラスです。
    /// </summary>
    /// <remarks>
    /// <see cref="IMouseOver"/>を明示的に実装しているため、
    /// マウスオーバー中の更新処理はUnityのメッセージとしてではなく、
    /// <see cref="MouseOverState"/>を通してのみ実行されます。
    /// </remarks>
    public abstract class MouseOverBehaviorBase :
        MonoBehaviour,
        IMouseOver
    {
        /// <summary>
        /// 現在マウスオーバー状態であるかどうかを示します。
        /// </summary>
        private bool _isMouseOver;

        protected bool IsMouseOver
        {
            get
            {
                return _isMouseOver;
            }
        }

        /// <summary>
        /// マウスオーバー状態へ遷移したときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// 色、サイズ、表示内容、アニメーションなどを
        /// マウスオーバー用の状態へ変更する処理を実装します。
        /// </remarks>
        protected virtual void OnMouseOverEnter()
        {
        }

        /// <summary>
        /// マウスオーバー状態である間、
        /// 状態機械の更新時に呼び出されます。
        /// </summary>
        /// <remarks>
        /// マウスオーバー中に継続して実行する処理がない場合は、
        /// オーバーライドする必要はありません。
        /// </remarks>
        protected virtual void OnMouseOverUpdate()
        {
        }

        /// <summary>
        /// マウスオーバー状態が終了したときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// 色、サイズ、表示内容、アニメーションなどを
        ///通常の状態へ戻す処理を実装します。
        /// </remarks>
        protected virtual void OnMouseOverExit()
        {
        }

        /// <summary>
        /// マウスオーバー状態の開始処理を実行します。
        /// </summary>
        void IMouseOver.Enter()
        {
            if (!isActiveAndEnabled ||
                _isMouseOver)
            {
                return;
            }

            _isMouseOver = true;

            OnMouseOverEnter();
        }

        /// <summary>
        /// マウスオーバー状態の更新処理を実行します。
        /// </summary>
        void IMouseOver.Update()
        {
            if (!isActiveAndEnabled ||
                !_isMouseOver)
            {
                return;
            }

            OnMouseOverUpdate();
        }

        /// <summary>
        /// マウスオーバー状態の終了処理を実行します。
        /// </summary>
        void IMouseOver.Exit()
        {
            if (!_isMouseOver)
            {
                return;
            }

            _isMouseOver = false;

            OnMouseOverExit();
        }

        /// <summary>
        /// コンポーネントが無効になった場合に、
        /// マウスオーバー状態を解除します。
        /// </summary>
        protected virtual void OnDisable()
        {
            if (!_isMouseOver)
            {
                return;
            }

            _isMouseOver = false;

            OnMouseOverExit();
        }
    }
}
