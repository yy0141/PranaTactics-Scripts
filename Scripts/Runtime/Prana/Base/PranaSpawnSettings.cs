using System;
using UnityEngine;

using PranaTactics.Pranam;

namespace PranaTactics.Prana
{
    /// <summary>
    /// Pranaが参照する固定設定から、通信する発射時の性能を確定します。
    /// </summary>
    internal static class PranaSpawnSettings
    {
        /// <summary>
        /// 生成情報を複製し、兵装種別の性能と分割数を適用します。
        /// </summary>
        /// <param name="data">
        /// Prefabに登録された固定設定です。共有アセット自体は変更しません。
        /// </param>
        /// <param name="generation">
        /// 所有者、対象、位置など発射ごとに決まる情報です。
        /// </param>
        /// <param name="attackDivisionCount">
        /// 射出弾の威力を分配する弾数です。
        /// </param>
        public static PranamContext CreateSnapshot(
            PranamData data,
            PranamContext generation,
            int attackDivisionCount)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (generation == null)
            {
                throw new ArgumentNullException(nameof(generation));
            }

            if (attackDivisionCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attackDivisionCount));
            }

            PranamContext snapshot = generation.CreateSnapshot();
            snapshot.SoundProfileId = data.SoundProfile != null
                ? data.SoundProfile.SoundId
                : string.Empty;

            switch (data)
            {
                case AttackerPranamData attacker:
                    snapshot.Attack = attacker.AttackPower;
                    snapshot.MeleeRange = attacker.Diameter;
                    break;

                case GunnerPranamData gunner:
                    snapshot.Attack = Mathf.Max(0.0f, gunner.AttackPower);
                    snapshot.Speed = Mathf.Max(0.0f, gunner.BulletSpeed);
                    snapshot.SpreadAngle = Mathf.Max(0.0f, gunner.SpreadAngle);
                    break;

                case ShooterPranamData shooter:
                    snapshot.Attack = Mathf.Max(0.0f, shooter.AttackPower / attackDivisionCount);
                    snapshot.Speed = Mathf.Max(0.0f, shooter.BulletSpeed);
                    snapshot.SpreadAngle = Mathf.Max(0.0f, shooter.SpreadAngle);
                    snapshot.Explosion = shooter.Explosion != null && shooter.Explosion.Enabled
                        ? shooter.Explosion.CreateSnapshot()
                        : null;
                    break;

                case SniperPranamData sniper:
                    snapshot.Attack = Mathf.Max(0.0f, sniper.AttackPower);
                    snapshot.Speed = Mathf.Max(0.0f, sniper.BulletSpeed);
                    break;

                case DefencePranamData shield:
                    snapshot.Defense = Mathf.Max(0.0f, shield.DefensePower);
                    break;

                default:
                    throw new ArgumentException("生成性能に対応していない兵装設定です。", nameof(data));
            }

            if (data is IHitSlowdownPranamData hitSlowdown)
            {
                snapshot.ConfigureHitSlowdown(hitSlowdown);
            }

            return snapshot;
        }
    }
}
