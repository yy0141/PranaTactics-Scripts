using UnityEngine;

using PranaTactics.Combat;
using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.Prana
{
    /// <summary>
    /// 起動後に設置地点でEruptと共通の球状爆風を発生させる罠です。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Landmine : TrapperPranaBase
    {
        /// <summary>
        /// Eruptと同じ板状シールドの防御方向の閾値です。
        /// </summary>
        private const float k_ShieldFaceDotThreshold = -0.2f;

        protected override bool RequiresLivingEnemyTarget => false;

        /// <summary>
        /// 設置時の威力、減衰、演出と減速設定を固定します。
        /// </summary>
        protected override void OnInitialized()
        {
            if (TrapData is not ExplosiveTrapperPranamData data || data.Explosion == null)
            {
                Debug.LogError("LandmineにはExplosiveTrapperPranamDataと爆発設定が必要です。", this);
                return;
            }

            Context.Attack = data.AttackPower;
            Context.Explosion = data.Explosion.CreateSnapshot();
            Context.ConfigureHitSlowdown(data);
        }

        /// <summary>
        /// 設置地点に独立した爆発VFXを生成し、罠の破棄後も煙を残します。
        /// </summary>
        protected override void OnTrapActivated(UnitBehavior target)
        {
            if (Context.Explosion != null && Context.Explosion.Enabled)
            {
                ShooterExplosionVfx.Play(Context.GeneratePosition, Context.Explosion);
            }
        }

        /// <summary>
        /// 起動した敵の位置によらず設置地点から爆風を発生させ、全対象を距離で減衰させます。
        /// </summary>
        protected override void ApplyTrapEffect(UnitBehavior target)
        {
            if (Context.Explosion == null || !Context.Explosion.Enabled || UnitManager.Instance == null)
            {
                return;
            }

            var resolver = new ShooterExplosionResolver(
                Context, ApplyExplosionHit, IsOwnerOrAlly, k_ShieldFaceDotThreshold);
            resolver.Explode(Context.GeneratePosition, Vector3.up, null, null);
        }

        /// <summary>
        /// 設置時の所属チームで所有者と味方を除外し、所属不明の対象にも効果を与えません。
        /// </summary>
        private bool IsOwnerOrAlly(int playerId)
        {
            UnitManager units = UnitManager.Instance;
            return playerId == OwnerPlayerId || units == null ||
                !units.TryGetUnit(playerId, out UnitBehavior unit) ||
                unit.GetTeamId() < 0 || unit.GetTeamId() == OwnerTeamId;
        }

        /// <summary>
        /// 各敵に爆風を一度だけ適用し、シールドを通過してHPが減った場合だけ減速させます。
        /// </summary>
        private void ApplyExplosionHit(int playerId, float damage)
        {
            UnitManager units = UnitManager.Instance;
            if (units == null || IsOwnerOrAlly(playerId) || !units.TryGetUnit(playerId, out UnitBehavior unit) ||
                unit.GetUnitStatus() == null || !unit.GetUnitStatus().IsAlive)
            {
                return;
            }

            float previousHealth = unit.GetUnitStatus().CurrentHealth;
            unit.ApplyCombatDamage(OwnerPlayerId, damage,
                IsMain ? CombatFeedbackSide.Main : CombatFeedbackSide.Sub, SourcePranamNumber);
            if (unit.GetUnitStatus().CurrentHealth < previousHealth)
            {
                unit.ApplyHitSlowdown(Context.HitSlowdownRatio, Context.HitSlowdownDurationSeconds);
            }
        }
    }
}
