using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.UI.Interaction
{
    /// <summary>
    /// UIに適用される状態の切り替えと更新を管理します。
    /// </summary>
    public sealed class UIStateMachine : IStateMachine<UIState>
    {
        /// <summary>
        /// 現在適用されているUI状態です。
        /// </summary>
        private UIState _currentState;

        public UIState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        public bool HasCurrentState
        {
            get
            {
                return _currentState != null;
            }
        }

        /// <summary>
        /// 現在のUI状態を終了し、新しい状態へ切り替えます。
        /// </summary>
        /// <param name="newState">
        /// 新しく適用するUI状態です。
        /// </param>
        public void ChangeState(
            UIState newState)
        {
            if (newState == null)
            {
                Debug.LogError(
                    $"{nameof(UIStateMachine)}: " +
                    "切り替え先のUI状態がnullです。");

                return;
            }

            if (ReferenceEquals(
                    _currentState,
                    newState))
            {
                return;
            }

            UIState previousState =
                _currentState;

            previousState?.Exit();

            _currentState =
                newState;

            _currentState.Enter();

            Debug.Log(
                $"{nameof(UIStateMachine)}: " +
                $"状態を変更しました。" +
                $" Previous={previousState?.GetType().Name ?? "None"}," +
                $" Current={_currentState.GetType().Name}");
        }

        /// <summary>
        /// 現在のUI状態を更新します。
        /// </summary>
        public void UpdateState()
        {
            _currentState?.Update();
        }

        /// <summary>
        /// 現在のUI状態を終了し、
        /// 状態が設定されていない状態へ戻します。
        /// </summary>
        public void ClearState()
        {
            if (_currentState == null)
            {
                return;
            }

            UIState previousState =
                _currentState;

            _currentState = null;

            previousState.Exit();
        }

        /// <summary>
        /// 現在の状態が指定された型であるかどうかを判定します。
        /// </summary>
        /// <typeparam name="TState">
        /// 判定するUI状態の型です。
        /// </typeparam>
        /// <returns>
        /// 現在の状態が指定された型の場合は<c>true</c>です。
        /// </returns>
        public bool IsCurrentState<TState>()
            where TState : UIState
        {
            return _currentState is TState;
        }
    }
}
