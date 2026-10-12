using System.Collections.Generic;

using PranaTactics.Pranam;

namespace PranaTactics.UI.Screens.PranamProfile
{
    /// <summary>
    /// 兵装のロール分類、表示順および絞り込みを定義します。
    /// </summary>
    public static class PranamProfileCatalog
    {
        /// <summary>
        /// データの型を兵装一覧のロールへ変換します。
        /// </summary>
        public static PranamRole GetRole(PranamData weapon)
        {
            return weapon switch
            {
                AttackerPranamData => PranamRole.Attacker,
                ShooterPranamData => PranamRole.Shooter,
                GunnerPranamData => PranamRole.Gunner,
                SniperPranamData => PranamRole.Sniper,
                DefencePranamData => PranamRole.Defense,
                OptionPranamData => PranamRole.Option,
                _ => PranamRole.Other
            };
        }

        /// <summary>
        /// 指定ロール順に整列し、同じロール内では登録順を維持します。
        /// </summary>
        public static List<PranamData> Order(IReadOnlyList<PranamData> weapons)
        {
            var ordered = new List<PranamData>();

            for (PranamRole role = PranamRole.Attacker; role <= PranamRole.Other; role++)
            {
                foreach (PranamData weapon in weapons)
                {
                    if (weapon != null && GetRole(weapon) == role && !ordered.Contains(weapon))
                    {
                        ordered.Add(weapon);
                    }
                }
            }

            return ordered;
        }

        /// <summary>
        /// 整列済みの一覧から、指定されたロールの兵装だけを取得します。
        /// </summary>
        public static List<PranamData> Filter(IReadOnlyList<PranamData> weapons, PranamRole role)
        {
            var filtered = new List<PranamData>();

            foreach (PranamData weapon in weapons)
            {
                if (weapon != null && (role == PranamRole.All || GetRole(weapon) == role))
                {
                    filtered.Add(weapon);
                }
            }

            return filtered;
        }
    }
}
