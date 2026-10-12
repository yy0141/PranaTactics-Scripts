using System;
using System.Collections.Generic;

using PranaTactics.Pranam;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// 兵装ごとの性能データを、プロフィール表示用の共通項目へ変換します。
    /// </summary>
    public sealed class PranamProfileMetrics
    {
        private readonly float?[] _values;

        public IReadOnlyList<float?> Values => _values;

        public string Category { get; }

        public string PowerLabel { get; }

        /// <summary>
        /// 射程、角度、威力、行動力、プラーナ、硬直時間の順で値を保持します。
        /// </summary>
        private PranamProfileMetrics(
            PranamData data, string category, float? distance, float? angle,
            float? power, float? recovery, string powerLabel = "攻撃力")
        {
            Category = category;
            PowerLabel = powerLabel;
            _values = new float?[] { distance, angle, power, data.ActionPointCost, data.PranaCost, recovery };
        }

        /// <summary>
        /// 実際の兵装型から性能を読み取り、非対応の項目を未設定として扱います。
        /// </summary>
        public static PranamProfileMetrics Create(PranamData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return data switch
            {
                SniperPranamData sniper => new PranamProfileMetrics(data, "SNIPER", sniper.Diameter, sniper.Angle, sniper.AttackPower, sniper.RecoveryTime),
                ShooterPranamData shooter => new PranamProfileMetrics(data, "SHOOTER", shooter.Diameter, shooter.Angle, shooter.AttackPower, shooter.RecoveryTime),
                GunnerPranamData gunner => new PranamProfileMetrics(data, "GUNNER", gunner.Diameter, gunner.Angle, gunner.AttackPower, gunner.RecoveryTime),
                AttackerPranamData attacker => new PranamProfileMetrics(data, "ATTACKER", attacker.Diameter, attacker.Angle, attacker.AttackPower, attacker.RecoveryTime),
                DefencePranamData defence => new PranamProfileMetrics(data, "DEFENSE", null, null, defence.DefensePower, defence.RecoveryTime, "防御力"),
                AttackOptionPranamData attackOption => new PranamProfileMetrics(data, "OPTION", attackOption.Diameter, attackOption.Angle, attackOption.AttackPower, attackOption.RecoveryTime),
                OptionPranamData option => new PranamProfileMetrics(data, "OPTION", null, null, null, option.RecoveryTime),
                _ => new PranamProfileMetrics(data, "PRANAM", null, null, null, null)
            };
        }
    }
}
