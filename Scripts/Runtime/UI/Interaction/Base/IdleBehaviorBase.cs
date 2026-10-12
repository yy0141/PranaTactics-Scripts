using UnityEngine;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIオブジェクトの待機状態における処理を実装する
    /// MonoBehaviourの基底クラスです。
    /// </summary>
    /// <remarks>
    /// 初期化時に自身を<see cref="IdleState"/>でラップし、
    /// <see cref="UIManager"/>へ登録します。
    ///
    /// <see cref="IIdle"/>は明示的に実装しているため、
    /// 待機中の更新処理はUnityのメッセージとしてではなく、
    /// <see cref="UIStateMachine.UpdateState"/>からのみ実行されます。
    /// </remarks>
    public abstract class IdleBehaviorBase :
        MonoBehaviour,
        IIdle
    {
        /// <summary>
        /// 待機状態の処理を実行できるかどうかを示します。
        /// </summary>
        private bool _canIdle;

        protected bool CanIdle
        {
            get
            {
                return _canIdle;
            }
            set
            {
                _canIdle = value;
            }
        }

        /// <summary>
        /// このコンポーネントに対応する待機状態です。
        /// </summary>
        private IdleState _idleState;

        /// <summary>
        /// UIManagerへの登録が完了しているかどうかを示します。
        /// </summary>
        private bool _isRegistered;

        /// <summary>
        /// UIManagerへ待機状態を登録します。
        /// </summary>
        protected virtual void Start()
        {
            RegisterIdleState();
        }

        /// <summary>
        /// コンポーネントの破棄時に、
        /// UIManagerから登録情報を解除します。
        /// </summary>
        protected virtual void OnDestroy()
        {
            UnregisterIdleState();
        }

        /// <summary>
        /// 待機状態へ遷移したときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラスは、待機表示への変更やアニメーション開始などを
        /// このメソッドへ実装します。
        /// </remarks>
        protected virtual void OnIdleEnter()
        {
            _canIdle = true;
        }

        /// <summary>
        /// 待機状態が有効な間、状態機械から呼び出されます。
        /// </summary>
        /// <remarks>
        /// 毎フレームの処理が不要な場合は、
        /// オーバーライドする必要はありません。
        /// </remarks>
        protected virtual void OnIdleUpdate()
        {
        }

        /// <summary>
        /// 待機状態から別の状態へ遷移するときに呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラスは、待機表示の解除やアニメーション停止などを
        /// このメソッドへ実装します。
        /// </remarks>
        protected virtual void OnIdleExit()
        {
            _canIdle = false;
        }

        /// <summary>
        /// 待機状態の開始処理を実行します。
        /// </summary>
        void IIdle.Enter()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            OnIdleEnter();
        }

        /// <summary>
        /// 待機状態の更新処理を実行します。
        /// </summary>
        void IIdle.Update()
        {
            if (!isActiveAndEnabled ||
                !_canIdle)
            {
                return;
            }

            OnIdleUpdate();
        }

        /// <summary>
        /// 待機状態の終了処理を実行します。
        /// </summary>
        void IIdle.Exit()
        {
            OnIdleExit();
        }

        /// <summary>
        /// このUIオブジェクトの待機状態をUIManagerへ登録します。
        /// </summary>
        private void RegisterIdleState()
        {
            if (_isRegistered)
            {
                return;
            }

            UIManager uiManager =
                UIManager.Instance;

            if (uiManager == null)
            {
                Debug.LogError(
                    $"{nameof(IdleBehaviorBase)}: " +
                    $"{nameof(UIManager)}が存在しないため、" +
                    $"{gameObject.name}の待機状態を登録できません。",
                    this);

                return;
            }

            _idleState =
                new IdleState(
                    gameObject,
                    this);

            uiManager.RegisterUIObject(
                gameObject,
                _idleState);

            _isRegistered = true;
        }

        /// <summary>
        /// このUIオブジェクトの登録情報をUIManagerから解除します。
        /// </summary>
        private void UnregisterIdleState()
        {
            if (!_isRegistered)
            {
                return;
            }

            UIManager uiManager =
                UIManager.Instance;

            if (uiManager != null)
            {
                UIStateMachine stateMachine =
                    uiManager.GetUIStateMachine(
                        gameObject);

                if (stateMachine != null &&
                    ReferenceEquals(
                        stateMachine.CurrentState,
                        _idleState))
                {
                    stateMachine.ClearState();
                }

                uiManager.UnregisterUI(
                    gameObject);
            }

            _idleState = null;
            _isRegistered = false;
        }
    }
}
