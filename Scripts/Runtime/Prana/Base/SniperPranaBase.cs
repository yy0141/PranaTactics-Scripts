using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 狙撃系プラーナの移動、脅威通知、
    /// 命中処理およびシールド判定を管理する基底クラスです。
    /// </summary>
    /// <remarks>
    /// ユニット本体のColliderがシールドより先に検出された場合は、
    /// 命中位置から進行方向の前後へRaycastし、
    /// 本来シールドへ命中していたかどうかを補正します。
    /// </remarks>
    [DefaultExecutionOrder(100)]
    public abstract class SniperPranaBase : PranaBase
    {
        /// <summary>
        /// フレーム間の通過区間を検査する命中判定です。
        /// </summary>
        private ProjectileCollisionSweep _collisionSweep;

        /// <summary>
        /// 有効な方向ベクトルと判断する最小二乗長です。
        /// </summary>
        private const float k_MinimumDirectionSqrMagnitude =
            0.0001f;

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
        /// 有効な攻撃対象位置が設定されているかどうかを示します。
        /// </summary>
        private bool _hasTargetPosition;

        /// <summary>
        /// プラーナの進行方向です。
        /// </summary>
        private Vector3 _direction =
            Vector3.zero;

        /// <summary>
        /// プラーナ生成後の経過時間です。
        /// </summary>
        private float _elapsedTime;

        /// <summary>
        /// 対象ユニットへ脅威を通知済みかどうかを示します。
        /// </summary>
        private bool _hasSentThreat;

        /// <summary>
        /// 脅威通知を行う最大距離です。
        /// </summary>
        [Header("Threat Notification")]
        [Min(0.0f)]
        [SerializeField]
        private float _notifyDistance =
            20.0f;

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
        /// シールド生成時刻の比較に使用する許容時間です。
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
        /// 同じシールドの複数Colliderによって
        /// ダメージが重複しないよう処理済みシールドを保持します。
        /// </summary>
        private readonly HashSet<EntityId>
            _processedShieldIds = new();

        protected Vector3 TargetPosition
        {
            get
            {
                return _targetPosition;
            }
            set
            {
                _targetPosition = value;
                _hasTargetPosition = true;
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

        protected float ElapsedTime
        {
            get
            {
                return _elapsedTime;
            }
        }

        /// <summary>
        /// このプラーナの現在の攻撃力を取得します。
        /// </summary>
        /// <returns>
        /// シールドによる減衰後の攻撃力です。
        /// </returns>
        public override float GetPranaHealth()
        {
            if (Context == null)
            {
                return 0.0f;
            }

            return Mathf.Max(
                0.0f,
                Context.Attack);
        }

        /// <summary>
        /// 指定されたプレイヤーの現在位置を取得し、
        /// 狙撃対象位置として保存します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーのIDです。
        /// </param>
        /// <returns>
        /// 対象ユニットのワールド座標です。
        /// 対象を取得できない場合は<see cref="Vector3.zero"/>です。
        /// </returns>
        protected virtual Vector3 GetTargetPosition(
            int playerId)
        {
            if (!TryGetTargetTransform(
                    playerId,
                    out Transform targetTransform))
            {
                ClearTargetPosition();

                return Vector3.zero;
            }

            TargetPosition =
                targetTransform.position;

            return TargetPosition;
        }

        /// <summary>
        /// 基底クラスによる初期化完了後に、
        /// 狙撃プラーナの実行状態を初期化します。
        /// </summary>
        protected override void OnInitialized()
        {
            base.OnInitialized();

            _targetUnitBehavior = null;
            _targetPosition = Vector3.zero;
            _hasTargetPosition = false;
            _direction = Vector3.zero;
            _elapsedTime = 0.0f;
            _hasSentThreat = false;
            _isFinished = false;

            _processedShieldIds.Clear();

            OnSniperInitialized();
        }

        /// <summary>
        /// 狙撃プラーナの共通初期化が完了した後に呼び出されます。
        /// </summary>
        /// <remarks>
        /// 派生クラスでは、このメソッド内で
        /// <see cref="TargetPosition"/>と<see cref="Direction"/>を
        /// 設定します。
        /// </remarks>
        protected virtual void OnSniperInitialized()
        {
        }

        /// <summary>
        /// プラーナの移動、寿命およびフェイズ状態を更新します。
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

            _elapsedTime +=
                Time.deltaTime;

            if (_hasTargetPosition)
            {
                MovePrana();
            }
        }

        /// <summary>
        /// この狙撃プラーナを対象ユニットへ
        /// 脅威として1回だけ通知します。
        /// </summary>
        /// <param name="isTracking">
        /// 対象を継続追跡する攻撃の場合は<c>true</c>です。
        /// </param>
        protected void SendThreat(
            bool isTracking)
        {
            if (_hasSentThreat ||
                Context == null)
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

            float notifyDistance =
                Mathf.Max(
                    0.0f,
                    _notifyDistance);

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
                        false,

                    IsTracking =
                        isTracking
                };

            targetUnit.NotifyThreat(
                threatData);

            _hasSentThreat = true;
        }

        /// <summary>
        /// 現在の進行方向と速度を使用して
        /// 狙撃プラーナを直線移動させます。
        /// </summary>
        protected virtual void MovePrana()
        {
            if (Context == null)
            {
                return;
            }

            Vector3 destination = transform.position +
                _direction *
                Mathf.Max(
                    0.0f,
                    Context.Speed) *
                Time.deltaTime;

            _collisionSweep ??= new ProjectileCollisionSweep(
                transform, GetComponent<Collider>(), OnTriggerEnter, () => _isFinished);
            if (!_collisionSweep.Move(destination))
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
        /// 経過時間を0へ戻します。
        /// </summary>
        protected void ResetElapsedTime()
        {
            _elapsedTime = 0.0f;
        }

        /// <summary>
        /// 現在設定されている攻撃対象位置を解除します。
        /// </summary>
        protected void ClearTargetPosition()
        {
            _targetPosition = Vector3.zero;
            _hasTargetPosition = false;
        }

        /// <summary>
        /// プラーナがColliderへ進入したときの命中処理です。
        /// </summary>
        /// <param name="hitCollider">
        /// 接触したColliderです。
        /// </param>
        protected virtual void OnTriggerEnter(
            Collider hitCollider)
        {
            if (_isFinished ||
                hitCollider == null ||
                Context == null ||
                PranamBehavior == null)
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
        /// 指定されたプレイヤーのミクロユニットTransformを取得します。
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
        /// 脅威予測に使用するプラーナ固有のIDを生成します。
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
        /// 現在の狙撃プラーナの進行方向を取得します。
        /// </summary>
        private Vector3 GetProjectileDirection()
        {
            if (_direction.sqrMagnitude >
                k_MinimumDirectionSqrMagnitude)
            {
                return _direction.normalized;
            }

            if (_hasTargetPosition)
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
        /// Colliderへ直接接触したシールドへダメージを適用します。
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
                return ApplyShieldDamage(containingShield);
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

            return ApplyShieldDamage(
                bestShield);
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
                Context == null ||
                IsOwnerOrAllyPlayer(
                    shield.OwnerPlayerId))
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

            EntityId shieldEntityId =
                shield.GetEntityId();

            if (!_processedShieldIds.Add(
                    shieldEntityId))
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
        private void DestroyLocally()
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
            _notifyDistance =
                Mathf.Max(
                0.0f,
                _notifyDistance);

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
