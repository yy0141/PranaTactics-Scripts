using System;

namespace PranaTactics.UI.Gameplay
{
    public interface IPranamSelector
    {
        event Action<int> MainChanged;

        event Action<int> SubChanged;

        /// <summary>
        /// 現在の入力や状態を確認し、
        /// Pranamスロットの選択要求を更新します。
        /// </summary>
        void Tick();

        /// <summary>
        /// 実際に選択されているMainスロットを同期します。
        /// </summary>
        /// <param name="slotIndex">
        /// 現在選択されているMainスロットのインデックスです。
        /// </param>
        void SetCurrentMainSlot(
            int slotIndex);

        /// <summary>
        /// 実際に選択されているSubスロットを同期します。
        /// </summary>
        /// <param name="slotIndex">
        /// 現在選択されているSubスロットのインデックスです。
        /// </param>
        void SetCurrentSubSlot(
            int slotIndex);
    }
}
