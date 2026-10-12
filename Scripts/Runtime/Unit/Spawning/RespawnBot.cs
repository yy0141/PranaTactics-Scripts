using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 対象ユニットが撃破されたときに、
    /// ローカルキャラクターの再スポーンを要求します。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawnBot : MonoBehaviour
    {
        /// <summary>
        /// 再スポーンを監視するユニットです。
        /// </summary>
        [Header("Target")]
        [SerializeField]
        private UnitBehavior _unitBehavior;

        /// <summary>
        /// 撃破時の再スポーンを有効にするかどうかです。
        /// </summary>
        [Header("Respawn")]
        [SerializeField]
        private bool _canRespawn;

        /// <summary>
        /// 撃破されてから再スポーンするまでの待機時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _respawnDelay = 3.0f;

        /// <summary>
        /// 現在の撃破状態について、
        /// 再スポーン要求を送信済みかどうかです。
        /// </summary>
        private bool _respawnRequested;

        public bool CanRespawn
        {
            get
            {
                return _canRespawn;
            }
        }

        public float RespawnDelay
        {
            get
            {
                return _respawnDelay;
            }
        }

        public bool RespawnRequested
        {
            get
            {
                return _respawnRequested;
            }
        }

        /// <summary>
        /// 対象ユニットを取得します。
        /// </summary>
        private void Awake()
        {
            if (_unitBehavior == null)
            {
                _unitBehavior =
                    GetComponent<UnitBehavior>();
            }
        }

        /// <summary>
        /// コンポーネントが有効になったときに、
        /// 再スポーン要求状態を初期化します。
        /// </summary>
        private void OnEnable()
        {
            _respawnRequested = false;
        }

        /// <summary>
        /// 対象ユニットのHPを監視し、
        /// 撃破時に再スポーンを要求します。
        /// </summary>
        private void Update()
        {
            TryRequestRespawn();
        }

        /// <summary>
        /// Unit削除前にも呼べる再スポーン要求を行います。
        /// </summary>
        /// <returns>
        /// 再スポーンを要求済みの場合は<c>true</c>です。
        /// </returns>
        public bool TryRequestRespawn()
        {
            if (!_canRespawn || _unitBehavior == null)
            {
                return false;
            }

            UnitStatus unitStatus = _unitBehavior.GetUnitStatus();
            if (unitStatus == null)
            {
                return false;
            }

            if (unitStatus.CurrentHealth > 0.0f)
            {
                _respawnRequested = false;
                return false;
            }

            if (!_respawnRequested)
            {
                RequestRespawn(unitStatus);
            }

            return _respawnRequested;
        }

        /// <summary>
        /// 対象ユニットの再スポーンを要求します。
        /// </summary>
        /// <param name="unitStatus">
        /// 再スポーン対象のユニット状態です。
        /// </param>
        private void RequestRespawn(
            UnitStatus unitStatus)
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return;
            }

            if (!unitManager.CanExecuteUnitLogic(
                    unitStatus.PlayerId))
            {
                return;
            }

            UnitParams characterData =
                unitStatus.CharacterData;

            if (characterData == null)
            {
                Debug.LogError(
                    $"{nameof(RespawnBot)}: " +
                    "CharacterDataが設定されていないため、" +
                    "再スポーンできません。",
                    this);

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    characterData.UnitName))
            {
                Debug.LogError(
                    $"{nameof(RespawnBot)}: " +
                    "UnitNameが設定されていないため、" +
                    "再スポーンできません。",
                    this);

                return;
            }

            _respawnRequested = unitManager.RequestLocalRespawn(
                unitStatus.PlayerId,
                characterData.UnitName,
                _respawnDelay);
        }

        /// <summary>
        /// 再スポーンの有効状態を設定します。
        /// </summary>
        /// <param name="canRespawn">
        /// 再スポーンを有効にする場合は<c>true</c>です。
        /// </param>
        public void SetRespawnEnabled(
            bool canRespawn)
        {
            _canRespawn = canRespawn;

            if (!_canRespawn)
            {
                _respawnRequested = false;
            }
        }

        /// <summary>
        /// 再スポーンまでの待機時間を設定します。
        /// </summary>
        /// <param name="respawnDelay">
        /// 再スポーンまでの待機時間です。
        /// </param>
        public void SetRespawnDelay(
            float respawnDelay)
        {
            _respawnDelay = Mathf.Max(
                0.0f,
                respawnDelay);
        }

        /// <summary>
        /// 再スポーン要求済みの状態を解除します。
        /// </summary>
        /// <remarks>
        /// 外部処理で再スポーンに失敗した場合や、
        /// 同じユニットを再利用するときに使用します。
        /// </remarks>
        public void ResetRespawnRequest()
        {
            _respawnRequested = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の待機時間を有効な範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _respawnDelay = Mathf.Max(
                0.0f,
                _respawnDelay);
        }
#endif
    }
}
