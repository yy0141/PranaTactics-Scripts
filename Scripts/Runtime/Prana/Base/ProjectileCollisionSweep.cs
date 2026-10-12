using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 弾の移動区間を実際のCollider形状で検査し、高速移動中のすり抜けを防ぎます。
    /// </summary>
    internal sealed class ProjectileCollisionSweep
    {
        /// <summary>
        /// 検査形状の最小サイズです。
        /// </summary>
        private const float k_MinimumSize = 0.001f;

        /// <summary>
        /// UnitのTransformを物理シーンへ反映した最後のフレームです。
        /// </summary>
        private static int s_lastSynchronizationFrame = -1;

        /// <summary>
        /// 移動対象の弾です。
        /// </summary>
        private readonly Transform _transform;

        /// <summary>
        /// 検査に使用する弾のColliderです。
        /// </summary>
        private readonly Collider _collider;

        /// <summary>
        /// 通常の接触通知と共通の命中処理です。
        /// </summary>
        private readonly Action<Collider> _applyCollision;

        /// <summary>
        /// ダメージ適用や破棄による飛行終了の確認処理です。
        /// </summary>
        private readonly Func<bool> _isFinished;

        /// <summary>
        /// 弾・視界・UIの検出専用領域を除外するレイヤーです。
        /// </summary>
        private readonly int _collisionMask;

        /// <summary>
        /// 再利用する開始位置の重なり検出バッファです。
        /// </summary>
        private Collider[] _overlaps = new Collider[8];

        /// <summary>
        /// 再利用する通過区間の接触検出バッファです。
        /// </summary>
        private RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>
        /// 距離順の比較処理です。
        /// </summary>
        private static readonly IComparer<RaycastHit> s_hitComparer =
            Comparer<RaycastHit>.Create(CompareHitDistance);

        /// <summary>
        /// 弾の形状と既存の命中処理を接続します。
        /// </summary>
        public ProjectileCollisionSweep(
            Transform projectileTransform,
            Collider projectileCollider,
            Action<Collider> applyCollision,
            Func<bool> isFinished)
        {
            _transform = projectileTransform != null
                ? projectileTransform : throw new ArgumentNullException(nameof(projectileTransform));
            _collider = projectileCollider;
            _collisionMask = ~LayerMask.GetMask("Trigger", "TriggerRange", "Sight", "UI", "Outline");
            _applyCollision = applyCollision ?? throw new ArgumentNullException(nameof(applyCollision));
            _isFinished = isFinished ?? throw new ArgumentNullException(nameof(isFinished));
        }

        /// <summary>
        /// 開始位置の重なりと移動区間を検査し、手前から順に命中処理を適用します。
        /// </summary>
        /// <remarks>
        /// Unit・防御はTriggerでも検出します。所有者や味方、防御の残余ダメージは
        /// 各兵装の既存処理で裁定し、弾が終了した時点で後方への検査を打ち切ります。
        /// </remarks>
        /// <returns>
        /// 弾が消費されずに移動先へ進めた場合は<c>true</c>です。
        /// </returns>
        public bool Move(Vector3 destination)
        {
            if (_isFinished())
            {
                return false;
            }

            Vector3 start = _transform.position;
            Vector3 movement = destination - start;
            float distance = movement.magnitude;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return false;
            }

            if (s_lastSynchronizationFrame != Time.frameCount)
            {
                Physics.SyncTransforms();
                s_lastSynchronizationFrame = Time.frameCount;
            }

            Vector3 direction = distance > 0.0f ? movement / distance : Vector3.zero;
            int overlapCount;
            int hitCount;
            if (_collider is BoxCollider box)
            {
                Vector3 scale = _transform.lossyScale;
                Vector3 halfExtents = Vector3.Scale(box.size * 0.5f,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                halfExtents = Vector3.Max(halfExtents, Vector3.one * k_MinimumSize);
                Vector3 center = _transform.TransformPoint(box.center);
                overlapCount = CollectOverlaps(center, halfExtents, 0.0f, true);
                hitCount = CollectHits(center, halfExtents, 0.0f, true, direction, distance);
            }
            else
            {
                Vector3 center = _collider != null ? _collider.bounds.center : start;
                float radius = _collider != null ? _collider.bounds.extents.magnitude : k_MinimumSize;
                if (_collider is SphereCollider sphere)
                {
                    Vector3 scale = _transform.lossyScale;
                    center = _transform.TransformPoint(sphere.center);
                    radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x),
                        Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                }

                radius = Mathf.Max(k_MinimumSize, radius);
                overlapCount = CollectOverlaps(center, Vector3.zero, radius, false);
                hitCount = CollectHits(center, Vector3.zero, radius, false, direction, distance);
            }

            for (int index = 0; index < overlapCount; index++)
            {
                if (ApplyCollision(_overlaps[index]))
                {
                    return false;
                }
            }

            Array.Sort(_hits, 0, hitCount, s_hitComparer);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hits[index];
                _transform.position = start + direction * Mathf.Clamp(hit.distance, 0.0f, distance);
                if (ApplyCollision(hit.collider))
                {
                    return false;
                }
            }

            _transform.position = destination;
            return true;
        }

        /// <summary>
        /// 開始位置の重なりを取得し、容量不足の場合は拡張して取り直します。
        /// </summary>
        private int CollectOverlaps(Vector3 center, Vector3 halfExtents, float radius, bool isBox)
        {
            while (true)
            {
                int count = isBox
                    ? Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps,
                        _transform.rotation, _collisionMask, QueryTriggerInteraction.Collide)
                    : Physics.OverlapSphereNonAlloc(center, radius, _overlaps,
                        _collisionMask, QueryTriggerInteraction.Collide);
                if (count < _overlaps.Length)
                {
                    return count;
                }

                Array.Resize(ref _overlaps, _overlaps.Length * 2);
            }
        }

        /// <summary>
        /// 通過区間の接触を取得し、容量不足による候補の欠落を防ぎます。
        /// </summary>
        private int CollectHits(Vector3 center, Vector3 halfExtents, float radius,
            bool isBox, Vector3 direction, float distance)
        {
            if (distance <= 0.0f)
            {
                return 0;
            }

            while (true)
            {
                int count = isBox
                    ? Physics.BoxCastNonAlloc(center, halfExtents, direction, _hits,
                        _transform.rotation, distance, _collisionMask, QueryTriggerInteraction.Collide)
                    : Physics.SphereCastNonAlloc(center, radius, direction, _hits,
                        distance, _collisionMask, QueryTriggerInteraction.Collide);
                if (count < _hits.Length)
                {
                    return count;
                }

                Array.Resize(ref _hits, _hits.Length * 2);
            }
        }

        /// <summary>
        /// 自分自身を除外して既存の命中処理を実行します。
        /// </summary>
        private bool ApplyCollision(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider == _collider ||
                hitCollider.transform.IsChildOf(_transform))
            {
                return false;
            }

            _applyCollision(hitCollider);
            return _isFinished();
        }

        /// <summary>
        /// 接触候補を移動開始位置からの距離順に並べます。
        /// </summary>
        private static int CompareHitDistance(RaycastHit first, RaycastHit second)
        {
            return first.distance.CompareTo(second.distance);
        }
    }
}
