using UnityEngine;
using UnityEngine.Serialization;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 近接攻撃プラーナの当たり判定をローカルZ軸周りに回転させ、
    /// 指定角度まで回転した時点でプラーナの破棄を要求します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SlashCollisionRotator : MonoBehaviour
    {
        /// <summary>
        /// 0秒として扱う最小回転時間です。
        /// </summary>
        private const float k_MinimumDuration = 0.0001f;

        /// <summary>
        /// 回転完了後に破棄する近接攻撃プラーナです。
        /// </summary>
        [SerializeField]
        private AttackerPranaBase _attackerPrana;

        /// <summary>
        /// 回転開始から完了までにかける時間です。
        /// </summary>
        [Header("Rotation")]
        [Min(0.0f)]
        [SerializeField]
        private float _rotationDuration = 0.5f;

        /// <summary>
        /// ローカルZ軸周りに回転する合計角度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _rotationAngle = 180.0f;

        /// <summary>
        /// 現在までに回転した角度です。
        /// </summary>
        private float _currentAngle;

        /// <summary>
        /// 回転完了後の破棄処理を実行済みかどうかを示します。
        /// </summary>
        private bool _hasCompleted;

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 回転状態を初期化します。
        /// </summary>
        private void OnEnable()
        {
            _currentAngle = 0.0f;
            _hasCompleted = false;
        }

        /// <summary>
        /// 指定された時間と角度に従って、
        /// 当たり判定をローカルZ軸周りに回転させます。
        /// </summary>
        private void Update()
        {
            if (_hasCompleted)
            {
                return;
            }

            if (_rotationAngle <= 0.0f)
            {
                CompleteRotation();

                return;
            }

            if (_rotationDuration <=
                k_MinimumDuration)
            {
                RotateBy(
                    _rotationAngle -
                    _currentAngle);

                CompleteRotation();

                return;
            }

            float rotationSpeed =
                _rotationAngle /
                _rotationDuration;

            float remainingAngle =
                _rotationAngle -
                _currentAngle;

            float deltaAngle =
                Mathf.Min(
                    rotationSpeed *
                    Time.deltaTime,
                    remainingAngle);

            RotateBy(
                deltaAngle);

            _currentAngle +=
                deltaAngle;

            if (_currentAngle >=
                _rotationAngle)
            {
                CompleteRotation();
            }
        }

        /// <summary>
        /// 指定された角度だけローカルZ軸周りに回転します。
        /// </summary>
        /// <param name="angle">
        /// 今回のフレームで回転する角度です。
        /// </param>
        private void RotateBy(
            float angle)
        {
            if (angle <= 0.0f)
            {
                return;
            }

            transform.Rotate(
                0.0f,
                0.0f,
                angle,
                Space.Self);
        }

        /// <summary>
        /// 回転を完了状態へ変更し、
        /// 近接攻撃プラーナの破棄を要求します。
        /// </summary>
        private void CompleteRotation()
        {
            if (_hasCompleted)
            {
                return;
            }

            _hasCompleted = true;
            _currentAngle = _rotationAngle;

            if (_attackerPrana == null)
            {
                Debug.LogWarning(
                    $"{nameof(SlashCollisionRotator)}: " +
                    $"{nameof(AttackerPranaBase)}が設定されていないため、" +
                    "プラーナを破棄できません。",
                    this);

                return;
            }

            _attackerPrana.DestroyPrana();
        }

        /// <summary>
        /// 近接攻撃プラーナへの参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            if (_attackerPrana != null)
            {
                return;
            }

            _attackerPrana =
                GetComponentInParent<AttackerPranaBase>();

            if (_attackerPrana == null)
            {
                Debug.LogError(
                    $"{nameof(SlashCollisionRotator)}: " +
                    $"{nameof(AttackerPranaBase)}を取得できません。",
                    this);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _rotationDuration =
                Mathf.Max(
                0.0f,
                _rotationDuration);

            _rotationAngle =
                Mathf.Max(
                    0.0f,
                    _rotationAngle);

            if (_attackerPrana == null)
            {
                _attackerPrana =
                    GetComponentInParent<AttackerPranaBase>();
            }
        }
#endif
    }
}
