using System;

using PranaTactics.Controls;
using PranaTactics.Settings;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 戦闘中の入力に基づいて、
    /// MainおよびSubのPranamスロット選択を要求します。
    /// </summary>
    /// <remarks>
    /// Mainスロットはマウスホイールで切り替え、
    /// Subスロットは数字キーによるスロット指定で切り替えます。
    ///
    /// このクラスが保持するスロット番号は、
    /// 入力による予測値ではなく、
    /// 実際にPranamの切り替えが成功した後の
    /// 確定済み状態です。
    /// </remarks>
    public sealed class BattlePranamSelector :
        IPranamSelector
    {
        /// <summary>
        /// 選択可能なPranamスロット数です。
        /// </summary>
        private const int k_SlotCount = 4;

        /// <summary>
        /// スロットの最小インデックスです。
        /// </summary>
        private const int k_MinimumSlotIndex = 0;

        /// <summary>
        /// スロットの最大インデックスです。
        /// </summary>
        private const int k_MaximumSlotIndex =
            k_SlotCount - 1;

        /// <summary>
        /// スロットが選択されていないことを示します。
        /// </summary>
        private const int k_InvalidSlotIndex = -1;

        /// <summary>
        /// スロットが選択されていないことを示す入力値です。
        /// </summary>
        private const int k_NoSelectedSlotNumber = -1;

        /// <summary>
        /// プレイヤーの入力状態です。
        /// </summary>
        private readonly PlayerInputContext _inputContext;

        private readonly Func<ControlsSettings> _getControls;

        private readonly ScrollSelectionInput _scrollInput = new ScrollSelectionInput();

        /// <summary>
        /// 実際に選択されているMainスロットのインデックスです。
        /// </summary>
        private int _mainSlotIndex =
            k_InvalidSlotIndex;

        /// <summary>
        /// 実際に選択されているSubスロットのインデックスです。
        /// </summary>
        private int _subSlotIndex =
            k_InvalidSlotIndex;

        public event Action<int> MainChanged;

        public event Action<int> SubChanged;

        [Obsolete(
            "Use MainChanged instead.")]
        public event Action<int> OnMainChanged
        {
            add
            {
                MainChanged += value;
            }

            remove
            {
                MainChanged -= value;
            }
        }

        [Obsolete(
            "Use SubChanged instead.")]
        public event Action<int> OnSubChanged
        {
            add
            {
                SubChanged += value;
            }

            remove
            {
                SubChanged -= value;
            }
        }

        public int MainSlotIndex
        {
            get
            {
                return _mainSlotIndex;
            }
        }

        public int SubSlotIndex
        {
            get
            {
                return _subSlotIndex;
            }
        }

        public BattlePranamSelector(
    PlayerInputContext inputContext, Func<ControlsSettings> getControls)
        {
            _getControls = getControls ?? throw new ArgumentNullException(nameof(getControls));
            _inputContext =
                inputContext ??
                throw new ArgumentNullException(
                    nameof(inputContext));
        }

        /// <summary>
        /// 現在の入力状態を確認し、
        /// MainおよびSubスロットの選択要求を更新します。
        /// </summary>
        public void Tick()
        {
            UpdateMainSelection();
            UpdateSubSelection();
        }

        /// <summary>
        /// 実際に選択されているMainスロットを同期します。
        /// </summary>
        /// <param name="slotIndex">
        /// 現在選択されているMainスロットです。
        /// </param>
        /// <remarks>
        /// このメソッドでは変更イベントを発生させません。
        /// 実際にPranamの切り替えが成功した後の
        /// 状態同期専用です。
        /// </remarks>
        public void SetCurrentMainSlot(
            int slotIndex)
        {
            if (!IsValidSlotIndex(
                    slotIndex))
            {
                return;
            }

            _mainSlotIndex =
                slotIndex;
        }

        /// <summary>
        /// 実際に選択されているSubスロットを同期します。
        /// </summary>
        /// <param name="slotIndex">
        /// 現在選択されているSubスロットです。
        /// </param>
        /// <remarks>
        /// このメソッドでは変更イベントを発生させません。
        /// 実際にPranamの切り替えが成功した後の
        /// 状態同期専用です。
        /// </remarks>
        public void SetCurrentSubSlot(
            int slotIndex)
        {
            if (!IsValidSlotIndex(
                    slotIndex))
            {
                return;
            }

            _subSlotIndex =
                slotIndex;
        }

        /// <summary>
        /// マウスホイール入力に基づいて
        /// Mainスロットの変更を要求します。
        /// </summary>
        private void UpdateMainSelection()
        {
            int selectionOffset =
                GetMainSelectionOffset();

            if (selectionOffset == 0)
            {
                return;
            }

            if (!IsValidSlotIndex(
                    _mainSlotIndex))
            {
                return;
            }

            int nextSlotIndex =
                ClampSlotIndex(
                    _mainSlotIndex +
                    selectionOffset);

            if (nextSlotIndex ==
                _mainSlotIndex)
            {
                return;
            }

            MainChanged?.Invoke(
                nextSlotIndex);
        }

        /// <summary>
        /// 数字キー入力に基づいて
        /// Subスロットの変更を要求します。
        /// </summary>
        private void UpdateSubSelection()
        {
            int selectedSlotNumber =
                _inputContext.SelectedSlotNumber;

            if (selectedSlotNumber ==
                k_NoSelectedSlotNumber)
            {
                return;
            }

            if (selectedSlotNumber < 1 ||
                selectedSlotNumber > k_SlotCount)
            {
                return;
            }

            int nextSlotIndex =
                selectedSlotNumber - 1;

            if (_subSlotIndex ==
                nextSlotIndex)
            {
                return;
            }

            SubChanged?.Invoke(
                nextSlotIndex);
        }

        /// <summary>
        /// Mainスロット選択に使用する増減値を取得します。
        /// </summary>
        /// <returns>
        /// 前のスロットを選択する場合は負数、
        /// 次のスロットを選択する場合は正数、
        /// 変更しない場合は0です。
        /// </returns>
        private int GetMainSelectionOffset()
        {
            ControlsSettings controls = _getControls();

            return _scrollInput.ReadOffset(_inputContext.CurrentState.MouseScrollDelta,
                controls.ScrollSensitivity, controls.IsScrollInverted, k_MaximumSlotIndex);
        }

        /// <summary>
        /// スロットインデックスが有効範囲内か判定します。
        /// </summary>
        /// <param name="slotIndex">
        /// 判定するスロットインデックスです。
        /// </param>
        /// <returns>
        /// 0から3の範囲内の場合は<c>true</c>です。
        /// </returns>
        private static bool IsValidSlotIndex(
            int slotIndex)
        {
            return slotIndex >=
                    k_MinimumSlotIndex &&
                slotIndex <=
                    k_MaximumSlotIndex;
        }

        /// <summary>
        /// スロットインデックスを有効範囲へ補正します。
        /// </summary>
        private static int ClampSlotIndex(
            int slotIndex)
        {
            if (slotIndex <
                k_MinimumSlotIndex)
            {
                return k_MinimumSlotIndex;
            }

            if (slotIndex >
                k_MaximumSlotIndex)
            {
                return k_MaximumSlotIndex;
            }

            return slotIndex;
        }
    }
}
