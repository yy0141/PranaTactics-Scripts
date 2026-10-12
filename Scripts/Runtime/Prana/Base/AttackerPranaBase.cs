using UnityEngine;

using PranaTactics.Audio;
using PranaTactics.Combat;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 近接攻撃によって生成されるプラーナの共通処理を提供します。
    /// </summary>
    /// <remarks>
    /// シールドまたは通常ユニットへの命中を判定し、
    /// <see cref="Pranam.PranamBehavior"/>を通して
    /// ダメージとプラーナの破棄を同期します。
    /// </remarks>
    public abstract class AttackerPranaBase : PranaBase
    {
        /// <summary>
        /// この斬撃で選択対象への命中を適用済みかどうかです。
        /// </summary>
        private bool _hasAppliedHit;

        /// <summary>
        /// Prefabの当たり判定とエフェクトを兵装の最大攻撃距離以内へ縮小します。
        /// </summary>
        /// <remarks>
        /// 子オブジェクトと親オブジェクトの拡大率を含めた判定の全頂点を確認します。
        /// Prefabの見た目用スケールで攻撃が遠くの敵へ届くことを防ぎます。
        /// </remarks>
        protected override void OnInitialized()
        {
            _hasAppliedHit = false;
            base.OnInitialized();
            float meleeRange = Context.MeleeRange;
            if (meleeRange <= 0.0f || float.IsNaN(meleeRange) || float.IsInfinity(meleeRange))
            {
                return;
            }

            float maximumDistance = 0.0f;
            foreach (BoxCollider hitbox in GetComponentsInChildren<BoxCollider>(true))
            {
                Vector3 halfSize = hitbox.size * 0.5f;
                for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
                {
                    Vector3 corner = hitbox.center + new Vector3(
                        (cornerIndex & 1) == 0 ? -halfSize.x : halfSize.x,
                        (cornerIndex & 2) == 0 ? -halfSize.y : halfSize.y,
                        (cornerIndex & 4) == 0 ? -halfSize.z : halfSize.z);
                    float distance = (hitbox.transform.TransformPoint(corner) - transform.position).magnitude;
                    maximumDistance = Mathf.Max(maximumDistance, distance);
                }
            }

            if (maximumDistance > meleeRange)
            {
                transform.localScale *= meleeRange / maximumDistance;
            }
        }

        /// <summary>
        /// ミクロ・フェイズの終了と命中処理を更新します。
        /// </summary>
        protected virtual void Update()
        {
            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null)
            {
                return;
            }

            if (phaseManager.CurrentPhase ==
                MatchPhase.Micro)
            {
                return;
            }

            Destroy(
                gameObject);
        }

        /// <summary>
        /// この攻撃プラーナの破棄を要求します。
        /// </summary>
        /// <remarks>
        /// 直接<see cref="Object.Destroy(Object)"/>を呼び出すのではなく、
        /// <see cref="Pranam.PranamBehavior"/>を経由して
        /// 各クライアントへ破棄を同期します。
        /// </remarks>
        public void DestroyPrana()
        {
            if (PranamBehavior == null ||
                Context == null)
            {
                Debug.LogWarning(
                    $"{nameof(AttackerPranaBase)}: " +
                    "プラーナの破棄に必要な情報がありません。",
                    this);

                return;
            }

            PranamBehavior.DestroyPrana(
                Context.Id);
        }

        /// <summary>
        /// 指定されたColliderへの命中処理を実行します。
        /// </summary>
        /// <param name="hitCollider">
        /// 攻撃プラーナが接触したColliderです。
        /// </param>
        public void HandleHit(
            Collider hitCollider)
        {
            if (IsOwnerCollider(hitCollider))
            {
                return;
            }

            if (!CanProcessHit(
                    hitCollider))
            {
                return;
            }

            if (IsOwnerOrAllyCollider(
                    hitCollider))
            {
                DestroyPrana();

                return;
            }

            ShieldPranaBase shield =
                hitCollider.GetComponentInParent<ShieldPranaBase>();

            if (shield != null)
            {
                HandleShieldHit(
                    shield);

                return;
            }

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            ApplyDamageToTarget(
                damageable);
        }

        /// <summary>
        /// 未命中の斬撃が選択対象のColliderへ接触したか判定します。
        /// </summary>
        /// <param name="hitCollider">
        /// 命中対象のColliderです。
        /// </param>
        /// <returns>
        /// 選択した生存対象が実際の戦闘位置で攻撃距離内にいる場合は<c>true</c>です。
        /// </returns>
        private bool CanProcessHit(
            Collider hitCollider)
        {
            if (hitCollider == null || _hasAppliedHit)
            {
                return false;
            }

            if (Context == null)
            {
                Debug.LogWarning(
                    $"{nameof(AttackerPranaBase)}: " +
                    $"{nameof(Pranam.PranamContext)}が設定されていません。",
                    this);

                return false;
            }

            if (PranamBehavior == null)
            {
                Debug.LogWarning(
                    $"{nameof(AttackerPranaBase)}: " +
                    $"{nameof(Pranam.PranamBehavior)}が設定されていません。",
                    this);

                return false;
            }

            if (Context.TargetPlayerId < 0)
            {
                return false;
            }

            ShieldPranaBase shield = hitCollider.GetComponentInParent<ShieldPranaBase>();
            int targetPlayerId = shield != null
                ? shield.OwnerPlayerId
                : hitCollider.GetComponentInParent<IDamageable>()?.PlayerId ?? -1;
            if (targetPlayerId != Context.TargetPlayerId ||
                Context.MeleeRange <= 0.0f || float.IsNaN(Context.MeleeRange) || float.IsInfinity(Context.MeleeRange))
            {
                return false;
            }

            UnitBehavior targetUnit = shield != null
                ? shield.GetComponentInParent<UnitBehavior>()
                : hitCollider.GetComponentInParent<UnitBehavior>();
            MicroUnitBehavior targetMicroUnit = targetUnit?.GetMicroUnitBehavior();
            if (targetUnit?.GetUnitStatus() == null || !targetUnit.GetUnitStatus().IsAlive || targetMicroUnit == null)
            {
                return false;
            }

            float squaredDistance = (targetMicroUnit.transform.position - transform.position).sqrMagnitude;
            return squaredDistance <= Context.MeleeRange * Context.MeleeRange;
        }

        /// <summary>
        /// シールドへ命中した場合の処理を実行します。
        /// </summary>
        /// <param name="shield">
        /// 命中したシールドです。
        /// </param>
        private void HandleShieldHit(
            ShieldPranaBase shield)
        {
            if (shield == null)
            {
                return;
            }

            if (IsOwnerOrAllyPlayer(
                    shield.OwnerPlayerId))
            {
                DestroyPrana();

                return;
            }

            float currentShieldHealth =
                Mathf.Max(
                    0.0f,
                    shield.GetPranaHealth());

            if (currentShieldHealth > 0.0f)
            {
                shield.ApplyCombatDamage(
                    Context.PlayerId,
                    currentShieldHealth,
                    Context.IsMain);
            }

            UnitBehavior ownerUnit =
                shield.GetComponentInParent<UnitBehavior>();

            if (ownerUnit == null)
            {
                return;
            }

            IDamageable ownerDamageable =
                ownerUnit.GetComponent<IDamageable>();

            ApplyDamageToTarget(
                ownerDamageable);
        }

        /// <summary>
        /// 指定されたダメージ対象へ攻撃を適用します。
        /// </summary>
        /// <param name="damageable">
        /// ダメージを受ける対象です。
        /// </param>
        private void ApplyDamageToTarget(
            IDamageable damageable)
        {
            if (damageable == null || _hasAppliedHit ||
                damageable.PlayerId != Context.TargetPlayerId ||
                IsOwnerOrAllyPlayer(
                    damageable.PlayerId))
            {
                return;
            }

            float attackPower =
                Mathf.Max(
                    0.0f,
                    Context.Attack);

            if (attackPower <= 0.0f)
            {
                return;
            }

            _hasAppliedHit = true;
            RequestPranaSound(PranamSoundEvent.UnitHit, transform.position);
            PranamBehavior.HitPrana(
                Context.PlayerId,
                Context.Id,
                damageable.PlayerId,
                attackPower,
                false,
                hitSlowdownRatio: Context.HitSlowdownRatio,
                hitSlowdownDurationSeconds: Context.HitSlowdownDurationSeconds);
        }

    }
}
