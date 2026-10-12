using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Combat
{
    /// <summary>
    /// 指定されたGameObjectが持つ<see cref="IDamageable"/>へ、
    /// プレイヤーIDの取得とダメージ処理を転送します。
    /// </summary>
    public sealed class DamageableProxy : MonoBehaviour, IDamageable
    {
        /// <summary>
        /// 有効なダメージ対象が設定されていない場合に返す
        /// 無効なプレイヤーIDです。
        /// </summary>
        private const int k_InvalidPlayerId = -1;

        /// <summary>
        /// ダメージ処理の転送先となるGameObjectです。
        /// </summary>
        [SerializeField]
        private GameObject _targetObject;

        /// <summary>
        /// ダメージ処理の転送先となるコンポーネントです。
        /// </summary>
        private IDamageable _damageable;

        public int PlayerId
        {
            get
            {
                if (_damageable != null)
                {
                    return _damageable.PlayerId;
                }

                Debug.LogWarning(
                    $"{nameof(DamageableProxy)}に" +
                    $"{nameof(IDamageable)}が設定されていません。",
                    this);

                return k_InvalidPlayerId;
            }
        }

        /// <summary>
        /// ダメージ処理の転送先となる
        /// <see cref="IDamageable"/>を取得します。
        /// </summary>
        private void Awake()
        {
            InitializeDamageable();
        }

        /// <summary>
        /// 指定された攻撃元から受けたダメージを、
        /// 設定されているダメージ対象へ適用します。
        /// </summary>
        /// <param name="attackerPlayerId">
        /// ダメージを与えたプレイヤーの識別子です。
        /// </param>
        /// <param name="damage">
        /// 適用するダメージ量です。
        /// </param>
        /// <param name="playAnimation">
        /// ダメージアニメーションを再生する場合は<c>true</c>、
        /// 再生しない場合は<c>false</c>です。
        /// </param>
        public void ApplyDamage(
            int attackerPlayerId,
            float damage,
            bool playAnimation = true)
        {
            if (_damageable == null)
            {
                Debug.LogWarning(
                    $"{nameof(IDamageable)}が設定されていないため、" +
                    "ダメージを適用できません。",
                    this);

                return;
            }

            _damageable.ApplyDamage(
                attackerPlayerId,
                damage,
                playAnimation);
        }

        /// <summary>
        /// 転送先のGameObjectから
        /// <see cref="IDamageable"/>を取得します。
        /// </summary>
        private void InitializeDamageable()
        {
            if (_targetObject == null)
            {
                Debug.LogError(
                    $"{nameof(DamageableProxy)}の転送先となる" +
                    "GameObjectが設定されていません。",
                    this);

                return;
            }

            _damageable = _targetObject.GetComponent<IDamageable>();

            if (_damageable == null)
            {
                Debug.LogError(
                    $"{_targetObject.name}に" +
                    $"{nameof(IDamageable)}を実装したコンポーネントが" +
                    "存在しません。",
                    _targetObject);
            }
        }
    }
}
