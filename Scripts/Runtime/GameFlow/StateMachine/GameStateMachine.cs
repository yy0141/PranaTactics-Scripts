using System;
using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.GameFlow
{
    /// <summary>
    /// ゲーム進行で使用する状態を管理し、
    /// 状態の切り替えと更新処理を実行します。
    /// </summary>
    public sealed class GameStateMachine : IStateMachine<GameState>
    {
        /// <summary>
        /// 現在有効なゲーム状態です。
        /// </summary>
        private GameState _currentState;

        public GameStateMachine(GameState initialState)
        {
            ChangeState(
                initialState ??
                throw new ArgumentNullException(nameof(initialState)));
        }

        public GameState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        /// <summary>
        /// 現在の状態を終了し、指定された新しい状態へ遷移します。
        /// </summary>
        /// <param name="newState">
        /// 遷移先となるゲーム状態です。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="newState"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void ChangeState(GameState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            if (ReferenceEquals(
                    _currentState,
                    newState))
            {
                return;
            }

            Debug.Log(
                $"{nameof(GameStateMachine)}: " +
                $"{newState.GetType().Name}へ状態を変更します。");

            _currentState?.Exit();

            _currentState = newState;
            _currentState.Enter();
        }

        /// <summary>
        /// 現在有効なゲーム状態の更新処理を実行します。
        /// </summary>
        public void UpdateState()
        {
            _currentState?.UpdateState();
        }
    }
}
