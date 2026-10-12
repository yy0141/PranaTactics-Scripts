using System;
using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトの待機状態を管理し、
    /// 状態処理を<see cref="IIdle"/>へ委譲します。
    /// </summary>
    public sealed class IdleState : UIState
    {
        /// <summary>
        /// この状態が適用されるUIオブジェクトです。
        /// </summary>
        private readonly GameObject _uiObject;

        /// <summary>
        /// 待機状態の開始、更新および終了処理を実行します。
        /// </summary>
        private readonly IIdle _idleHandler;

        public IdleState(
    GameObject uiObject,
    IIdle idleHandler)
        {
            _uiObject =
                uiObject != null
                    ? uiObject
                    : throw new ArgumentNullException(
                        nameof(uiObject));

            _idleHandler =
                idleHandler ??
                throw new ArgumentNullException(
                    nameof(idleHandler));
        }

        public GameObject UIObject
        {
            get
            {
                return _uiObject;
            }
        }

        /// <summary>
        /// 待機状態へ遷移したときに呼び出されます。
        /// </summary>
        public override void Enter()
        {
            _idleHandler.Enter();
        }

        /// <summary>
        /// 待機状態が有効な間、毎フレーム呼び出されます。
        /// </summary>
        public override void Update()
        {
            _idleHandler.Update();
        }

        /// <summary>
        /// 待機状態から別の状態へ遷移するときに呼び出されます。
        /// </summary>
        public override void Exit()
        {
            _idleHandler.Exit();
        }
    }
}
