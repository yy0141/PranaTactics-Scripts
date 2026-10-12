using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Match
{
    /// <summary>
    /// 現在のゲームで使用するルール設定を管理します。
    /// </summary>
    public sealed class GameRuleManager : MonoBehaviour
    {
        public static GameRuleManager Instance
        {
            get; private set;
        }

        /// <summary>
        /// 現在のゲームで使用するルール設定です。
        /// </summary>
        [SerializeField]
        private GameRuleData _currentRule;

        public GameRuleData CurrentRule
        {
            get
            {
                return _currentRule;
            }
        }

        /// <summary>
        /// シングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);

                return;
            }

            Instance = this;

            if (_currentRule == null)
            {
                Debug.LogWarning(
                    $"{nameof(GameRuleManager)}に" +
                    $"{nameof(GameRuleData)}が設定されていません。",
                    this);
            }
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
    }
}
