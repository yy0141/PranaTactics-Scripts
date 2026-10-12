using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Screens;
using PranaTactics.Unit;

namespace PranaTactics.UI.Gameplay
{
    /// <summary>
    /// 戦闘画面に表示するMainおよびSubの
    /// PranamスロットUIを管理します。
    /// </summary>
    /// <remarks>
    /// <see cref="IPranamSelector"/>から選択変更通知を受け取り、
    /// 対応するスロットUIの選択状態を切り替えます。
    ///
    /// また、各Pranamのクールダウンゲージ更新を受け付けます。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PranamUIManager : MonoBehaviour
    {
        /// <summary>
        /// MainのPranamスロットUI一覧です。
        /// </summary>
        [Header("Pranam Slots")]
        [SerializeField]
        private List<PranamSlotUI> _mainPranamSlotUIs = new();

        /// <summary>
        /// SubのPranamスロットUI一覧です。
        /// </summary>
        [SerializeField]
        private List<PranamSlotUI> _subPranamSlotUIs = new();

        /// <summary>
        /// Pranamの選択入力を処理するセレクターです。
        /// </summary>
        private IPranamSelector _selector;

        /// <summary>
        /// 現在選択されているMainスロットのインデックスです。
        /// </summary>
        private int _currentMainSlotIndex = -1;

        /// <summary>
        /// 現在選択されているSubスロットのインデックスです。
        /// </summary>
        private int _currentSubSlotIndex = -1;

        public static PranamUIManager Instance
        {
            get;
            private set;
        }

        public int CurrentMainSlotIndex
        {
            get
            {
                return _currentMainSlotIndex;
            }
        }

        public int CurrentSubSlotIndex
        {
            get
            {
                return _currentSubSlotIndex;
            }
        }

        public bool IsInitialized
        {
            get
            {
                return _selector != null;
            }
        }

        /// <summary>
        /// シングルトンインスタンスを設定します。
        /// </summary>
        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Debug.LogWarning(
                    $"{nameof(PranamUIManager)}が複数存在するため、" +
                    $"{gameObject.name}を破棄します。",
                    this);

                Destroy(
                    gameObject);

                return;
            }

