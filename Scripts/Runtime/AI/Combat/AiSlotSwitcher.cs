using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// Main/Subを独立して即時切り替えます。
    /// AIユニットにはUI更新が不要なため、実際の兵装オブジェクトだけを切り替えます。
    /// </summary>
    public static class AiSlotSwitcher
    {
        /// <remarks>
        /// <para>PranamManager経由で切り替え、硬直状態・選択状態・</para>
        /// <para>ターゲット再走査をゲーム本体と同じ経路で更新します。</para>
        /// </remarks>
        public static bool TrySelect(UnitBehavior unit, bool isMain, int slot)
        {
            if (unit == null || unit.GetMicroUnitBehavior() == null)
            {
                return false;
            }

            MicroUnitBehavior micro = unit.GetMicroUnitBehavior();

            if (slot < 0)
            {
                return false;
            }

            return micro.TrySelectPranam(isMain, slot);
        }
    }
}
