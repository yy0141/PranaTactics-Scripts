using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Unit;

namespace PranaTactics.Pranam
{
    /// <summary>
    /// ユニットが装備するメイン・サブのプラーナムを管理し、
    /// 使用入力、長押し対応および対象ハイライトを制御します。
    /// </summary>
    /// <remarks>
    /// 同じ対象がメインとサブの両方からハイライトされる場合があるため、
    /// 対象ごとの参照数を保持し、全てのハイライト要求が解除された時点で
    /// 実際のハイライト表示を無効化します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PranamManager : MonoBehaviour
    {
        /// <summary>
        /// 所有ユニットのMicro Phase動作です。
        /// </summary>
        private MicroUnitBehavior _microUnitBehavior;

        /// <summary>
        /// メイン側に装備されているプラーナム管理コンポーネントです。
        /// </summary>
        [SerializeField]
        private PranamBehavior _mainPranamBehavior;

        /// <summary>
        /// サブ側に装備されているプラーナム管理コンポーネントです。
        /// </summary>
        [SerializeField]
        private PranamBehavior _subPranamBehavior;

        /// <summary>
        /// ユニットごとのハイライト要求数を保持します。
        /// </summary>
        private readonly Dictionary<UnitBehavior, int>
            _highlightCounts = new();

        public PranamBehavior MainPranamBehavior
        {
            get
            {
                return _mainPranamBehavior;
            }
        }

        public PranamBehavior SubPranamBehavior
        {
            get
            {
                return _subPranamBehavior;
            }
        }

        public bool CanSwitchMainPranam
        {
            get
            {
                return CanSwitchPranam(
                    true);
            }
        }

        public bool CanSwitchSubPranam
        {
            get
            {
                return CanSwitchPranam(
                    false);
            }
        }

        /// <summary>
        /// 指定された側のプラーナムを
        /// 切り替え可能か判定します。
        /// </summary>
        /// <param name="isMain">
        /// Main側を判定する場合は<c>true</c>、
        /// Sub側を判定する場合は<c>false</c>です。
        /// </param>
        /// <returns>
        /// 指定された側の武器を切り替え可能な場合は
        /// <c>true</c>です。
        /// </returns>
        public bool CanSwitchPranam(
            bool isMain)
        {
            PranamBehavior pranamBehavior =
                isMain
                    ? _mainPranamBehavior
                    : _subPranamBehavior;

            if (pranamBehavior == null)
            {
                return false;
            }

            if (!pranamBehavior.CanSwitchPranam)
            {
                return false;
            }

            if (_microUnitBehavior != null &&
                _microUnitBehavior.IsSniperAimLocked)
            {
                bool isLockedSide =
                    _microUnitBehavior.IsSniperAimMain ==
                    isMain;

                if (isLockedSide)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 所有するMicroUnitを取得します。
        /// </summary>
        private void Awake()
        {
            _microUnitBehavior =
                GetComponentInParent<MicroUnitBehavior>(
                    true);
        }

        /// <summary>
        /// メインとサブのプラーナムを初期化します。
        /// </summary>
        public void Initialize()
        {
            InitializePranam(
                _mainPranamBehavior,
                true);

            InitializePranam(
                _subPranamBehavior,
                false);
        }

        public bool CanUseMainPranamHold
        {
            get
            {
                return CanUseHold(
                    _mainPranamBehavior);
            }
        }

        public bool CanUseSubPranamHold
        {
            get
            {
                return CanUseHold(
                    _subPranamBehavior);
            }
        }

        /// <summary>
        /// 指定された側のプラーナムを
        /// 指定スロットへ切り替えます。
        /// </summary>
        public bool TrySelectPranam(
            bool isMain,
            int slotNumber)
        {
            if (!CanSwitchPranam(
                    isMain))
            {
                return false;
            }

            PranamBehavior pranamBehavior =
                isMain
                    ? _mainPranamBehavior
                    : _subPranamBehavior;

            return pranamBehavior != null &&
                pranamBehavior.TrySelectPranam(
                    slotNumber);
        }

        /// <summary>
        /// メインプラーナムの使用処理を実行します。
        /// </summary>
        public void UseMainPranam()
        {
            UsePranam(
                _mainPranamBehavior);
        }

        /// <summary>
        /// サブプラーナムの使用処理を実行します。
        /// </summary>
        public void UseSubPranam()
        {
            UsePranam(
                _subPranamBehavior);
        }

        /// <summary>
        /// メインプラーナムの使用終了処理を実行します。
        /// </summary>
        public void ReleaseMainPranam()
        {
            ReleasePranam(
                _mainPranamBehavior);
        }

        /// <summary>
        /// サブプラーナムの使用終了処理を実行します。
        /// </summary>
        public void ReleaseSubPranam()
        {
            ReleasePranam(
                _subPranamBehavior);
        }

        /// <summary>
        /// 指定されたユニットへのハイライト要求を追加します。
        /// </summary>
        /// <param name="target">
        /// ハイライトする対象ユニットです。
        /// </param>
        /// <remarks>
        /// 同じユニットへ複数のプラーナムから要求された場合は
        /// 要求数のみを加算し、表示切り替えは最初の1回だけ行います。
        /// </remarks>
        public void AddHighlight(
            UnitBehavior target)
        {
            if (target == null)
            {
                return;
            }

            if (_highlightCounts.TryGetValue(
                    target,
                    out int currentCount))
            {
                _highlightCounts[target] =
                    currentCount + 1;

                return;
            }

            _highlightCounts.Add(
                target,
                1);

            target.SwitchHighLight(
                true);
        }

        /// <summary>
        /// 指定されたユニットへのハイライト要求を解除します。
        /// </summary>
        /// <param name="target">
        /// ハイライト要求を解除する対象ユニットです。
        /// </param>
        /// <remarks>
        /// 対象への要求数が0になった場合に限り、
        /// 実際のハイライト表示を無効化します。
        /// </remarks>
        public void RemoveHighlight(
            UnitBehavior target)
        {
            if (target == null)
            {
                return;
            }

            if (!_highlightCounts.TryGetValue(
                    target,
                    out int currentCount))
            {
                return;
            }

            int remainingCount =
                currentCount - 1;

            if (remainingCount > 0)
            {
                _highlightCounts[target] =
                    remainingCount;

                return;
            }

            _highlightCounts.Remove(
                target);

            target.SwitchHighLight(
                false);
        }

        /// <summary>
        /// 管理している全てのハイライト要求を解除します。
        /// </summary>
        public void ClearHighlights()
        {
            foreach (
                KeyValuePair<UnitBehavior, int> entry in
                _highlightCounts)
            {
                UnitBehavior target =
                    entry.Key;

                if (target == null)
                {
                    continue;
                }

                target.SwitchHighLight(
                    false);
            }

            _highlightCounts.Clear();
        }

        /// <summary>
        /// コンポーネントが無効化されたとき、
        /// 残っているハイライト表示を解除します。
        /// </summary>
        private void OnDisable()
        {
            ClearHighlights();
        }

        /// <summary>
        /// 指定されたプラーナム管理コンポーネントを初期化します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 初期化するプラーナム管理コンポーネントです。
        /// </param>
        /// <param name="isMain">
        /// メイン側として初期化する場合は<c>true</c>です。
        /// </param>
        private static void InitializePranam(
            PranamBehavior pranamBehavior,
            bool isMain)
        {
            if (pranamBehavior == null)
            {
                return;
            }

            pranamBehavior.Initialize(
                isMain);
        }

        /// <summary>
        /// 指定されたプラーナムが長押し入力に
        /// 対応しているか判定します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 判定するプラーナム管理コンポーネントです。
        /// </param>
        /// <returns>
        /// 長押しに対応している場合は<c>true</c>です。
        /// </returns>
        private static bool CanUseHold(
            PranamBehavior pranamBehavior)
        {
            return pranamBehavior != null &&
                pranamBehavior.CanUseHold;
        }

        /// <summary>
        /// 指定されたプラーナムの使用処理を実行します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 使用するプラーナム管理コンポーネントです。
        /// </param>
        private static void UsePranam(
            PranamBehavior pranamBehavior)
        {
            if (pranamBehavior == null)
            {
                return;
            }

            pranamBehavior.UsePranam();
        }

        /// <summary>
        /// 指定されたプラーナムの使用終了処理を実行します。
        /// </summary>
        /// <param name="pranamBehavior">
        /// 使用を終了するプラーナム管理コンポーネントです。
        /// </param>
        private static void ReleasePranam(
            PranamBehavior pranamBehavior)
        {
            if (pranamBehavior == null)
            {
                return;
            }

            pranamBehavior.ReleasePranam();
        }
    }
}
