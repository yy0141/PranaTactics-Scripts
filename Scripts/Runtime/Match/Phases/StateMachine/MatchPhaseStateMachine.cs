using System;
using UnityEngine;

using PranaTactics.Core;

namespace PranaTactics.Match.Phases
{
    /// <summary>
    /// 試合フェイズの現在状態を保持し、
    /// フェイズ間の遷移と更新処理を管理します。
    /// </summary>
    public sealed class MatchPhaseStateMachine :
    IStateMachine<MatchPhaseState>
    {
        /// <summary>
        /// 現在適用されている試合フェイズ状態です。
        /// </summary>
        private MatchPhaseState _currentState;

        public MatchPhaseStateMachine(
    MatchPhaseState initialState)
        {
            if (initialState == null)
            {
                throw new ArgumentNullException(
                    nameof(initialState));
            }

            ChangeState(initialState);
        }

        public MatchPhaseState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        /// <summary>
        /// 現在のフェイズを終了し、
        /// 指定された新しいフェイズへ遷移します。
        /// </summary>
        /// <param name="newState">
        /// 遷移先となる試合フェイズ状態です。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="newState"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void ChangeState(
            MatchPhaseState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(
                    nameof(newState));
            }

            if (ReferenceEquals(
                    _currentState,
                    newState))
            {
                return;
            }

            Debug.Log(
                $"{nameof(MatchPhaseStateMachine)}: " +
                $"フェイズを変更します。 " +
                $"Previous={_currentState?.GetType().Name ?? "None"}, " +
                $"Next={newState.GetType().Name}");

            _currentState?.Exit();

            _currentState = newState;
            _currentState.Enter();
        }

        /// <summary>
        /// 現在適用されている試合フェイズ状態を更新します。
        /// </summary>
        public void UpdateState()
        {
            _currentState?.UpdateState();
        }
    }
}
