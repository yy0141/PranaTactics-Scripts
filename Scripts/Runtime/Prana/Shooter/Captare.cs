using UnityEngine;

using PranaTactics.Match.Phases;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 対象へ向かって一定角速度で旋回する
    /// 追尾型の射出プラーナです。
    /// </summary>
    /// <remarks>
    /// 散開を使用する場合は、散開待機、散開移動、
    /// 照準待機を経てから追尾飛行を開始します。
    ///
    /// 一定時間ごとに対象方向を再計算し、
    /// 前回採用した追尾方向との差が制限角度以内である場合のみ
    /// 新しい方向への追尾を継続します。
    /// </remarks>
    public sealed class Captare : ShooterPranaBase
    {
        /// <summary>
        /// 有効な方向ベクトルと判断する最小二乗長です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude =
            0.0001f;

        /// <summary>
        /// プラーナが自動消滅するまでの時間です。
        /// </summary>
        private const float k_MaximumLifeTime =
            15.0f;

        private enum FlightState
        {
            ScatterDelay,

            Scattering,

            Aiming,

            Tracking,

            Finished
        }

        /// <summary>
        /// 1秒間に旋回できる最大角度です。
        /// </summary>
        [Header("Tracking")]
        [Min(0.0f)]
        [SerializeField]
        private float _turnSpeed =
            240.0f;

        /// <summary>
        /// 前回採用した追尾方向から、
        /// 新しい追尾方向を受け入れられる最大角度です。
        /// </summary>
        [Range(0.0f, 180.0f)]
        [SerializeField]
        private float _trackingAngleLimit =
            120.0f;

        /// <summary>
        /// 対象方向を再計算する時間間隔です。
        /// </summary>
        [Min(0.01f)]
        [SerializeField]
        private float _trackingInterval =
            0.05f;

        /// <summary>
        /// 前回追尾方向を更新してからの経過時間です。
        /// </summary>
        private float _trackingTimer;

        /// <summary>
        /// プラーナ生成後の総経過時間です。
        /// </summary>
        private float _lifeTime;

        /// <summary>
        /// 現在採用している追尾先の方向です。
        /// </summary>
        private Vector3 _targetDirection =
            Vector3.zero;

        /// <summary>
        /// 現在、対象の追尾を継続しているかどうかを示します。
        /// </summary>
        private bool _isTracking;

        /// <summary>
        /// 初回の進行方向を設定済みかどうかを示します。
        /// </summary>
        private bool _hasInitialDirection;

        /// <summary>
        /// 現在の飛行段階です。
        /// </summary>
        private FlightState _flightState =
            FlightState.ScatterDelay;

        /// <summary>
        /// Shooter系プラーナの共通初期化完了後に、
        /// Captare固有の散開位置と追尾状態を設定します。
        /// </summary>
        protected override void OnShooterInitialized()
        {
            base.OnShooterInitialized();

            IsHoming = true;

            _trackingTimer = 0.0f;
            _lifeTime = 0.0f;
            _targetDirection = Vector3.zero;
            _isTracking = true;
            _hasInitialDirection = false;
            _hasPlayedScatterSound = false;
            _hasPlayedFireSound = false;

            ResetElapsedTime();

            if (UsesScatter)
            {
                if (!TrySetCommonScatterPosition())
                {
                    FinishPrana();

                    return;
                }

                IsScattered = false;
                _flightState = FlightState.ScatterDelay;

                return;
            }

            IsScattered = true;
            _flightState = FlightState.Tracking;
        }

        /// <summary>
        /// 散開、照準待機、追尾方向更新および
        /// 前進移動を更新します。
        /// </summary>
        protected override void Update()
        {
            if (!IsInitialized ||
                Context == null ||
                _flightState ==
                FlightState.Finished)
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager != null &&
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                FinishPrana();

                return;
            }

            FollowPreparationOwner();

            float deltaTime =
                Time.deltaTime;

            _lifeTime +=
                deltaTime;

            _trackingTimer +=
                deltaTime;

            ElapsedTime +=
                deltaTime;

            if (_lifeTime >=
                k_MaximumLifeTime)
            {
                FinishPrana();

                return;
            }

            switch (_flightState)
            {
                case FlightState.ScatterDelay:
                    UpdateScatterDelay();
                    break;

                case FlightState.Scattering:
                    UpdateScattering();
                    break;

                case FlightState.Aiming:
                    UpdateAiming();
                    break;

                case FlightState.Tracking:
                    UpdateTrackingFlight();
                    break;

                case FlightState.Finished:
                    break;

                default:
                    FinishPrana();
                    break;
            }
        }

        /// <summary>
        /// 散開を開始するまで待機します。
        /// </summary>
        private void UpdateScatterDelay()
        {
            if (ElapsedTime <
                ScatterDelay)
            {
                return;
            }

            PlayScatterSoundOnce();

            _flightState =
                FlightState.Scattering;
        }

        /// <summary>
        /// プラーナを設定された散開位置へ移動させます。
        /// </summary>
        private void UpdateScattering()
        {
            if (!MoveToScatterPosition())
            {
                return;
            }

            _flightState =
                FlightState.Aiming;
        }

        /// <summary>
        /// 散開完了後、射出開始まで待機します。
        /// </summary>
        private void UpdateAiming()
        {
            if (ElapsedTime <
                AimDuration)
            {
                return;
            }

            if (!PlayFireSoundOnce())
            {
                return;
            }

            _flightState =
                FlightState.Tracking;
        }

        /// <summary>
        /// 対象方向の更新、旋回、移動および
        /// 脅威通知を実行します。
        /// </summary>
        private void UpdateTrackingFlight()
        {
            if (!_hasInitialDirection)
            {
                if (!PlayFireSoundOnce())
                {
                    return;
                }
            }

            if (_trackingTimer >=
                Mathf.Max(
                    0.01f,
                    _trackingInterval))
            {
                _trackingTimer = 0.0f;

                RefreshTrackingDirection();
            }

            if (!RotateTowardsTargetDirection())
            {
                FinishPrana();

                return;
            }

            if (!TryMoveForward())
            {
                return;
            }

            SendThreat(
                _isTracking);
        }

        /// <summary>
        /// 対象方向を取得して、
        /// 初回の進行方向と追尾方向を設定します。
        /// </summary>
        /// <returns>
        /// 有効な対象方向を取得できた場合は<c>true</c>です。
        /// </returns>
        protected override bool TryPrepareLaunch()
        {
            return TryInitializeDirection();
        }

        /// <summary>
        /// 発射直前の敵位置から初回の進行方向と追尾方向を設定します。
        /// </summary>
        private bool TryInitializeDirection()
        {
            Vector3 initialDirection =
                GetTargetDirection(
                    Context.TargetPlayerId,
                    Context.SpreadAngle);

            if (initialDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            initialDirection.Normalize();

            Direction =
                initialDirection;

            _targetDirection =
                initialDirection;

            _isTracking = true;
            _hasInitialDirection = true;

            return true;
        }

        /// <summary>
        /// 対象の現在位置から新しい追尾方向を計算し、
        /// 追尾角度制限に基づいて採用するか判定します。
        /// </summary>
        private void RefreshTrackingDirection()
        {
            Vector3 nextTargetDirection =
                GetTargetDirection(
                    Context.TargetPlayerId,
                    0.0f);

            if (nextTargetDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                StopTracking();

                return;
            }

            nextTargetDirection.Normalize();

            Vector3 comparisonDirection =
                _targetDirection.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude
                    ? _targetDirection
                    : Direction;

            float angleFromPrevious =
                Vector3.Angle(
                    comparisonDirection,
                    nextTargetDirection);

            if (angleFromPrevious <=
                _trackingAngleLimit)
            {
                _targetDirection =
                    nextTargetDirection;

                _isTracking = true;

                return;
            }

            StopTracking();
        }

        /// <summary>
        /// 追尾を中止し、現在の進行方向へ直進させます。
        /// </summary>
        private void StopTracking()
        {
            _targetDirection =
                Direction.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude
                    ? Direction.normalized
                    : Vector3.zero;

            _isTracking = false;
        }

        /// <summary>
        /// 現在の進行方向を追尾方向へ徐々に回転させます。
        /// </summary>
        /// <returns>
        /// 有効な進行方向を維持できた場合は<c>true</c>です。
        /// </returns>
        private bool RotateTowardsTargetDirection()
        {
            if (_targetDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            Vector3 currentDirection =
                Direction;

            if (currentDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                currentDirection =
                    _targetDirection;
            }

            Vector3 rotatedDirection =
                Vector3.RotateTowards(
                    currentDirection.normalized,
                    _targetDirection.normalized,
                    Mathf.Max(
                        0.0f,
                        _turnSpeed) *
                    Mathf.Deg2Rad *
                    Time.deltaTime,
                    0.0f);

            if (rotatedDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            Direction =
                rotatedDirection;

            return true;
        }

        /// <summary>
        /// 現在の進行方向と設定速度に従って
        /// プラーナを移動させます。
        /// </summary>
        private bool TryMoveForward()
        {
            Vector3 destination = transform.position +
                Direction *
                Mathf.Max(
                    0.0f,
                    Context.Speed) *
                Time.deltaTime;
            return TryMoveProjectileWithCollision(destination);
        }

        /// <summary>
        /// Captareの飛行を終了し、
        /// ローカルのプラーナを破棄します。
        /// </summary>
        private void FinishPrana()
        {
            if (_flightState ==
                FlightState.Finished)
            {
                return;
            }

            _flightState =
                FlightState.Finished;

            DestroyLocally();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _turnSpeed =
                Mathf.Max(
                0.0f,
                _turnSpeed);

            _trackingAngleLimit =
                Mathf.Clamp(
                    _trackingAngleLimit,
                    0.0f,
                    180.0f);

            _trackingInterval =
                Mathf.Max(
                    0.01f,
                    _trackingInterval);
        }
#endif
    }
}
