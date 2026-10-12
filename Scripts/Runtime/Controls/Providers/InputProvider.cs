using System;
using UnityEngine;

namespace PranaTactics.Controls
{
    /// <summary>
    /// 現在使用している入力ソースから入力状態を取得し、
    /// ゲーム内の各処理へ提供します。
    /// </summary>
    public sealed class InputProvider : MonoBehaviour
    {
        public static InputProvider Instance
        {
            get; private set;
        }

        /// <summary>
        /// 現在の入力状態を提供する入力ソースです。
        /// </summary>
        private IUnitInput _currentInput;

        /// <summary>
        /// 最後に取得されたユニット入力状態です。
        /// </summary>
        private UnitInputState _currentState;

        public UnitInputState CurrentState
        {
            get
            {
                return _currentState;
            }
        }

        /// <summary>
        /// シングルトンインスタンスを設定し、
        /// 既定のプレイヤー入力を初期化します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;
            _currentInput = new PlayerUnitInput();
        }

        /// <summary>
        /// このインスタンスが破棄されたとき、
        /// シングルトンに保持されている参照を解除します。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 現在の入力ソースから最新の入力状態を取得します。
        /// </summary>
        private void Update()
        {
            if (_currentInput == null)
            {
                return;
            }

            _currentState = _currentInput.CurrentState;
        }

        /// <summary>
        /// 入力状態の取得に使用する入力ソースを変更します。
        /// </summary>
        /// <param name="input">
        /// 新しく使用する入力ソースです。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="input"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void SetInputSource(IUnitInput input)
        {
            _currentInput =
                input ?? throw new ArgumentNullException(nameof(input));

            _currentState = _currentInput.CurrentState;
        }
    }
}
