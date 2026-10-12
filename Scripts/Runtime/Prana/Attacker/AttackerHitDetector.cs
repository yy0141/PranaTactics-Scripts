using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Combat;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 近接攻撃プラーナの当たり判定を検出し、
    /// 有効な対象を<see cref="AttackerPranaBase"/>へ通知します。
    /// </summary>
    /// <remarks>
    /// 攻撃判定が有効になった時点ですでに重なっている対象と、
    /// その後Triggerへ進入した対象の両方を処理します。
    ///
    /// 同じユニットが複数のColliderを持つ場合でも、
    /// 1回の攻撃中に同じ対象へ複数回ダメージを適用しません。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class AttackerHitDetector : MonoBehaviour
    {
        /// <summary>
        /// 命中処理を実行する近接攻撃プラーナです。
        /// </summary>
        [SerializeField]
        private AttackerPranaBase _attackerPrana;

        /// <summary>
        /// 初期重なり判定とTrigger判定の対象とするレイヤーです。
        /// </summary>
        [Header("Collision")]
        [SerializeField]
        private LayerMask _collisionMask = ~0;

        /// <summary>
        /// 攻撃判定として使用するBoxColliderです。
        /// </summary>
        private BoxCollider _hitbox;

        /// <summary>
        /// この攻撃ですでに処理したプレイヤーIDです。
        /// </summary>
        private readonly HashSet<int>
            _processedPlayerIds = new();

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void Awake()
        {
            CacheReferences();
        }

        /// <summary>
        /// コンポーネントが有効になったとき、
        /// 前回の命中対象を初期化します。
        /// </summary>
        private void OnEnable()
        {
            _processedPlayerIds.Clear();
        }

        /// <summary>
        /// 攻撃判定の生成時点ですでに重なっている対象を確認します。
        /// </summary>
        private void Start()
        {
            CheckInitialOverlap();
        }

        /// <summary>
        /// 攻撃判定へ進入したColliderを処理します。
        /// </summary>
        /// <param name="hitCollider">
        /// 攻撃判定へ進入したColliderです。
        /// </param>
        private void OnTriggerEnter(
            Collider hitCollider)
        {
            ProcessHitCollider(
                hitCollider);
        }

        /// <summary>
        /// 指定されたColliderが有効な攻撃対象である場合、
        /// 近接攻撃プラーナへ命中処理を通知します。
        /// </summary>
        /// <param name="hitCollider">
        /// 判定対象のColliderです。
        /// </param>
        private void ProcessHitCollider(
            Collider hitCollider)
        {
            if (_attackerPrana == null ||
                ShouldIgnore(
                    hitCollider))
            {
                return;
            }

            if (!TryGetTargetPlayerId(
                    hitCollider,
                    out int targetPlayerId))
            {
                return;
            }

            if (!_processedPlayerIds.Add(
                    targetPlayerId))
            {
                return;
            }

            _attackerPrana.HandleHit(
                hitCollider);
        }

        /// <summary>
        /// 指定されたColliderを攻撃判定から
        /// 除外するべきか判定します。
        /// </summary>
        /// <param name="hitCollider">
        /// 判定対象のColliderです。
        /// </param>
        /// <returns>
        /// 自身の攻撃オブジェクト、攻撃者自身、
        /// または有効な対象情報を持たない場合は<c>true</c>です。
        /// </returns>
        private bool ShouldIgnore(
            Collider hitCollider)
        {
            if (hitCollider == null ||
                _attackerPrana == null)
            {
                return true;
            }

            if (hitCollider.transform.root ==
                transform.root)
            {
                return true;
            }

            if (!TryGetTargetPlayerId(
                    hitCollider,
                    out int targetPlayerId))
            {
                return true;
            }

            return targetPlayerId ==
                _attackerPrana.OwnerPlayerId;
        }

        /// <summary>
        /// 指定されたColliderから攻撃対象の
        /// プレイヤーIDを取得します。
        /// </summary>
        /// <param name="hitCollider">
        /// 対象情報を取得するColliderです。
        /// </param>
        /// <param name="targetPlayerId">
        /// 取得された対象プレイヤーIDです。
        /// </param>
        /// <returns>
        /// 有効なプレイヤーIDを取得できた場合は<c>true</c>です。
        /// </returns>
        private static bool TryGetTargetPlayerId(
            Collider hitCollider,
            out int targetPlayerId)
        {
            targetPlayerId = -1;

            if (hitCollider == null)
            {
                return false;
            }

            ShieldPranaBase shield =
                hitCollider.GetComponentInParent<ShieldPranaBase>();

            if (shield != null)
            {
                targetPlayerId =
                    shield.OwnerPlayerId;

                return targetPlayerId >= 0;
            }

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                targetPlayerId =
                    damageable.PlayerId;

                return targetPlayerId >= 0;
            }

            UnitBehaviorBase unit =
                hitCollider.GetComponentInParent<UnitBehaviorBase>();

            if (unit == null)
            {
                return false;
            }

            UnitStatus unitStatus =
                unit.GetUnitStatus();

            if (unitStatus == null)
            {
                return false;
            }

            targetPlayerId =
                unitStatus.PlayerId;

            return targetPlayerId >= 0;
        }

        /// <summary>
        /// 攻撃判定の生成時点でBoxColliderと
        /// 重なっている全ての対象を処理します。
        /// </summary>
        private void CheckInitialOverlap()
        {
            if (_attackerPrana == null)
            {
                Debug.LogError(
                    $"{nameof(AttackerHitDetector)}: " +
                    $"{nameof(AttackerPranaBase)}が設定されていません。",
                    this);

                return;
            }

            if (_hitbox == null)
            {
                CacheReferences();
            }

            if (_hitbox == null)
            {
                return;
            }

            Vector3 worldCenter =
                transform.TransformPoint(
                    _hitbox.center);

            Vector3 absoluteScale =
                GetAbsoluteVector(
                    transform.lossyScale);

            Vector3 worldHalfExtents =
                Vector3.Scale(
                    _hitbox.size * 0.5f,
                    absoluteScale);

            Collider[] overlappingColliders =
                Physics.OverlapBox(
                    worldCenter,
                    worldHalfExtents,
                    transform.rotation,
                    _collisionMask,
                    QueryTriggerInteraction.Collide);

            foreach (Collider overlappingCollider in
                    overlappingColliders)
            {
                ProcessHitCollider(
                    overlappingCollider);
            }
        }

        /// <summary>
        /// 必要なコンポーネントへの参照を取得します。
        /// </summary>
        private void CacheReferences()
        {
            _hitbox =
                GetComponent<BoxCollider>();

            if (_hitbox == null)
            {
                Debug.LogError(
                    $"{nameof(AttackerHitDetector)}: " +
                    $"{nameof(BoxCollider)}を取得できません。",
                    this);
            }

            if (_attackerPrana == null)
            {
                _attackerPrana =
                    GetComponentInParent<AttackerPranaBase>();
            }

            if (_attackerPrana == null)
            {
                Debug.LogError(
                    $"{nameof(AttackerHitDetector)}: " +
                    $"{nameof(AttackerPranaBase)}を取得できません。",
                    this);
            }
        }

        /// <summary>
        /// 指定されたベクトルの各成分を絶対値へ変換します。
        /// </summary>
        /// <param name="value">
        /// 変換するベクトルです。
        /// </param>
        /// <returns>
        /// 各成分を絶対値へ変換したベクトルです。
        /// </returns>
        private static Vector3 GetAbsoluteVector(
            Vector3 value)
        {
            return new Vector3(
                Mathf.Abs(
                    value.x),
                Mathf.Abs(
                    value.y),
                Mathf.Abs(
                    value.z));
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector上の設定を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_attackerPrana == null)
            {
                _attackerPrana =
                GetComponentInParent<AttackerPranaBase>();
            }

            BoxCollider hitbox =
                GetComponent<BoxCollider>();

            if (hitbox != null &&
                !hitbox.isTrigger)
            {
                Debug.LogWarning(
                    $"{nameof(AttackerHitDetector)}: " +
                    $"{nameof(BoxCollider)}の" +
                    $"{nameof(BoxCollider.isTrigger)}を有効にしてください。",
                    this);
            }
        }
#endif
    }
}
