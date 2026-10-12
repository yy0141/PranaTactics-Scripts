using System;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトのクリック状態を管理し、
    /// 状態処理を<see cref="IClick"/>へ委譲します。
    /// </summary>
    public sealed class ClickState : UIState
    {
        /// <summary>
        /// この状態が適用されるUIオブジェクトです。
        /// </summary>
        private readonly GameObject _uiObject;

        /// <summary>
        /// クリック状態の開始、更新および終了処理を実行します。
        /// </summary>
        private readonly IClick _clickHandler;

        public ClickState(
    GameObject uiObject,
    IClick clickHandler)
        {
            _uiObject =
                uiObject != null
                    ? uiObject
                    : throw new ArgumentNullException(
                        nameof(uiObject));

            _clickHandler =
                clickHandler ??
                throw new ArgumentNullException(
                    nameof(clickHandler));
        }

        public GameObject UIObject
        {
            get
            {
                return _uiObject;
            }
        }

        /// <summary>
        /// クリック状態へ遷移したときに呼び出されます。
        /// </summary>
        public override void Enter()
        {
            _clickHandler.Enter();
        }

        /// <summary>
        /// クリック状態が有効な間、毎フレーム呼び出されます。
        /// </summary>
        public override void Update()
        {
            _clickHandler.Update();
        }

        /// <summary>
        /// クリック状態から別の状態へ遷移するときに呼び出されます。
        /// </summary>
        public override void Exit()
        {
            _clickHandler.Exit();
        }
    }
}
