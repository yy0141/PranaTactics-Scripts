using UnityEngine;

namespace PranaTactics.Controls
{
    /// <summary>
    /// プレイヤー入力の更新と、現在のユニット入力状態の提供を管理します。
    /// </summary>
    public sealed class InputManager : MonoBehaviour
    {
        public static InputManager Instance
        {
            get; private set;
        }

        public PlayerInputContext InputContext
        {
            get
            {
                return _inputContext;
            }
        }

        public UnitInputState CurrentState
        {
            get
            {
                if (_inputContext == null)
                {
                    return default;
                }

                return _inputContext.CurrentState;
            }
        }

        /// <summary>
        /// プレイヤーまたはAIの入力状態を管理するコンテキストです。
        /// </summary>
        private PlayerInputContext _inputContext;

        /// <summary>
        /// シングルトンインスタンスを設定し、
        /// プレイヤー入力コンテキストを初期化します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;

            _inputContext = new PlayerInputContext(
                new PlayerUnitInput());
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
        /// 現在設定されている入力ソースから
        /// 最新のユニット入力状態を取得します。
        /// </summary>
        private void Update()
        {
            if (_inputContext == null)
            {
                return;
            }

            _inputContext.UpdateState();
        }

        /// <summary>
        /// 入力状態を取得する入力ソースを変更します。
        /// </summary>
        /// <param name="input">
        /// 新しく使用する入力ソースです。
        /// </param>
        public void SetInputSource(IUnitInput input)
        {
            if (_inputContext == null)
            {
                _inputContext = new PlayerInputContext(input);

                return;
            }

            _inputContext.SetInputSource(input);
        }
    }
}