            Instance =
                this;
        }

        /// <summary>
        /// セレクターの入力状態を更新します。
        /// </summary>
        private void Update()
        {
            if (_selector == null ||
                !CanUpdateSelector())
            {
                return;
            }

            _selector.Tick();
        }

        /// <summary>
        /// セレクターのイベント購読を解除し、
        /// シングルトン参照を解放します。
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeSelector();

            if (Instance == this)
            {
                Instance =
                    null;
            }
        }

        /// <summary>
        /// Pranamスロットの選択に使用するセレクターを設定します。
        /// </summary>
        /// <param name="selector">
        /// 使用するPranamセレクターです。
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="selector"/>が<c>null</c>の場合に発生します。
        /// </exception>
        public void Initialize(
            IPranamSelector selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(
                    nameof(selector));
            }

            if (ReferenceEquals(
                    _selector,
                    selector))
            {
                SynchronizeSelectorSelection();

                return;
            }

            UnsubscribeSelector();

            _selector =
                selector;

            _selector.MainChanged +=
                HandleMainSelectionChanged;

            _selector.SubChanged +=
                HandleSubSelectionChanged;

            SynchronizeSelectorSelection();
        }

        /// <summary>
        /// 指定されたMainのPranamスロットUIを初期化します。
        /// </summary>
        /// <param name="pranamRange">
        /// 選択時に表示するPranamの射程オブジェクトです。
        /// </param>
        /// <param name="icon">
        /// スロットに表示するアイコンです。
        /// </param>
        /// <param name="displayName">
        /// スロットに表示するPranam名です。
        /// </param>
        /// <param name="index">
        /// 初期化するMainスロットのインデックスです。
        /// </param>
        /// <param name="actionPointCost">
        /// 兵装を1回使用する際の消費行動力です。
        /// </param>
        /// <param name="isDefault">
        /// 初期選択スロットにする場合は<c>true</c>です。
        /// </param>
        public void InitializeMainPranamSlotUI(
            GameObject pranamRange,
            Sprite icon,
            string displayName,
            int actionPointCost,
            int index,
            bool isDefault)
        {
            if (!TryGetMainSlot(
                    index,
                    out PranamSlotUI slotUI))
            {
                return;
            }

            slotUI.Initialize(
                pranamRange,
                icon,
                displayName,
                actionPointCost,
                false);

            bool shouldSelect =
                isDefault ||
                _currentMainSlotIndex < 0 ||
                _currentMainSlotIndex == index;

            if (shouldSelect)
            {
                SelectMainSlot(
                    index,
                    true);
            }
        }

        /// <summary>
        /// 指定されたSubのPranamスロットUIを初期化します。
        /// </summary>
        /// <param name="pranamRange">
        /// 選択時に表示するPranamの射程オブジェクトです。
        /// </param>
        /// <param name="icon">
        /// スロットに表示するアイコンです。
        /// </param>
        /// <param name="displayName">
        /// スロットに表示するPranam名です。
        /// </param>
        /// <param name="index">
        /// 初期化するSubスロットのインデックスです。
        /// </param>
        /// <param name="actionPointCost">
        /// 兵装を1回使用する際の消費行動力です。
        /// </param>
        /// <param name="isDefault">
        /// 初期選択スロットにする場合は<c>true</c>です。
        /// </param>
        public void InitializeSubPranamSlotUI(
            GameObject pranamRange,
            Sprite icon,
            string displayName,
            int actionPointCost,
            int index,
            bool isDefault)
        {
            if (!TryGetSubSlot(
                    index,
                    out PranamSlotUI slotUI))
            {
                return;
            }

            slotUI.Initialize(
                pranamRange,
                icon,
                displayName,
                actionPointCost,
                false);

            bool shouldSelect =
                isDefault ||
                _currentSubSlotIndex < 0 ||
                _currentSubSlotIndex == index;

            if (shouldSelect)
            {
                SelectSubSlot(
                    index,
                    true);
            }
        }

        /// <summary>
        /// 実際に選択されたPranamに合わせて、
        /// スロットUIの選択状態を更新します。
        /// </summary>
        /// <param name="isMain">
        /// Main側の場合は<c>true</c>、
        /// Sub側の場合は<c>false</c>です。
        /// </param>
        /// <param name="index">
        /// 選択されたスロットのインデックスです。
        /// </param>
        /// <remarks>
        /// 武器切替の可否はPranam側で判定し、
        /// 実際の切替が成功した後にこのメソッドを呼び出します。
        /// これにより、硬直中にUIだけが先に切り替わることを防ぎます。
        /// </remarks>
        public void SetSelectedPranam(
            bool isMain,
            int index)
        {
            if (isMain)
            {
                SelectMainSlot(
                    index);

                return;
            }

            SelectSubSlot(
                index);
        }

        /// <summary>
        /// 指定されたPranamスロットの
        /// クールダウンゲージを開始します。
        /// </summary>
        /// <param name="isMain">
        /// Mainスロットの場合は<c>true</c>、
        /// Subスロットの場合は<c>false</c>です。
        /// </param>
        /// <param name="index">
        /// 対象となるスロットのインデックスです。
        /// </param>
        /// <param name="duration">
        /// クールダウン完了までの秒数です。
        /// </param>
        public void SetCooldownGauge(
            bool isMain,
            int index,
            float duration)
        {
            PranamSlotUI slotUI;

            bool hasSlot =
                isMain
                    ? TryGetMainSlot(
                        index,
                        out slotUI)
                    : TryGetSubSlot(
                        index,
                        out slotUI);

            if (!hasSlot)
            {
                return;
            }

            slotUI.CountGauge(
                duration);
        }

        /// <summary>
        /// 指定されたPranamスロットの
        /// クールダウンゲージを開始します。
        /// </summary>
        /// <param name="isMain">
        /// Mainスロットの場合は<c>true</c>です。
        /// </param>
        /// <param name="number">
        /// 対象スロットのインデックスです。
        /// </param>
        /// <param name="time">
        /// クールダウン完了までの秒数です。
        /// </param>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="SetCooldownGauge"/>を使用してください。
        /// </remarks>
        [Obsolete(
            "Use SetCooldownGauge instead.")]
        public void SetCountGuage(
            bool isMain,
            int number,
            float time)
        {
            SetCooldownGauge(
                isMain,
                number,
                time);
        }

        /// <summary>
        /// Mainスロットの選択変更通知を処理します。
        /// </summary>
        /// <param name="index">
        /// 選択されたMainスロットのインデックスです。
        /// </param>
        private void HandleMainSelectionChanged(
            int index)
        {
            if (!CanChangeSelection())
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            MicroUnitBehavior microUnitBehavior =
                unitManager?.LocalMicroUnitBehavior;

            if (microUnitBehavior == null)
            {
                return;
            }

            microUnitBehavior.TrySelectPranam(
                true,
                index);
        }

        /// <summary>
        /// Subスロットの選択変更通知を処理します。
        /// </summary>
        /// <param name="index">
        /// 選択されたSubスロットのインデックスです。
        /// </param>
        private void HandleSubSelectionChanged(
            int index)
        {
            if (!CanChangeSelection())
            {
                return;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            MicroUnitBehavior microUnitBehavior =
                unitManager?.LocalMicroUnitBehavior;

            if (microUnitBehavior == null)
            {
                return;
            }

            microUnitBehavior.TrySelectPranam(
                false,
                index);
        }

        /// <summary>
        /// 指定されたMainスロットを選択状態へ変更します。
        /// </summary>
        /// <param name="index">
        /// 選択するMainスロットのインデックスです。
        /// </param>
        /// <param name="force">
        /// 現在と同じスロットでも状態を再適用する場合は
        /// <c>true</c>です。
        /// </param>
        private void SelectMainSlot(
            int index,
            bool force = false)
        {
            if (!TryGetMainSlot(
                    index,
                    out PranamSlotUI nextSlotUI))
            {
                return;
            }

            if (_currentMainSlotIndex == index)
            {
                if (force)
                {
                    nextSlotUI.Enter();
                }

                _selector?.SetCurrentMainSlot(
                    index);

                return;
            }

            if (TryGetMainSlot(
                    _currentMainSlotIndex,
                    out PranamSlotUI currentSlotUI,
                    false))
            {
                currentSlotUI.Exit();
            }

            _currentMainSlotIndex =
                index;

            nextSlotUI.Enter();

            _selector?.SetCurrentMainSlot(
                _currentMainSlotIndex);
        }

        /// <summary>
        /// 指定されたSubスロットを選択状態へ変更します。
        /// </summary>
        /// <param name="index">
        /// 選択するSubスロットのインデックスです。
        /// </param>
        /// <param name="force">
        /// 現在と同じスロットでも状態を再適用する場合は
        /// <c>true</c>です。
        /// </param>
        private void SelectSubSlot(
            int index,
            bool force = false)
        {
            if (!TryGetSubSlot(
                    index,
                    out PranamSlotUI nextSlotUI))
            {
                return;
            }

            if (_currentSubSlotIndex == index)
            {
                if (force)
                {
                    nextSlotUI.Enter();
                }

                _selector?.SetCurrentSubSlot(
                    index);

                return;
            }

            if (TryGetSubSlot(
                    _currentSubSlotIndex,
                    out PranamSlotUI currentSlotUI,
                    false))
            {
                currentSlotUI.Exit();
            }

            _currentSubSlotIndex =
                index;

            nextSlotUI.Enter();

            _selector?.SetCurrentSubSlot(
                _currentSubSlotIndex);
        }

        /// <summary>
        /// Mainスロット一覧から指定されたスロットを取得します。
        /// </summary>
        private bool TryGetMainSlot(
            int index,
            out PranamSlotUI slotUI,
            bool logWarning = true)
        {
            return TryGetSlot(
                _mainPranamSlotUIs,
                index,
                "Main",
                out slotUI,
                logWarning);
        }

        /// <summary>
        /// Subスロット一覧から指定されたスロットを取得します。
        /// </summary>
        private bool TryGetSubSlot(
            int index,
            out PranamSlotUI slotUI,
            bool logWarning = true)
        {
            return TryGetSlot(
                _subPranamSlotUIs,
                index,
                "Sub",
                out slotUI,
                logWarning);
        }

        /// <summary>
        /// 指定された一覧からPranamスロットUIを取得します。
        /// </summary>
        /// <param name="slotUIs">
        /// 検索対象のスロットUI一覧です。
        /// </param>
        /// <param name="index">
        /// 取得するスロットのインデックスです。
        /// </param>
        /// <param name="slotType">
        /// ログへ表示するスロット種別です。
        /// </param>
        /// <param name="slotUI">
        /// 取得されたPranamスロットUIです。
        /// </param>
        /// <param name="logWarning">
        /// 取得できない場合に警告を表示するかどうかです。
        /// </param>
        /// <returns>
        /// 有効なスロットUIを取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetSlot(
            List<PranamSlotUI> slotUIs,
            int index,
            string slotType,
            out PranamSlotUI slotUI,
            bool logWarning)
        {
            slotUI =
                null;

            if (slotUIs == null ||
                index < 0 ||
                index >= slotUIs.Count)
            {
                if (logWarning)
                {
                    int count =
                        slotUIs?.Count ??
                        0;

                    Debug.LogWarning(
                        $"{nameof(PranamUIManager)}: " +
                        $"{slotType}スロットのインデックスが範囲外です。" +
                        $" Index={index}, Count={count}",
                        this);
                }

                return false;
            }

            slotUI =
                slotUIs[index];

            if (slotUI != null)
            {
                return true;
            }

            if (logWarning)
            {
                Debug.LogWarning(
                    $"{nameof(PranamUIManager)}: " +
                    $"{slotType}スロットUIが設定されていません。" +
                    $" Index={index}",
                    this);
            }

            return false;
        }

        /// <summary>
        /// 現在Pranamセレクターを更新できるかどうかを判定します。
        /// </summary>
        /// <returns>
        /// Micro Phase中かつ設定画面が閉じている場合は
        /// <c>true</c>です。
        /// </returns>
        private static bool CanUpdateSelector()
        {
            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager != null &&
                phaseManager.CurrentPhase !=
                MatchPhase.Micro)
            {
                return false;
            }

            SettingsUIManager settingsUIManager =
                SettingsUIManager.Instance;

            return settingsUIManager == null ||
                !settingsUIManager.IsOpenedUI();
        }

        /// <summary>
        /// 現在スロット選択を変更できるかどうかを判定します。
        /// </summary>
        private static bool CanChangeSelection()
        {
            return CanUpdateSelector();
        }

        /// <summary>
        /// 現在確定しているMain/SubのPranam選択状態を
        /// Selectorへ同期します。
        /// </summary>
        /// <remarks>
        /// Pranamスロットの初期化とSelectorの初期化順序が
        /// 前後しても状態が一致するようにするための処理です。
        /// </remarks>
        private void SynchronizeSelectorSelection()
        {
            if (_selector == null)
            {
                return;
            }

            if (_currentMainSlotIndex >= 0)
            {
                _selector.SetCurrentMainSlot(
                    _currentMainSlotIndex);
            }

            if (_currentSubSlotIndex >= 0)
            {
                _selector.SetCurrentSubSlot(
                    _currentSubSlotIndex);
            }
        }

        /// <summary>
        /// 現在のセレクターからイベント購読を解除します。
        /// </summary>
        private void UnsubscribeSelector()
        {
            if (_selector == null)
            {
                return;
            }

            _selector.MainChanged -=
                HandleMainSelectionChanged;

            _selector.SubChanged -=
                HandleSubSelectionChanged;

            _selector =
                null;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたスロット一覧を検証します。
        /// </summary>
        private void OnValidate()
        {
            if (_mainPranamSlotUIs == null)
            {
                _mainPranamSlotUIs =
                    new List<PranamSlotUI>();
            }

            if (_subPranamSlotUIs == null)
            {
                _subPranamSlotUIs =
                    new List<PranamSlotUI>();
            }
        }
#endif
    }
}
