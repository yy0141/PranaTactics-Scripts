using System;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのマウスオーバー状態を管理し、
    /// 状態処理を<see cref="IMouseOver"/>へ委譲します。
    /// </summary>
    public sealed class MouseOverState : UIState
    {
        /// <summary>
        /// この状態が適用されるUIオブジェクトです。
        /// </summary>
        private readonly GameObject _uiObject;

        /// <summary>
        /// マウスオーバー状態の開始、更新および終了処理を実行します。
        /// </summary>
        private readonly IMouseOver _mouseOverHandler;

        public MouseOverState(
    GameObject uiObject,
    IMouseOver mouseOverHandler)
        {
            _uiObject =
                uiObject != null
                    ? uiObject
                    : throw new ArgumentNullException(
                        nameof(uiObject));

            _mouseOverHandler =
                mouseOverHandler ??
                throw new ArgumentNullException(
                    nameof(mouseOverHandler));
        }

        public GameObject UIObject
        {
            get
            {
                return _uiObject;
            }
        }

        /// <summary>
        /// マウスオーバー状態へ遷移したときに呼び出されます。
        /// </summary>
        public override void Enter()
        {
            _mouseOverHandler.Enter();
        }

        /// <summary>
        /// マウスオーバー状態が有効な間、毎フレーム呼び出されます。
        /// </summary>
        public override void Update()
        {
            _mouseOverHandler.Update();
        }

        /// <summary>
        /// マウスオーバー状態から別の状態へ遷移するときに呼び出されます。
        /// </summary>
        public override void Exit()
        {
            _mouseOverHandler.Exit();
        }
    }
}
