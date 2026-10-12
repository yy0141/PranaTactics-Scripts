using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 射撃系プラーナの移動、対象追跡、脅威通知および
    /// Colliderとの命中処理に関する共通機能を提供します。
    /// </summary>
    /// <remarks>
    /// ユニット本体のColliderがシールドより先に検出された場合は、
    /// プラーナの進行方向と逆方向をRaycastし、
    /// 本来シールドへ命中していたかどうかを補正します。
    /// </remarks>
    [DefaultExecutionOrder(100)]
    public abstract class GunnerPranaBase : PranaBase
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
        /// 対象とほぼ同じ位置にいると判断する最小二乗距離です。
        /// </summary>
        private const float k_MinimumTargetSqrDistance =
            0.001f;

        /// <summary>
        /// プラーナの最低速度です。
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
        /// シールドレイヤーの名前です。
        /// </summary>
        private const string k_ShieldLayerName =
            "Shield";

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
        /// 現在の攻撃対象となっているユニットです。
        /// </summary>
        private UnitBehavior _targetUnitBehavior;

        /// <summary>
        /// 現在の攻撃対象位置です。
        /// </summary>
        protected Vector3 _targetPosition =
            Vector3.zero;

        /// <summary>
        /// プラーナが進行する方向です。
        /// </summary>
        protected Vector3 _direction =
            Vector3.zero;

        /// <summary>
        /// プラーナが生成されてからの経過時間です。
        /// </summary>
        protected float _time;

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
        /// 弾速から脅威通知距離を計算する際に使用する
        /// 予測時間です。
        /// </summary>
        [Min(0.0f)]
        [SerializeField]
        private float _notifyLeadTime =
            1.2f;

        /// <summary>
        /// 前回脅威を通知した時刻です。
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
        /// シールドがプラーナの方向を向いていると
        /// 判断する内積の上限値です。
        /// </summary>
        [Range(-1.0f, 1.0f)]
        [SerializeField]
        private float _shieldFaceDotThreshold =
            -0.2f;

        /// <summary>
        /// 現在のプラーナが既に終了処理へ入ったかどうかを示します。
        /// </summary>
        private bool _isFinished;

        /// <summary>
        /// 同じシールドへ複数回ダメージを与えないため、
        /// 処理済みシールドのエンティティIDを保持します。
        /// </summary>
        private readonly HashSet<EntityId>
            _processedShieldIds = new();

        /// <summary>
        /// 指定されたプレイヤーのユニット位置を取得し、
        /// 必要に応じて円錐状の拡散を適用します。
        /// </summary>
        /// <param name="playerId">
        /// 対象となるプレイヤーのIDです。
        /// </param>
        /// <param name="spreadAngle">
        /// 円錐全体の拡散角度です。
        /// 10を指定した場合は、中心方向から最大5度拡散します。
        /// </param>
        /// <returns>
        /// 拡散を適用した目標位置です。
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

            float maximumSpreadAngle =
                spreadAngle * 0.5f;

            Vector3 spreadDirection =
                ApplyConeSpread(
                    direction,
                    maximumSpreadAngle);

            if (spreadDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                spreadDirection =
                    direction;
            }

            _targetPosition =
                transform.position +
                spreadDirection.normalized *
                targetDistance;

            return _targetPosition;
        }

        /// <summary>
        /// 指定されたプレイヤーのユニットへ向かう方向を取得し、
        /// 必要に応じて円錐状の拡散を適用します。
        /// </summary>
        /// <param name="playerId">
        /// 対象となるプレイヤーのIDです。
        /// </param>
        /// <param name="spreadAngle">
        /// 中心方向からの最大拡散角度です。
        /// </param>
        /// <returns>
        /// 対象へ向かう正規化済み方向です。
        /// 対象を取得できない場合は<see cref="Vector3.zero"/>です。
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
                direction =
                    ApplyConeSpread(
                        direction,
                        spreadAngle);
            }

            return direction.normalized;
        }

        /// <summary>
        /// 指定された基準方向を中心として、
        /// 水平・垂直方向を含む円錐状の拡散を適用します。
        /// </summary>
        /// <param name="baseDirection">
        /// 拡散の中心となる方向です。
        /// </param>
        /// <param name="spreadAngle">
        /// 円錐全体の拡散角度です。
        /// </param>
        /// <returns>
        /// 拡散後の正規化済み方向です。
        /// </returns>
        protected Vector3 ApplyConeSpread(
            Vector3 baseDirection,
            float spreadAngle)
        {
            if (baseDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return Vector3.zero;
            }

            baseDirection.Normalize();

            float maximumSpreadAngle =
                Mathf.Clamp(
                    spreadAngle * 0.5f,
                    0.0f,
                    89.0f);

            Vector3 right =
                Vector3.Cross(
                    Vector3.up,
                    baseDirection);

            if (right.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                right =
                    Vector3.Cross(
                        Vector3.forward,
                        baseDirection);
            }

            if (right.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                right = Vector3.right;
            }

            right.Normalize();

            Vector3 localUp =
                Vector3.Cross(
                    baseDirection,
                    right);

            if (localUp.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                localUp = Vector3.up;
            }

            localUp.Normalize();

            Vector2 randomOffset =
                Random.insideUnitCircle;

            float spreadRadius =
                Mathf.Tan(
                    maximumSpreadAngle *
                    Mathf.Deg2Rad);

            Vector3 spreadDirection =
                baseDirection +
                right *
                randomOffset.x *
                spreadRadius +
                localUp *
                randomOffset.y *
                spreadRadius;

            if (spreadDirection.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return baseDirection;
            }

            return spreadDirection.normalized;
        }

        /// <summary>
        /// プラーナの移動と寿命を更新します。
        /// </summary>
        protected virtual void Update()
        {
            if (_isFinished)
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

            _time +=
                Time.deltaTime;

            MovePrana();
        }

        /// <summary>
        /// 現在のプラーナを対象ユニットへ脅威として通知します。
        /// </summary>
        /// <param name="isTracking">
        /// 対象を継続追跡するプラーナの場合は<c>true</c>です。
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

                    IsTracking =
                        isTracking
                };

            targetUnit.NotifyThreat(
                threatData);

            _lastThreatNotifyTime =
                Time.time;
        }

        /// <summary>
        /// 現在の進行方向と速度を使用して
        /// プラーナを直線移動させます。
        /// </summary>
        /// <remarks>
        /// 旧メソッド名の<c>LerpMove</c>とは異なり、
        /// 補間ではなく一定速度での直線移動です。
        /// </remarks>
        protected virtual void MovePrana()
        {
            if (Context == null)
            {
                return;
            }

            Vector3 movement =
                _direction *
                Mathf.Max(
                    0.0f,
                    Context.Speed) *
                Time.deltaTime;

            _collisionSweep ??= new ProjectileCollisionSweep(
                transform, GetComponent<Collider>(), OnTriggerEnter, () => _isFinished);
            if (!_collisionSweep.Move(transform.position + movement))
            {
                return;
            }

            if (_time >
                k_MaximumLifeTime)
            {
                DestroyLocally();
            }
        }

        /// <summary>
        /// プラーナがColliderへ進入したときの
        /// 命中処理を実行します。
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
                RequestHitSound(hitCollider);

                if (TryApplyDirectShield(
                        directShield,
                        projectileDirection) &&
                    Context.Attack <= 0.0f)
                {
                    DestroyLocally();
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
        /// 指定されたプレイヤーの対象Transformを取得します。
        /// </summary>
        /// <param name="playerId">
        /// 対象プレイヤーのIDです。
        /// </param>
        /// <param name="targetTransform">
        /// 取得されたミクロユニットのTransformです。
        /// </param>
        /// <returns>
        /// 有効な対象を取得できた場合は<c>true</c>です。
        /// </returns>
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
        /// <param name="playerId">
        /// 取得するプレイヤーのIDです。
        /// </param>
        /// <param name="targetUnit">
        /// 取得されたユニットです。
        /// </param>
        /// <returns>
        /// 有効なユニットを取得できた場合は<c>true</c>です。
        /// </returns>
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
        /// 脅威予測で使用するプラーナ固有のIDを取得します。
        /// </summary>
        /// <returns>
        /// 所有者、メイン・サブおよびプラーナIDから
        /// 生成した識別子です。
        /// </returns>
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
        /// <param name="hitCollider">
        /// 判定対象のColliderです。
        /// </param>
        /// <returns>
        /// 環境Colliderの場合は<c>true</c>です。
        /// </returns>
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
        /// <returns>
        /// 正規化済みの進行方向です。
        /// </returns>
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
        /// <param name="shield">
        /// 接触したシールドです。
        /// </param>
        /// <param name="projectileDirection">
        /// プラーナの進行方向です。
        /// </param>
        /// <returns>
        /// シールドへダメージを適用した場合は<c>true</c>です。
        /// </returns>
        private bool TryApplyDirectShield(
            ShieldPranaBase shield,
            Vector3 projectileDirection)
        {
            if (shield == null ||
                Context == null)
            {
                return false;
            }

            if (IsOwnerOrAllyPlayer(
                    shield.OwnerPlayerId))
            {
                return false;
            }

            if (shield.GetPranaHealth() <=
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
        /// 本来シールドへ命中していたかどうかを判定します。
        /// </summary>
        /// <param name="targetPlayerId">
        /// 命中対象のプレイヤーIDです。
        /// </param>
        /// <param name="origin">
        /// Raycastの開始位置です。
        /// </param>
        /// <param name="projectileDirection">
        /// プラーナの進行方向です。
        /// </param>
        /// <returns>
        /// シールドへダメージを適用した場合は<c>true</c>です。
        /// </returns>
        private bool TryApplyForwardBackwardRaycastShield(
            int targetPlayerId,
            Vector3 origin,
            Vector3 projectileDirection)
        {
            if (Context == null ||
                projectileDirection.sqrMagnitude <
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
        /// 指定された方向へRaycastし、
        /// 防御に使用できる最も近いシールドを検索します。
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
        /// <param name="targetCollider">
        /// 厚みを計算するColliderです。
        /// </param>
        /// <param name="direction">
        /// 厚みを計算する方向です。
        /// </param>
        /// <returns>
        /// 指定方向に投影したColliderの厚みです。
        /// </returns>
        private static float GetColliderThicknessAlongDirection(
            Collider targetCollider,
            Vector3 direction)
        {
            if (targetCollider == null)
            {
                return k_MinimumColliderThickness;
            }

            if (direction.sqrMagnitude <
                k_MinimumDirectionSqrMagnitude)
            {
                return k_MinimumColliderThickness;
            }

            direction.Normalize();

            if (targetCollider is BoxCollider boxCollider)
            {
                Transform boxTransform =
                    boxCollider.transform;

                Vector3 scaledSize =
                    Vector3.Scale(
                        boxCollider.size,
                        Abs(
                            boxTransform.lossyScale));

                float thickness =
                    Mathf.Abs(
                        Vector3.Dot(
                            direction,
                            boxTransform.right)) *
                    scaledSize.x +
                    Mathf.Abs(
                        Vector3.Dot(
                            direction,
                            boxTransform.up)) *
                    scaledSize.y +
                    Mathf.Abs(
                        Vector3.Dot(
                            direction,
                            boxTransform.forward)) *
                    scaledSize.z;

                return Mathf.Max(
                    thickness,
                    k_MinimumColliderThickness);
            }

            Bounds bounds =
                targetCollider.bounds;

            float boundsThickness =
                Mathf.Abs(direction.x) *
                bounds.size.x +
                Mathf.Abs(direction.y) *
                bounds.size.y +
                Mathf.Abs(direction.z) *
                bounds.size.z;

            return Mathf.Max(
                boundsThickness,
                k_MinimumColliderThickness);
        }

        /// <summary>
        /// 指定されたVector3の各要素を絶対値へ変換します。
        /// </summary>
        private static Vector3 Abs(
            Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }


        /// <summary>
        /// シールドへダメージを適用し、
        /// シールドで吸収した分だけプラーナの攻撃力を減少させます。
        /// </summary>
        /// <param name="shield">
        /// ダメージを適用するシールドです。
        /// </param>
        /// <returns>
        /// シールドへダメージを適用した場合は<c>true</c>です。
        /// </returns>
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
        /// <returns>
        /// シールドレイヤーのみを含むLayerMaskです。
        /// シールドレイヤーが存在しない場合は全レイヤーです。
        /// </returns>
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
        /// このプラーナをローカル環境から破棄します。
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
    }
}
