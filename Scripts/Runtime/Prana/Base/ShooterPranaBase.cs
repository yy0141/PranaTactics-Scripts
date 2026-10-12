using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// Shooter系プラーナの散開、照準待機、移動、
    /// 脅威通知および命中処理を管理する基底クラスです。
    /// </summary>
    /// <remarks>
    /// プラーナは必要に応じて生成位置から散開位置まで移動し、
    /// 照準時間の経過後に対象方向へ射出されます。
    ///
    /// ユニット本体のColliderがシールドより先に検出された場合は、
    /// 命中位置からプラーナの進行方向前後へRaycastし、
    /// 本来シールドへ命中していたかどうかを補正します。
    /// </remarks>
    [DefaultExecutionOrder(100)]
    public abstract class ShooterPranaBase : PranaBase
    {
        /// <summary>
        /// 発射時だけ表示する弾と子オブジェクトの軌跡です。
        /// </summary>
        private TrailRenderer[] _trailRenderers;

        /// <summary>
        /// 生成直後から発射前の軌跡を非表示にします。
        /// </summary>
        protected virtual void Awake()
        {
            SetTrailVisibility(false);
        }

        /// <summary>
        /// 準備中の軌跡を消去し、発射位置から新しい軌跡を描画します。
        /// </summary>
        private void SetTrailVisibility(bool isVisible)
        {
            _trailRenderers ??= GetComponentsInChildren<TrailRenderer>(true);
            foreach (TrailRenderer trail in _trailRenderers)
            {
                if (trail == null)
                {
                    continue;
                }

                trail.emitting = false;
                trail.Clear();
                trail.enabled = isVisible;
                trail.emitting = isVisible;
            }
        }

        /// <summary>
        /// フレーム間の通過区間を検査する命中判定です。
        /// </summary>
        private ProjectileCollisionSweep _collisionSweep;

        /// <summary>
        /// 直進・追尾・曲射で共通の通過区間検査を行って移動します。
        /// </summary>
        protected bool TryMoveProjectileWithCollision(Vector3 destination)
        {
            _collisionSweep ??= new ProjectileCollisionSweep(
                transform, GetComponent<Collider>(), OnTriggerEnter, () => _isFinished);
            return _collisionSweep.Move(destination);
        }

        /// <summary>
        /// 発射前に追従し、移動ロックを保持する所有Unitです。
        /// </summary>
        private MicroUnitBehavior _preparationOwner;

        /// <summary>
        /// 最後に追従を反映した所有Unitの位置です。
        /// </summary>
        private Vector3 _lastPreparationOwnerPosition;

        /// <summary>
        /// 発射前の弾と散開先を所有Unitの移動量に追従させます。
        /// </summary>
        protected void FollowPreparationOwner()
        {
            if (_hasPlayedFireSound || _preparationOwner == null)
            {
                return;
            }

            Vector3 position = _preparationOwner.transform.position;
            Vector3 displacement = position - _lastPreparationOwnerPosition;
            transform.position += displacement;
            if (_hasScatterPosition)
            {
                _scatterPosition += displacement;
            }

            _lastPreparationOwnerPosition = position;
        }

        /// <summary>
        /// UnitManagerによる降下更新後に、発射前の追従位置を補正します。
        /// </summary>
        protected virtual void LateUpdate()
        {
            FollowPreparationOwner();
        }

        /// <summary>
        /// 発射前に弾が破棄された場合も所有Unitのロックを解除します。
        /// </summary>
        protected virtual void OnDestroy()
        {
            _preparationOwner?.ReleaseShooterPreparation(this);
        }

        /// <summary>
        /// 発射時点の敵位置から直進弾の射出方向を確定します。
        /// </summary>
        /// <returns>
        /// 有効な射出方向を確定できた場合は<c>true</c>です。
        /// </returns>
        protected virtual bool TryPrepareLaunch()
        {
#if UNITY_EDITOR || PV_CAPTURE
            if (_hasCaptureDirection)
            {
                return Direction.sqrMagnitude >= k_MinimumDirectionSqrMagnitude;
            }
#endif
            Direction = GetTargetDirection(Context.TargetPlayerId, Context.SpreadAngle);
            return Direction.sqrMagnitude >= k_MinimumDirectionSqrMagnitude;
        }

#if UNITY_EDITOR || PV_CAPTURE
        /// <summary>
        /// 撮影用に射出方向を明示指定しているかどうかです。
        /// </summary>
        private bool _hasCaptureDirection;

        public float CaptureFlightDistance
        {
            get; private set;
        }

        /// <summary>
        /// 撮影時だけ散開後の射線を対象へ揃え、着弾演出の乱数依存を除きます。
        /// </summary>
        public void AimForCapture(Vector3 target)
        {
            if (CaptureFlightDistance == 0 && _hasScatterPosition)
            {
                _direction = (target - _scatterPosition).normalized;
                _hasCaptureDirection = true;
            }
        }

#endif
        /// <summary>
        /// 有効な方向ベクトルと判断する最小二乗長です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude =
            0.0001f;

        /// <summary>
        /// 生成位置から散開位置を算出するときに使用する倍率です。
        /// Gerade、CaptareおよびArcusで共通使用します。
        /// </summary>
        private const float k_ScatterOffsetMultiplier =
            0.5f;

        /// <summary>
        /// 対象とほぼ同じ位置にいると判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumTargetSqrDistance =
            0.001f;

        /// <summary>
        /// 散開位置へ到達したと判断する距離です。
        /// </summary>
        private const float k_ScatterArrivalDistance =
            0.01f;

        /// <summary>
        /// シールド通過時刻の計算に使用する最低速度です。
        /// </summary>
        private const float k_MinimumPranaSpeed =
            0.1f;

        /// <summary>
        /// Colliderの厚みとして使用する最低値です。
        /// </summary>
        private const float k_MinimumColliderThickness =
            0.1f;

        /// <summary>
        /// プラーナが自動消滅するまでの時間です。
        /// </summary>
        private const float k_MaximumLifeTime =
            15.0f;

        /// <summary>
        /// 壁レイヤーの名前です。
        /// </summary>
        private const string k_WallLayerName =
            "Wall";

        /// <summary>
        /// ヘックスセルレイヤーの名前です。
        /// </summary>
        private const string k_HexCellLayerName =
            "HexCell";

        /// <summary>
        /// シールドレイヤーの名前です。
        /// </summary>
        private const string k_ShieldLayerName =
            "Shield";

        /// <summary>
        /// 現在の攻撃対象となっているユニットです。
        /// </summary>
        private UnitBehavior _targetUnitBehavior;

        /// <summary>
        /// 現在の攻撃対象位置です。
        /// </summary>
        private Vector3 _targetPosition =
            Vector3.zero;

        /// <summary>
        /// プラーナの進行方向です。
        /// </summary>
        private Vector3 _direction =
            Vector3.zero;

        /// <summary>
        /// 散開後に到達する位置です。
        /// </summary>
        private Vector3 _scatterPosition =
            Vector3.zero;

        /// <summary>
        /// 有効な散開位置が設定されているかどうかを示します。
        /// </summary>
        private bool _hasScatterPosition;

        /// <summary>
        /// プラーナ生成後、散開を開始するまでの時間です。
        /// </summary>
        [Header("Scatter")]
        [Min(0.0f)]
        [SerializeField]
        private float _scatterDelay =
            0.3f;

        /// <summary>
        /// 散開完了後、射出を開始するまでの照準時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _aimDuration =
            1.0f;

        /// <summary>
        /// 散開位置までの移動速度です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _scatterSpeed =
            15.0f;

        /// <summary>
        /// 散開位置を決定するときに使用する基本距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _scatterDistance =
            1.0f;

        /// <summary>
        /// 射出前に散開処理を行うかどうかを示します。
        /// </summary>
        [FormerlySerializedAs("UseScatter")]
        [SerializeField]
        private bool _usesScatter =
            true;

        /// <summary>
        /// 散開処理が完了しているかどうかを示します。
        /// </summary>
        private bool _isScattered;

        /// <summary>
        /// プラーナが生成されてから、
        /// または散開が完了してからの経過時間です。
        /// </summary>
        private float _elapsedTime;

        /// <summary>
        /// 散開開始時の効果音を再生済みかどうかを示します。
        /// </summary>
        protected bool _hasPlayedScatterSound;

        /// <summary>
        /// 射出開始時の効果音を再生済みかどうかを示します。
        /// </summary>
        protected bool _hasPlayedFireSound;

        /// <summary>
        /// 位置同期処理を実行する間隔です。
        /// </summary>
        [Header("Synchronization")]
        [Min(0.01f)]
        [SerializeField]
        private float _synchronizationInterval =
            0.2f;

        /// <summary>
        /// 最後に位置同期を実行した時刻です。
        /// </summary>
        private float _lastSynchronizationTime;

        /// <summary>
        /// このプラーナが対象を追尾するかどうかを示します。
        /// </summary>
        private bool _isHoming;

        /// <summary>
        /// 脅威通知を開始する基本距離です。
        /// </summary>
        [Header("Threat Notification")]
        [Min(0.0f)]
        [SerializeField]
        private float _notifyDistance =
            25.0f;

        /// <summary>
        /// 脅威通知を再送する時間間隔です。
        /// </summary>
        [Min(0.01f)]
        [SerializeField]
        private float _threatNotifyInterval =
            0.08f;

        /// <summary>
        /// 弾速から脅威通知距離を算出するときに使用する
        /// 予測時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _notifyLeadTime =
            1.2f;

        /// <summary>
        /// 最後に脅威を通知した時刻です。
        /// </summary>
        private float _lastThreatNotifyTime =
            float.NegativeInfinity;

        /// <summary>
        /// ユニット命中位置からシールドを検索するときに
        /// 射線へ追加する距離です。
        /// </summary>
        [Header("Shield Correction")]
        [Min(0.0f)]
        [SerializeField]
        private float _shieldRayExtraDistance =
            4.0f;

        /// <summary>
        /// 進行方向側のシールドを現在位置と重なっていると
        /// 判断する最大距離です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _shieldForwardImmediateDistance =
            0.5f;

        /// <summary>
        /// シールドの生成時刻を比較するときに使用する許容時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _shieldSpawnTimeTolerance =
            0.02f;

        /// <summary>
        /// シールドがプラーナを防御できる向きか判定する
        /// 内積の上限値です。
        /// </summary>
        [Range(-1.0f, 1.0f)]
        [SerializeField]
        private float _shieldFaceDotThreshold =
            -0.2f;

        /// <summary>
        /// プラーナが既に終了処理へ入ったかどうかを示します。
        /// </summary>
        private bool _isFinished;

        /// <summary>
        /// 同じシールドへ複数回ダメージを与えないため、
        /// 処理済みシールドのエンティティIDを保持します。
        /// </summary>
        private readonly HashSet<EntityId>
            _processedShieldIds = new();

        protected float ScatterDelay
        {
            get
            {
                return _scatterDelay;
            }
        }

        protected float AimDuration
        {
            get
            {
                return _aimDuration;
            }
        }

        protected float ScatterSpeed
        {
            get
            {
                return _scatterSpeed;
            }
        }

        protected Vector3 TargetPosition
        {
            get
            {
                return _targetPosition;
            }
            set
            {
                _targetPosition = value;
            }
        }

        protected Vector3 Direction
        {
            get
            {
                return _direction;
            }
            set
            {
                _direction =
                    value.sqrMagnitude >
                    k_MinimumDirectionSqrMagnitude
                        ? value.normalized
                        : Vector3.zero;
            }
        }

        protected Vector3 ScatterPosition
        {
            get
            {
                return _scatterPosition;
            }
            set
            {
                _scatterPosition = value;
                _hasScatterPosition = true;
            }
        }

        protected float ScatterDistance
        {
            get
            {
                return _scatterDistance;
            }
        }

        protected bool IsScattered
        {
            get
            {
                return _isScattered;
            }
            set
            {
                _isScattered = value;
            }
        }

        protected bool IsHoming
        {
            get
            {
                return _isHoming;
            }
            set
            {
                _isHoming = value;
            }
        }

        protected float SynchronizationInterval
        {
            get
            {
                return _synchronizationInterval;
            }
        }

        protected float LastSynchronizationTime
        {
            get
            {
                return _lastSynchronizationTime;
            }
            set
            {
                _lastSynchronizationTime = value;
            }
        }

        protected float ElapsedTime
        {
            get
            {
                return _elapsedTime;
            }
            set
            {
                _elapsedTime =
                    Mathf.Max(
                        0.0f,
                        value);
            }
        }

        public bool UsesScatter
        {
            get
            {
                return _usesScatter;
            }
        }

        /// <summary>
        /// 指定されたプレイヤーのユニット位置を取得し、
        /// 必要に応じて水平方向の拡散を適用します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーのIDです。
        /// </param>
        /// <param name="spreadAngle">
        /// 左右を合わせた拡散角度です。
        /// 10を指定した場合は-5度から+5度の範囲になります。
        /// </param>
        /// <returns>
        /// 拡散後の目標位置です。
        /// 対象を取得できない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        protected virtual Vector3 GetTargetPosition(
            int playerId,
            float spreadAngle = 0.0f)
        {
            if (!TryGetTargetTransform(
                    playerId,
                    out Transform targetTransform))
            {
                return Vector3.zero;
            }

            Vector3 targetPosition =
                targetTransform.position;

            if (spreadAngle <= 0.0f)
            {
                _targetPosition =
                    targetPosition;

                return _targetPosition;
            }

            Vector3 direction =
                targetPosition -
                transform.position;

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                _targetPosition =
                    targetPosition;

                return _targetPosition;
            }

            float targetDistance =
                direction.magnitude;

            direction.Normalize();

            float randomYaw =
                Random.Range(
                    -spreadAngle * 0.5f,
                    spreadAngle * 0.5f);

            Vector3 spreadDirection =
                Quaternion.AngleAxis(
                    randomYaw,
                    Vector3.up) *
                direction;

            _targetPosition =
                transform.position +
                spreadDirection.normalized *
                targetDistance;

            return _targetPosition;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニットへ向かう方向を取得し、
        /// 必要に応じて水平方向の拡散を適用します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーのIDです。
        /// </param>
        /// <param name="spreadAngle">
        /// 左右を合わせた拡散角度です。
        /// </param>
        /// <returns>
        /// 対象へ向かう正規化済み方向です。
        /// </returns>
        protected virtual Vector3 GetTargetDirection(
            int playerId,
            float spreadAngle = 0.0f)
        {
            if (!TryGetTargetTransform(
                    playerId,
                    out Transform targetTransform))
            {
                return Vector3.zero;
            }

            _targetPosition =
                targetTransform.position;

            Vector3 direction =
                _targetPosition -
                transform.position;

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return Vector3.zero;
            }

            direction.Normalize();

            if (spreadAngle > 0.0f)
            {
                float randomYaw =
                    Random.Range(
                        -spreadAngle * 0.5f,
                        spreadAngle * 0.5f);

                direction =
                    Quaternion.AngleAxis(
                        randomYaw,
                        Vector3.up) *
                    direction;
            }

            return direction.normalized;
        }

        /// <summary>
        /// 基底クラスによる初期化完了後に、
        /// Shooter系プラーナの実行状態を初期化します。
        /// </summary>
        protected override void OnInitialized()
        {
            SetTrailVisibility(false);
            _preparationOwner?.ReleaseShooterPreparation(this);
            _preparationOwner = null;
            base.OnInitialized();
#if UNITY_EDITOR || PV_CAPTURE
            CaptureFlightDistance = 0;
            _hasCaptureDirection = false;
#endif

            _targetUnitBehavior = null;
            _targetPosition = Vector3.zero;
            _direction = Vector3.zero;
            _scatterPosition = Vector3.zero;
            _hasScatterPosition = false;
            _isScattered = false;
            _elapsedTime = 0.0f;
            _hasPlayedScatterSound = false;
            _hasPlayedFireSound = false;
            _lastSynchronizationTime = 0.0f;
            _isHoming = false;
            _lastThreatNotifyTime = float.NegativeInfinity;
            _isFinished = false;

            _processedShieldIds.Clear();

            UnitManager unitManager = UnitManager.Instance;
            if (Context != null && unitManager != null &&
                unitManager.TryGetUnit(Context.PlayerId, out UnitBehavior owner))
            {
                _preparationOwner = owner.GetMicroUnitBehavior();
                if (_preparationOwner != null)
                {
                    _lastPreparationOwnerPosition = _preparationOwner.transform.position;
                    _preparationOwner.RegisterShooterPreparation(this);
                }
            }

            OnShooterInitialized();
        }

        /// <summary>
        /// Shooter系プラーナの共通初期化が完了した後に呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラスではこのメソッド内で
        /// <see cref="Direction"/>、<see cref="TargetPosition"/>、
        /// <see cref="ScatterPosition"/>および
        /// <see cref="IsHoming"/>を設定します。
        /// </remarks>
        protected virtual void OnShooterInitialized()
        {
        }

        /// <summary>
        /// プラーナの散開、照準待機および射出移動を更新します。
        /// </summary>
        protected virtual void Update()
        {
            if (!IsInitialized ||
                _isFinished)
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager != null &&
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                DestroyLocally();

                return;
            }

            FollowPreparationOwner();

            _elapsedTime +=
                Time.deltaTime;

            if (_usesScatter)
            {
                UpdateWithScatter();

                return;
            }

            UpdateWithoutScatter();
        }

        /// <summary>
        /// 現在のプラーナを対象ユニットへ脅威として通知します。
        /// </summary>
        /// <param name="isTracking">
        /// 対象を継続追跡する場合は<c>true</c>です。
        /// </param>
        protected void SendThreat(
            bool isTracking)
        {
            if (Context == null)
            {
                return;
            }

            if (Time.time -
                _lastThreatNotifyTime <
                _threatNotifyInterval)
            {
                return;
            }

            if (!TryGetTargetUnitBehavior(
                    Context.TargetPlayerId,
                    out UnitBehavior targetUnit))
            {
                return;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return;
            }

            Vector3 targetPosition =
                targetMicroUnit.transform.position;

            Vector3 toTarget =
                targetPosition -
                transform.position;

            if (toTarget.sqrMagnitude <
                k_MinimumTargetSqrDistance)
            {
                return;
            }

            float notifyDistance =
                Mathf.Max(
                    _notifyDistance,
                    Mathf.Max(
                        0.0f,
                        Context.Speed) *
                    _notifyLeadTime);

            if (toTarget.sqrMagnitude >
                notifyDistance *
                notifyDistance)
            {
                return;
            }

            Vector3 projectileDirection =
                GetProjectileDirection();

            if (projectileDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return;
            }

            ThreatData threatData =
                new()
                {
                    AttackerPlayerId =
                        Context.PlayerId,

                    ThreatId =
                        GetThreatId(),

                    IsMainWeapon =
                        Context.IsMain,

                    Position =
                        transform.position,

                    Direction =
                        projectileDirection.normalized,

                    Damage =
                        Mathf.Max(
                            0.0f,
                            Context.Attack),

                    Speed =
                        Mathf.Max(
                            0.0f,
                            Context.Speed),

                    IsHoming =
                        _isHoming,

                    IsTracking =
                        isTracking
                };

            targetUnit.NotifyThreat(
                threatData);

            _lastThreatNotifyTime =
                Time.time;
        }

        /// <summary>
        /// 生成位置を中心として，分割時の格子配置を維持したまま
        /// 各プラーナを外側へ散開させます。
        /// </summary>
        /// <remarks>
        /// 3×3分割の中央弾は移動しません。
        /// 4×4分割では中央位置に弾が存在しないため，
        /// すべての弾が起点から外側へ移動します。
        /// </remarks>
        /// <returns>
        /// 散開位置を設定できた場合は<c>true</c>です。
        /// </returns>
        protected bool TrySetCommonScatterPosition()
        {
            if (Context == null)
            {
                ClearScatterPosition();

                return false;
            }

            Vector3 generatePosition =
                Context.GeneratePosition;

            Vector3 initialOffset =
                transform.position -
                generatePosition;

            if (initialOffset.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                ScatterPosition =
                    generatePosition;

                return true;
            }

            Vector3 scatterOffset =
                initialOffset *
                k_ScatterOffsetMultiplier;

            ScatterPosition =
                transform.position +
                scatterOffset;

            return true;
        }

        /// <summary>
        /// 現在の移動段階で使用している経過時間を0へ戻します。
        /// </summary>
        protected void ResetElapsedTime()
        {
            _elapsedTime = 0.0f;
        }

        /// <summary>
        /// 散開位置の設定を解除します。
        /// </summary>
        protected void ClearScatterPosition()
        {
            _scatterPosition = Vector3.zero;
            _hasScatterPosition = false;
        }

        /// <summary>
        /// 散開ありの移動状態を更新します。
        /// </summary>
        private void UpdateWithScatter()
        {
            if (!_isScattered)
            {
                if (_elapsedTime <
                    _scatterDelay)
                {
                    return;
                }

                if (!_hasScatterPosition)
                {
                    return;
                }

                PlayScatterSoundOnce();
                MoveToScatterPosition();

                return;
            }

            if (_elapsedTime <
                _aimDuration)
            {
                return;
            }

            if (!PlayFireSoundOnce())
            {
                return;
            }
            MoveForward();
        }

        /// <summary>
        /// 散開なしの射出移動を更新します。
        /// </summary>
        private void UpdateWithoutScatter()
        {
            if (!PlayFireSoundOnce())
            {
                return;
            }
            MoveForward();
        }

        /// <summary>
        /// 散開開始時の効果音を1回だけ再生します。
        /// </summary>
        protected void PlayScatterSoundOnce()
        {
            if (_hasPlayedScatterSound)
            {
                return;
            }

            if (PranamBehavior != null && Context != null &&
                PranamBehavior.TryClaimGroupedSound(Context.SoundGroupId, PranamSoundEvent.Scatter))
            {
                RequestPranaSound(PranamSoundEvent.Scatter, transform.position);
            }

            _hasPlayedScatterSound = true;
        }

        /// <summary>
        /// 射出開始時の効果音を1回だけ再生します。
        /// </summary>
        /// <returns>
        /// 射出を開始できる場合は<c>true</c>です。
        /// </returns>
        protected bool PlayFireSoundOnce()
        {
            if (_hasPlayedFireSound)
            {
                return true;
            }

            FollowPreparationOwner();
            if (!TryPrepareLaunch())
            {
                _preparationOwner?.ReleaseShooterPreparation(this);
                DestroyLocally();
                return false;
            }
            _preparationOwner?.ReleaseShooterPreparation(this);

            if (PranamBehavior != null && Context != null &&
                PranamBehavior.TryClaimGroupedSound(Context.SoundGroupId, PranamSoundEvent.Fire))
            {
                RequestPranaSound(PranamSoundEvent.Fire, transform.position);
            }

            _hasPlayedFireSound = true;
            SetTrailVisibility(true);
            return true;
        }

        /// <summary>
        /// プラーナを設定された散開位置まで移動させます。
        /// </summary>
        protected bool MoveToScatterPosition()
        {
            transform.position =
                Vector3.MoveTowards(
                    transform.position,
                    _scatterPosition,
                    Time.deltaTime *
                    Mathf.Max(
                        0.0f,
                        _scatterSpeed));

            float remainingDistance =
                Vector3.Distance(
                    transform.position,
                    _scatterPosition);

            if (remainingDistance >
                k_ScatterArrivalDistance)
            {
                return false;
            }

            transform.position =
                _scatterPosition;

            _isScattered = true;
            _elapsedTime = 0.0f;

            return true;
        }

        /// <summary>
        /// 現在の進行方向と速度を使用して
        /// プラーナを直線移動させます。
        /// </summary>
        private void MoveForward()
        {
            if (Context == null)
            {
                return;
            }

#if UNITY_EDITOR || PV_CAPTURE
            CaptureFlightDistance += Mathf.Max(0, Context.Speed) * Time.deltaTime;
#endif
            Vector3 destination = transform.position +
                _direction *
                Mathf.Max(
                    0.0f,
                    Context.Speed) *
                Time.deltaTime;

            if (!TryMoveProjectileWithCollision(destination))
            {
                return;
            }

            if (_elapsedTime >
                k_MaximumLifeTime)
            {
                DestroyLocally();
            }
        }

        /// <summary>
        /// 指定されたプレイヤーの対象Transformを取得します。
        /// </summary>
        private bool TryGetTargetTransform(
            int playerId,
            out Transform targetTransform)
        {
            targetTransform = null;

            if (!TryGetTargetUnitBehavior(
                    playerId,
                    out UnitBehavior targetUnit))
            {
                return false;
            }

            MicroUnitBehavior targetMicroUnit =
                targetUnit.GetMicroUnitBehavior();

            if (targetMicroUnit == null)
            {
                return false;
            }

            targetTransform =
                targetMicroUnit.transform;

            return targetTransform != null;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニットを取得します。
        /// </summary>
        private bool TryGetTargetUnitBehavior(
            int playerId,
            out UnitBehavior targetUnit)
        {
            targetUnit = null;

            if (_targetUnitBehavior != null &&
                _targetUnitBehavior.PlayerId ==
                playerId)
            {
                targetUnit =
                    _targetUnitBehavior;

                return true;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            GameObject targetObject =
                unitManager.GetUnitObject(
                    playerId);

            if (targetObject == null ||
                !targetObject.TryGetComponent(
                    out UnitBehavior foundUnit))
            {
                _targetUnitBehavior = null;

                return false;
            }

            _targetUnitBehavior =
                foundUnit;

            targetUnit =
                foundUnit;

            return true;
        }

        /// <summary>
        /// 脅威予測で使用するプラーナ固有のIDを生成します。
        /// </summary>
        private int GetThreatId()
        {
            if (Context == null)
            {
                return GetEntityId().GetHashCode();
            }

            unchecked
            {
                int hash = 17;

                hash =
                    hash * 31 +
                    Context.PlayerId;

                hash =
                    hash * 31 +
                    Context.Id;

                hash =
                    hash * 31 +
                    (Context.IsMain
                        ? 1
                        : 0);

                return hash;
            }
        }

        /// <summary>
        /// プラーナがColliderへ進入したときの命中処理です。
        /// </summary>
        /// <remarks>
        /// 分割生成されたShooter弾は生成直後に互いのColliderへ
        /// 接触する場合があります。同じプレイヤーが生成した弾同士は
        /// Unitや味方への接触として扱わず、そのまま飛行を継続します。
        /// </remarks>
        private void OnTriggerEnter(
                    Collider hitCollider)
        {
            if (_isFinished ||
                hitCollider == null ||
                Context == null ||
                PranamBehavior == null)
            {
                return;
            }

            if (IsSameOwnerShooterPranaCollider(
                    hitCollider))
            {
                return;
            }

            if (IsOwnerCollider(hitCollider))
            {
                return;
            }

            if (IsOwnerOrAllyCollider(
                    hitCollider))
            {
                RequestHitSound(hitCollider);
                DestroyLocally();

                return;
            }

            if (TryExplodeOnImpact(hitCollider))
            {
                return;
            }

            if (IsEnvironmentCollider(
                    hitCollider))
            {
                RequestHitSound(hitCollider);
                DestroyLocally();

                return;
            }

            Vector3 projectileDirection =
                GetProjectileDirection();

            ShieldPranaBase directShield =
                hitCollider.GetComponentInParent<ShieldPranaBase>();

            if (directShield != null)
            {
                if (TryApplyDirectShield(
                        directShield,
                        projectileDirection))
                {
                    RequestHitSound(hitCollider);

                    if (Context.Attack <= 0.0f)
                    {
                        DestroyLocally();
                    }
                }

                return;
            }

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            if (damageable == null)
            {
                return;
            }

            int targetPlayerId =
                damageable.PlayerId;

            if (IsOwnerOrAllyPlayer(
                    targetPlayerId))
            {
                RequestHitSound(hitCollider);
                DestroyLocally();

                return;
            }

            RequestHitSound(hitCollider);

            TryApplyForwardBackwardRaycastShield(
                targetPlayerId,
                transform.position,
                projectileDirection);

            if (Context.Attack <= 0.0f)
            {
                DestroyLocally();

                return;
            }

            PranamBehavior.HitPrana(
                Context.PlayerId,
                Context.Id,
                targetPlayerId,
                Context.Attack,
                hitSlowdownRatio: Context.HitSlowdownRatio,
                hitSlowdownDurationSeconds: Context.HitSlowdownDurationSeconds);

            DestroyLocally();
        }

        /// <summary>
        /// 指定されたColliderが、この弾と同じプレイヤーによって
        /// 生成されたShooter弾に属するか判定します。
        /// </summary>
        /// <param name="hitCollider">
        /// 接触したColliderです。
        /// </param>
        /// <returns>
        /// 自身のCollider、または同一プレイヤーが生成した
        /// Shooter弾のColliderの場合は<c>true</c>です。
        /// </returns>
        private bool IsSameOwnerShooterPranaCollider(
            Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            ShooterPranaBase otherProjectile =
                hitCollider.GetComponentInParent<ShooterPranaBase>();

            if (otherProjectile == null)
            {
                return false;
            }

            if (otherProjectile == this)
            {
                return true;
            }

            int ownerPlayerId =
                OwnerPlayerId;

            return ownerPlayerId >= 0 &&
                otherProjectile.OwnerPlayerId ==
                ownerPlayerId;
        }

        /// <summary>
        /// 生成・分割・散開中の接触では命中音を鳴らしません。
        /// </summary>
        protected override void RequestHitSound(Collider hitCollider)
        {
            if (!_hasPlayedFireSound)
            {
                return;
            }

            base.RequestHitSound(hitCollider);
        }

        /// <summary>
        /// 指定されたColliderが壁またはヘックスセルか判定します。
        /// </summary>
        private static bool IsEnvironmentCollider(
            Collider hitCollider)
        {
            int layer =
                hitCollider.gameObject.layer;

            int wallLayer =
                LayerMask.NameToLayer(
                    k_WallLayerName);

            int hexCellLayer =
                LayerMask.NameToLayer(
                    k_HexCellLayerName);

            return layer == wallLayer ||
                layer == hexCellLayer;
        }

        /// <summary>
        /// 現在のプラーナの進行方向を取得します。
        /// </summary>
        private Vector3 GetProjectileDirection()
        {
            if (_direction.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude)
            {
                return _direction.normalized;
            }

            if (_targetPosition !=
                Vector3.zero)
            {
                Vector3 directionToTarget =
                    _targetPosition -
                    transform.position;

                if (directionToTarget.sqrMagnitude >
                    k_MinimumDirectionSqrMagnitude)
                {
                    return directionToTarget.normalized;
                }
            }

            if (transform.forward.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude)
            {
                return transform.forward.normalized;
            }

            return Vector3.forward;
        }

        /// <summary>
        /// Colliderへ直接接触したシールドへ
        /// ダメージを適用します。
        /// </summary>
        private bool TryApplyDirectShield(
            ShieldPranaBase shield,
            Vector3 projectileDirection)
        {
            if (shield == null ||
                IsOwnerOrAllyPlayer(
                    shield.OwnerPlayerId) ||
                shield.GetPranaHealth() <=
                0.0f)
            {
                return false;
            }

            if (!shield.CanBlockProjectile(
                    projectileDirection,
                    _shieldFaceDotThreshold))
            {
                return false;
            }

            return ApplyShieldDamage(
                shield);
        }

        /// <summary>
        /// ユニット命中位置から前後へRaycastし、
        /// 本来シールドへ命中していたか確認します。
        /// </summary>
        private bool TryApplyForwardBackwardRaycastShield(
            int targetPlayerId,
            Vector3 origin,
            Vector3 projectileDirection)
        {
            return TryFindImpactShield(targetPlayerId, origin, projectileDirection,
                out ShieldPranaBase shield) && ApplyShieldDamage(shield);
        }

        /// <summary>
        /// 高速弾の命中位置と通過区間から、本来命中するシールドを検索します。
        /// </summary>
        private bool TryFindImpactShield(
            int targetPlayerId,
            Vector3 origin,
            Vector3 projectileDirection,
            out ShieldPranaBase impactShield)
        {
            impactShield = null;
            if (projectileDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return false;
            }

            projectileDirection.Normalize();

            if (TryGetContainingShield(
                    targetPlayerId,
                    origin,
                    projectileDirection,
                    GetShieldLayerMask(),
                    _processedShieldIds,
                    out ShieldPranaBase containingShield))
            {
                impactShield = containingShield;
                return true;
            }

            float speed =
                Mathf.Max(
                    Context.Speed,
                    k_MinimumPranaSpeed);

            float searchDistance =
                Mathf.Max(
                    speed *
                    Time.deltaTime +
                    _shieldRayExtraDistance,
                    _shieldRayExtraDistance);

            ShieldPranaBase bestShield =
                null;

            float bestDistance =
                float.MaxValue;

            CheckShieldRaycastDirection(
                targetPlayerId,
                origin,
                -projectileDirection,
                projectileDirection,
                searchDistance,
                speed,
                true,
                ref bestShield,
                ref bestDistance);

            CheckShieldRaycastDirection(
                targetPlayerId,
                origin,
                projectileDirection,
                projectileDirection,
                searchDistance,
                speed,
                false,
                ref bestShield,
                ref bestDistance);

            if (bestShield == null)
            {
                return false;
            }

            impactShield = bestShield;
            return true;
        }

        /// <summary>
        /// 爆発設定を持つ弾の着弾を球状爆風と演出へ変換します。
        /// </summary>
        private bool TryExplodeOnImpact(Collider hitCollider)
        {
            if (Context.Explosion == null || !Context.Explosion.Enabled)
            {
                return false;
            }

            ShieldPranaBase shield = hitCollider.GetComponentInParent<ShieldPranaBase>();
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
            Vector3 direction = GetProjectileDirection();
            if (shield != null && !shield.CanBlockProjectile(direction, _shieldFaceDotThreshold))
            {
                return true;
            }

            if (!CanExplodeOnCollider(hitCollider))
            {
                return false;
            }

            if (shield == null && target != null)
            {
                TryFindImpactShield(target.PlayerId, transform.position, direction, out shield);
            }

            Collider impactCollider = shield != null ? shield.GetComponentInChildren<Collider>() : hitCollider;
            Vector3 origin = GetExplosionOrigin(impactCollider, direction, shield != null);
            _isFinished = true;
            RequestHitSound(impactCollider);
            ShooterExplosionVfx.Play(origin, Context.Explosion);
            var resolver = new ShooterExplosionResolver(
                Context, ApplyExplosionHit, IsOwnerOrAllyPlayer, _shieldFaceDotThreshold);
            resolver.Explode(origin, direction, shield == null ? hitCollider : null, shield);
            PranamBehavior.ApplyDestroyPrana(Context.Id);
            Destroy(gameObject);
            return true;
        }

        /// <summary>
        /// 爆発弾が着弾するユニット、シールドまたは地形か判定します。
        /// </summary>
        protected virtual bool CanExplodeOnCollider(Collider hitCollider)
        {
            return IsEnvironmentCollider(hitCollider) ||
                hitCollider.GetComponentInParent<IDamageable>() != null;
        }

        /// <summary>
        /// 着弾面の座標を取得し、シールド内へすり抜けた場合は入射側の表面へ戻します。
        /// </summary>
        private Vector3 GetExplosionOrigin(Collider impactCollider, Vector3 direction, bool isShield)
        {
            if (impactCollider == null)
            {
                return transform.position;
            }

            Vector3 origin = impactCollider.ClosestPoint(transform.position);
            if (isShield && (origin - transform.position).sqrMagnitude < 0.000001f)
            {
                float distance = impactCollider.bounds.size.magnitude + _shieldRayExtraDistance;
                var ray = new Ray(origin - direction * distance, direction);
                if (impactCollider.Raycast(ray, out RaycastHit hit, distance * 2.0f))
                {
                    return hit.point;
                }
            }

            return origin;
        }

        /// <summary>
        /// 一つの爆発で複数の対象へ命中を通知する間は、発生元の弾を保持します。
        /// </summary>
        private void ApplyExplosionHit(int targetPlayerId, float damage)
        {
            PranamBehavior.HitPrana(
                Context.PlayerId, Context.Id, targetPlayerId, damage,
                destroyPrana: false,
                hitSlowdownRatio: Context.HitSlowdownRatio,
                hitSlowdownDurationSeconds: Context.HitSlowdownDurationSeconds);
        }

        /// <summary>
        /// 指定方向へRaycastし、防御に使用できる
        /// 最も近いシールドを検索します。
        /// </summary>
        private void CheckShieldRaycastDirection(
            int targetPlayerId,
            Vector3 origin,
            Vector3 rayDirection,
            Vector3 projectileDirection,
            float searchDistance,
            float speed,
            bool isBackwardRay,
            ref ShieldPranaBase bestShield,
            ref float bestDistance)
        {
            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    rayDirection,
                    searchDistance,
                    GetShieldLayerMask(),
                    QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits)
            {
                ShieldPranaBase shield =
                    hit.collider.GetComponentInParent<ShieldPranaBase>();

                if (shield == null ||
                    shield.OwnerPlayerId !=
                    targetPlayerId ||
                    shield.GetPranaHealth() <=
                    0.0f)
                {
                    continue;
                }

                if (_processedShieldIds.Contains(
                        shield.GetEntityId()))
                {
                    continue;
                }

                if (!shield.CanBlockProjectile(
                        projectileDirection,
                        _shieldFaceDotThreshold))
                {
                    continue;
                }

                if (!isBackwardRay &&
                    hit.distance >
                    _shieldForwardImmediateDistance)
                {
                    continue;
                }

                float shieldThickness =
                    GetColliderThicknessAlongDirection(
                        hit.collider,
                        rayDirection);

                float shieldPassTime =
                    EstimateShieldPassTime(
                        hit.distance,
                        shieldThickness,
                        speed,
                        isBackwardRay);

                if (shield.SpawnTime >
                    shieldPassTime +
                    _shieldSpawnTimeTolerance)
                {
                    continue;
                }

                if (hit.distance >=
                    bestDistance)
                {
                    continue;
                }

                bestDistance =
                    hit.distance;

                bestShield =
                    shield;
            }
        }

        /// <summary>
        /// プラーナがシールドを通過した推定時刻を計算します。
        /// </summary>
        private static float EstimateShieldPassTime(
            float distanceToShield,
            float shieldThickness,
            float speed,
            bool isBackwardRay)
        {
            if (isBackwardRay)
            {
                return Time.time -
                    ((distanceToShield +
                        shieldThickness) /
                        speed);
            }

            return Time.time -
                (shieldThickness /
                    speed);
        }

        /// <summary>
        /// Colliderの指定方向に対する厚みを取得します。
        /// </summary>
        private static float GetColliderThicknessAlongDirection(
            Collider targetCollider,
            Vector3 direction)
        {
            if (targetCollider == null ||
                direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return k_MinimumColliderThickness;
            }

            direction.Normalize();

            Bounds bounds =
                targetCollider.bounds;

            float thickness =
                Mathf.Abs(direction.x) *
                bounds.size.x +
                Mathf.Abs(direction.y) *
                bounds.size.y +
                Mathf.Abs(direction.z) *
                bounds.size.z;

            return Mathf.Max(
                thickness,
                k_MinimumColliderThickness);
        }


        /// <summary>
        /// シールドへダメージを適用し、
        /// 吸収された分だけプラーナの攻撃力を減少させます。
        /// </summary>
        private bool ApplyShieldDamage(
            ShieldPranaBase shield)
        {
            if (shield == null ||
                IsOwnerOrAllyPlayer(
                    shield.OwnerPlayerId))
            {
                return false;
            }

            EntityId shieldEntityId =
                shield.GetEntityId();

            if (!_processedShieldIds.Add(
                    shieldEntityId))
            {
                return false;
            }

            float attackBefore =
                Mathf.Max(
                    0.0f,
                    Context.Attack);

            float shieldHealthBefore =
                Mathf.Max(
                    0.0f,
                    shield.GetPranaHealth());

            if (attackBefore <= 0.0f ||
                shieldHealthBefore <= 0.0f)
            {
                return false;
            }

            shield.ApplyCombatDamage(
                Context.PlayerId,
                attackBefore,
                Context.IsMain);

            float absorbedDamage =
                Mathf.Min(
                    attackBefore,
                    shieldHealthBefore);

            Context.Attack =
                Mathf.Max(
                    0.0f,
                    attackBefore -
                    absorbedDamage);

            return true;
        }

        /// <summary>
        /// シールドレイヤーを対象とするLayerMaskを取得します。
        /// </summary>
        private static int GetShieldLayerMask()
        {
            int shieldLayer =
                LayerMask.NameToLayer(
                    k_ShieldLayerName);

            return shieldLayer >= 0
                ? 1 << shieldLayer
                : Physics.AllLayers;
        }

        /// <summary>
        /// プラーナをローカル環境から破棄します。
        /// </summary>
        protected void DestroyLocally()
        {
            if (_isFinished)
            {
                return;
            }

            _isFinished = true;

            Destroy(
                gameObject);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorから設定された値を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _scatterDelay =
            Mathf.Max(
            0.0f,
            _scatterDelay);

            _aimDuration =
                Mathf.Max(
                    0.0f,
                    _aimDuration);

            _scatterSpeed =
                Mathf.Max(
                    0.0f,
                    _scatterSpeed);

            _scatterDistance =
                Mathf.Max(
                    0.0f,
                    _scatterDistance);

            _synchronizationInterval =
                Mathf.Max(
                    0.01f,
                    _synchronizationInterval);

            _notifyDistance =
                Mathf.Max(
                    0.0f,
                    _notifyDistance);

            _threatNotifyInterval =
                Mathf.Max(
                    0.01f,
                    _threatNotifyInterval);

            _notifyLeadTime =
                Mathf.Max(
                    0.0f,
                    _notifyLeadTime);

            _shieldRayExtraDistance =
                Mathf.Max(
                    0.0f,
                    _shieldRayExtraDistance);

            _shieldForwardImmediateDistance =
                Mathf.Max(
                    0.0f,
                    _shieldForwardImmediateDistance);

            _shieldSpawnTimeTolerance =
                Mathf.Max(
                    0.0f,
                    _shieldSpawnTimeTolerance);
        }

#endif
    }
}
