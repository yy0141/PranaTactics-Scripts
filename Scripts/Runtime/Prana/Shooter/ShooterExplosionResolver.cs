using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Combat;
using PranaTactics.Pranam;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 球状の爆風による直撃、距離減衰とシールドの吸収を裁定します。
    /// </summary>
    internal sealed class ShooterExplosionResolver
    {
        /// <summary>
        /// 発射時の弾の性能です。
        /// </summary>
        private readonly PranamContext _context;

        /// <summary>
        /// ユニットへの命中を既存の通信経路へ渡します。
        /// </summary>
        private readonly Action<int, float> _applyHit;

        /// <summary>
        /// 所有者と味方へのダメージを除外します。
        /// </summary>
        private readonly Func<int, bool> _isAlly;

        /// <summary>
        /// 板状シールドが防御可能な方向の閾値です。
        /// </summary>
        private readonly float _faceDotThreshold;

        /// <summary>
        /// 生成コンテキストと命中処理の境界を設定します。
        /// </summary>
        public ShooterExplosionResolver(
            PranamContext context,
            Action<int, float> applyHit,
            Func<int, bool> isAlly,
            float faceDotThreshold)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _applyHit = applyHit ?? throw new ArgumentNullException(nameof(applyHit));
            _isAlly = isAlly ?? throw new ArgumentNullException(nameof(isAlly));
            _faceDotThreshold = faceDotThreshold;
        }

        /// <summary>
        /// 着弾地点から全方向へ爆風を発生させ、各ユニットへ一度だけ命中を通知します。
        /// </summary>
        /// <remarks>
        /// シールド耐久値と遮蔽関係を先に保存し、破壊順やCollider列挙順で
        /// 吸収結果が変わることを防ぎます。直撃対象には爆風を重ねません。
        /// シールドを破壊した場合は、吸収後の残余ダメージだけが通過します。
        /// </remarks>
        public void Explode(Vector3 origin, Vector3 projectileDirection,
            Collider directCollider, ShieldPranaBase impactShield)
        {
            Physics.SyncTransforms();
            Collider[] overlaps = Physics.OverlapSphere(
                origin, _context.Explosion.Radius, LayerMask.GetMask("Unit", "Stealth", "Shield"),
                QueryTriggerInteraction.Collide);
            Dictionary<int, Collider> targets = CollectTargets(overlaps, origin);
            int directPlayerId = GetUnitPlayerId(directCollider);
            if (directPlayerId >= 0 && !_isAlly(directPlayerId))
            {
                targets[directPlayerId] = directCollider;
            }

            List<ShieldImpact> shields = CollectShields(overlaps, impactShield);
            var hits = new List<UnitImpact>();
            foreach (KeyValuePair<int, Collider> target in targets)
            {
                Vector3 targetPoint = target.Value.ClosestPoint(origin);
                bool isDirect = target.Key == directPlayerId;
                float damage = isDirect ? _context.Attack :
                    _context.Explosion.CalculateDamage(
                        _context.Attack, Vector3.Distance(origin, targetPoint));
                Vector3 direction = targetPoint - origin;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = projectileDirection;
                }

                List<ShieldImpact> blockers = FindBlockers(
                    shields, origin, targetPoint, direction, impactShield, target.Key);
                foreach (ShieldImpact blocker in blockers)
                {
                    blocker.Damage = Mathf.Max(blocker.Damage, damage);
                    damage = Mathf.Max(0.0f, damage - blocker.Health);
                }

                if (damage > 0.0f)
                {
                    hits.Add(new UnitImpact(target.Key, damage));
                }
            }

            foreach (ShieldImpact shield in shields)
            {
                float damage = CalculateShieldDamage(shield, origin, projectileDirection, impactShield);
                shield.Shield.ApplyCombatDamage(_context.PlayerId, damage, _context.IsMain);
            }

            foreach (UnitImpact hit in hits)
            {
                _applyHit(hit.PlayerId, hit.Damage);
            }
        }

        /// <summary>
        /// 同一ユニットの複数Colliderを爆心に最も近い一つへ集約します。
        /// </summary>
        private Dictionary<int, Collider> CollectTargets(Collider[] overlaps, Vector3 origin)
        {
            var targets = new Dictionary<int, Collider>();
            foreach (Collider collider in overlaps)
            {
                int playerId = GetUnitPlayerId(collider);
                if (playerId < 0 || _isAlly(playerId))
                {
                    continue;
                }

                if (!targets.TryGetValue(playerId, out Collider previous) ||
                    (collider.ClosestPoint(origin) - origin).sqrMagnitude <
                    (previous.ClosestPoint(origin) - origin).sqrMagnitude)
                {
                    targets[playerId] = collider;
                }
            }

            return targets;
        }

        /// <summary>
        /// 弾やシールドを除き、ユニットのダメージ対象IDを取得します。
        /// </summary>
        private static int GetUnitPlayerId(Collider collider)
        {
            if (collider == null || collider.GetComponentInParent<PranaBase>() != null)
            {
                return -1;
            }

            IDamageable target = collider.GetComponentInParent<IDamageable>();
            return target != null ? target.PlayerId : -1;
        }

        /// <summary>
        /// 爆風範囲内と直撃したシールドの耐久値を保存します。
        /// </summary>
        private List<ShieldImpact> CollectShields(Collider[] overlaps, ShieldPranaBase impactShield)
        {
            var shields = new List<ShieldImpact>();
            var registered = new HashSet<ShieldPranaBase>();
            foreach (Collider collider in overlaps)
            {
                ShieldPranaBase shield = collider.GetComponentInParent<ShieldPranaBase>();
                if (shield == null || _isAlly(shield.OwnerPlayerId) ||
                    shield.GetPranaHealth() <= 0.0f || !registered.Add(shield))
                {
                    continue;
                }

                shields.Add(new ShieldImpact(shield));
            }

            if (impactShield != null && registered.Add(impactShield))
            {
                shields.Add(new ShieldImpact(impactShield));
            }

            return shields;
        }

        /// <summary>
        /// 爆心から対象へ向かう区間を遮るシールドを近い順に取得します。
        /// </summary>
        private List<ShieldImpact> FindBlockers(List<ShieldImpact> shields,
            Vector3 origin, Vector3 targetPoint, Vector3 direction, ShieldPranaBase impactShield,
            int targetPlayerId)
        {
            var blockers = new List<ShieldImpact>();
            foreach (ShieldImpact shield in shields)
            {
                if (!shield.Shield.CanBlockProjectile(direction, _faceDotThreshold))
                {
                    continue;
                }

                bool blocks = shield.Shield == impactShield &&
                    shield.Shield.OwnerPlayerId == targetPlayerId;
                float distance = float.MaxValue;
                foreach (Collider collider in shield.Colliders)
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    bool containsOrigin = (collider.ClosestPoint(origin) - origin).sqrMagnitude < 0.000001f;
                    bool containsTarget = (collider.ClosestPoint(targetPoint) - targetPoint).sqrMagnitude < 0.000001f;
                    if (shield.Shield.BlocksFromAllDirections && containsTarget)
                    {
                        blocks = true;
                        distance = Mathf.Min(distance, Vector3.Distance(origin, collider.ClosestPoint(origin)));
                    }

                    if (shield.Shield.BlocksFromAllDirections && containsOrigin && !containsTarget &&
                        collider.Raycast(new Ray(targetPoint, -direction.normalized),
                            out RaycastHit exitHit, Vector3.Distance(origin, targetPoint) - 0.001f))
                    {
                        blocks = true;
                        distance = Mathf.Min(distance, Vector3.Distance(origin, targetPoint) - exitHit.distance);
                    }

                    if (collider.Raycast(new Ray(origin, direction.normalized),
                            out RaycastHit hit, Vector3.Distance(origin, targetPoint) + 0.001f))
                    {
                        blocks = true;
                        distance = Mathf.Min(distance, hit.distance);
                    }
                }

                if (blocks)
                {
                    shield.Distance = shield.Shield == impactShield ? 0.0f : distance;
                    blockers.Add(shield);
                }
            }

            blockers.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            return blockers;
        }

        /// <summary>
        /// シールド自身が受ける爆風と、そのシールドが遮った最大威力を統合します。
        /// </summary>
        private float CalculateShieldDamage(ShieldImpact shield, Vector3 origin,
            Vector3 projectileDirection, ShieldPranaBase impactShield)
        {
            if (shield.Shield == impactShield)
            {
                return Mathf.Max(shield.Damage, _context.Attack);
            }

            float damage = shield.Damage;
            foreach (Collider collider in shield.Colliders)
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 offset = collider.ClosestPoint(origin) - origin;
                Vector3 direction = offset.sqrMagnitude > 0.0001f ? offset : projectileDirection;
                if (shield.Shield.CanBlockProjectile(direction, _faceDotThreshold))
                {
                    damage = Mathf.Max(damage,
                        _context.Explosion.CalculateDamage(_context.Attack, offset.magnitude));
                }
            }

            return damage;
        }

        /// <summary>
        /// 一つの爆発に対するシールドの初期状態と受ける最大ダメージです。
        /// </summary>
        private sealed class ShieldImpact
        {
            public ShieldPranaBase Shield { get; }
            public Collider[] Colliders { get; }
            public float Health { get; }
            public float Damage { get; set; }
            public float Distance { get; set; }

            /// <summary>
            /// ダメージ適用前の耐久値とColliderを保存します。
            /// </summary>
            public ShieldImpact(ShieldPranaBase shield)
            {
                Shield = shield;
                Colliders = shield.GetComponentsInChildren<Collider>();
                Health = shield.GetPranaHealth();
            }
        }

        /// <summary>
        /// シールド吸収後にユニットへ通知する一回分の命中です。
        /// </summary>
        private sealed class UnitImpact
        {
            public int PlayerId { get; }
            public float Damage { get; }

            /// <summary>
            /// 確定した対象と残余ダメージを保持します。
            /// </summary>
            public UnitImpact(int playerId, float damage)
            {
                PlayerId = playerId;
                Damage = damage;
            }
        }
    }
}
