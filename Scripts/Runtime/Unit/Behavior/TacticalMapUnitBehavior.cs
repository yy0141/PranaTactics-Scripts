using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

using PranaTactics.Settings;
using PranaTactics.Board;
using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Pranam;
using PranaTactics.UI.Gameplay;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 戦術マップ上のユニット表示、Pranamの射程方向、
    /// ミニマップカメラおよびフルマップ表示を管理します。
    /// </summary>
    /// <remarks>
    /// キャラクターデータに設定されたMainおよびSubの
    /// Pranam一覧から、各スロットへ対応する
    /// <see cref="PranamBase"/>派生コンポーネントを追加します。
    ///
    /// ローカルプレイヤーのユニットでは、
    /// 戦術マップ用カメラのRenderTextureを
    /// ミニマップUIへ設定します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TacticalMapUnitBehavior : UnitBehaviorBase
    {
        /// <summary>
        /// 空のPranamスロットを表す名前です。
        /// </summary>
        private const string k_NonePranamName = "NONE";

        /// <summary>
        /// PranamBase派生クラスの既定名前空間です。
        /// </summary>
        private const string k_PranamNamespace =
            "PranaTactics.Pranam.";

        /// <summary>
        /// 所有者以外のPranam射程へ設定するレイヤー名です。
        /// </summary>
        private const string k_DefaultLayerName = "Default";

        /// <summary>
        /// ミニマップカメラ状態を自己修復する既定間隔です。
        /// </summary>
        private const float k_DefaultMinimapRepairInterval = 0.5f;

        /// <summary>
        /// ミニマップ初期化後に再確認する既定フレーム数です。
        /// </summary>
        private const int k_DefaultMinimapRetryFrameCount = 8;

        /// <summary>
        /// Pranamの扇形初期化を再確認する最大フレーム数です。
        /// </summary>
        private const int k_PranamInitializationRetryFrameCount = 8;

        /// <summary>
        /// Main Pranamのスロットを格納するルートオブジェクトです。
        /// </summary>
        [Header("Pranam Slots")]
        [FormerlySerializedAs("_mainTriggers")]
        [SerializeField]
        private GameObject _mainPranamSlotsRoot;

        /// <summary>
        /// Sub Pranamのスロットを格納するルートオブジェクトです。
        /// </summary>
        [FormerlySerializedAs("_subTriggers")]
        [SerializeField]
        private GameObject _subPranamSlotsRoot;

        /// <summary>
        /// Main側のPranam使用と同期を管理します。
        /// </summary>
        [SerializeField]
        private PranamBehavior _mainPranamBehavior;

        /// <summary>
        /// Sub側のPranam使用と同期を管理します。
        /// </summary>
        [SerializeField]
        private PranamBehavior _subPranamBehavior;

        /// <summary>
        /// ミニマップを表示するRawImageです。
        /// </summary>
        [Header("Minimap")]
        [SerializeField]
        private RawImage _minimapRawImage;

        /// <summary>
        /// ミニマップカメラの状態を確認する間隔です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _minimapRepairInterval =
            k_DefaultMinimapRepairInterval;

        /// <summary>
        /// 初期化直後にミニマップを再確認するフレーム数です。
        /// </summary>
        [Min(1)]
        [SerializeField]
        private int _minimapRetryFrameCount =
            k_DefaultMinimapRetryFrameCount;

        /// <summary>
        /// ミニマップUIの自動検索を使用するかどうかです。
        /// </summary>
        /// <remarks>
        /// 基本的にはInspectorから
        /// <see cref="_minimapRawImage"/>を設定してください。
        /// </remarks>
        [SerializeField]
        private bool _allowAutomaticMinimapUiSearch =
            true;

        /// <summary>
        /// 初期化やミニマップ状態をログへ出力するかどうかです。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        public enum UnitHighlightState
        {
            None,

            Single,

            Multiple
        }

        [Obsolete(
    "Use UnitHighlightState instead.")]
        public enum UnitHighLight
        {
            None,

            Either,

            Both
        }

        /// <summary>
        /// 現在フルマップ表示中かどうかです。
        /// </summary>
        private bool _isMapMode;

        /// <summary>
        /// Pranamの初期化処理を開始済みかどうかです。
        /// </summary>
        private bool _isPranamInitializationStarted;

        /// <summary>
        /// Pranamスロットの初期化が完了しているかどうかです。
        /// </summary>
        private bool _isPranamInitialized;

        /// <summary>
        /// Pranamまたは扇形範囲の初期化に失敗したかどうかです。
        /// </summary>
        private bool _hasPranamInitializationFailed;

        /// <summary>
        /// 戦術マップユニットが移動先を保持しているかどうかです。
        /// </summary>
        private bool _hasMoveTarget;

        /// <summary>
        /// ハイライトを要求している処理数です。
        /// </summary>
        private int _highlightRequestCount;

        /// <summary>
        /// ミニマップ初期化の再試行コルーチンです。
        /// </summary>
        private Coroutine _minimapInitializationCoroutine;

        /// <summary>
        /// 次にミニマップ状態を自己修復できる時刻です。
        /// </summary>
        private float _nextMinimapRepairTime;

        /// <summary>
        /// 次にミニマップUIの自動検索を実行できる時刻です。
        /// </summary>
        private float _nextMinimapUiSearchTime;

        public bool IsMapMode
        {
            get
            {
                return _isMapMode;
            }
        }

        public bool IsPranamInitialized
        {
            get
            {
                return _isPranamInitialized;
            }
        }

        public bool HasPranamInitializationFailed
        {
            get
            {
                return _hasPranamInitializationFailed;
            }
        }

        public UnitHighlightState HighlightState
        {
            get
            {
                if (_highlightRequestCount <= 0)
                {
                    return UnitHighlightState.None;
                }

                if (_highlightRequestCount == 1)
                {
                    return UnitHighlightState.Single;
                }

                return UnitHighlightState.Multiple;
            }
        }

        /// <summary>
        /// Main PranamスロットのルートTransformを取得します。
        /// </summary>
        public Transform GetMainPranamTransform()
        {
            return _mainPranamSlotsRoot != null
                ? _mainPranamSlotsRoot.transform
                : null;
        }

        /// <summary>
        /// Sub PranamスロットのルートTransformを取得します。
        /// </summary>
        public Transform GetSubPranamTransform()
        {
            return _subPranamSlotsRoot != null
                ? _subPranamSlotsRoot.transform
                : null;
        }

        /// <summary>
        /// Main PranamスロットのルートTransformを取得します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="GetMainPranamTransform"/>を
        /// 使用してください。
        /// </remarks>
        [Obsolete(
            "Use GetMainPranamTransform instead.")]
        public Transform GetMainTriggerTransform()
        {
            return GetMainPranamTransform();
        }

        /// <summary>
        /// Sub PranamスロットのルートTransformを取得します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="GetSubPranamTransform"/>を
        /// 使用してください。
        /// </remarks>
        [Obsolete(
            "Use GetSubPranamTransform instead.")]
        public Transform GetSubTriggerTransform()
        {
            return GetSubPranamTransform();
        }

        /// <summary>
        /// ユニットへのハイライト要求を1つ追加します。
        /// </summary>
        public void AddUnitHighlight()
        {
            _highlightRequestCount =
                Mathf.Min(
                    2,
                    _highlightRequestCount + 1);
        }

        /// <summary>
        /// ユニットへのハイライト要求を1つ削除します。
        /// </summary>
        /// <returns>
        /// 削除後もハイライト要求が残っている場合は
        /// <c>true</c>です。
        /// </returns>
        public bool RemoveUnitHighlight()
        {
            _highlightRequestCount =
                Mathf.Max(
                    0,
                    _highlightRequestCount - 1);

            return _highlightRequestCount >
                0;
        }

        /// <summary>
        /// ユニットへのハイライト要求を1つ追加します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// </remarks>
        [Obsolete(
            "Use AddUnitHighlight instead.")]
        public void AddUnitHightLight()
        {
            AddUnitHighlight();
        }

        /// <summary>
        /// ユニットへのハイライト要求を1つ削除します。
        /// </summary>
        /// <returns>
        /// 削除後もハイライト要求が残っている場合は
        /// <c>true</c>です。
        /// </returns>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// </remarks>
        [Obsolete(
            "Use RemoveUnitHighlight instead.")]
        public bool RemoveUnitHightLight()
        {
            return RemoveUnitHighlight();
        }

        /// <summary>
        /// キャラクターに設定されたMainおよびSubの
        /// Pranamスロットを初期化します。
        /// </summary>
        /// <param name="yRotation">
        /// Pranamの初期水平角度です。
        /// </param>
        public void Initialize(
            int yRotation)
        {
            if (_isPranamInitializationStarted ||
                _isPranamInitialized)
            {
                return;
            }

            if (!ValidatePranamInitializationReferences())
            {
                return;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null ||
                !repository.IsLoaded)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{nameof(GameDataRepository)}の読み込みが" +
                    "完了していないため、Pranamを初期化できません。",
                    this);

                return;
            }

            List<List<PranamData>> pranamLists =
                repository.GetPranams(
                    _unitStatus.UnitName);

            if (pranamLists == null ||
                pranamLists.Count < 2)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "Pranamデータの構造が不正です。" +
                    $" Unit={_unitStatus.UnitName}," +
                    $" ListCount={pranamLists?.Count ?? 0}",
                    this);

                return;
            }

            _isPranamInitializationStarted =
                true;

            _isPranamInitialized =
                false;

            _hasPranamInitializationFailed =
                false;

            List<PranamData> mainPranams =
                pranamLists[0];

            List<PranamData> subPranams =
                pranamLists[1];

            InitializePranamSlots(
                _mainPranamSlotsRoot.transform,
                _mainPranamBehavior,
                mainPranams,
                yRotation,
                true);

            InitializePranamSlots(
                _subPranamSlotsRoot.transform,
                _subPranamBehavior,
                subPranams,
                yRotation,
                false);

            _ = CompletePranamInitializationAsync();

            Debug.Log(
                $"[UnitInit] TacticalMap Initialize " +
                $"Frame={Time.frameCount}," +
                $"PlayerId={_unitStatus?.PlayerId}",
                this);
        }

        /// <summary>
        /// 動的に追加したPranamコンポーネントの有効化を待ち、
        /// Micro側のPranam管理を初期化します。
        /// </summary>
        /// <remarks>
        /// Unitの破棄によるキャンセルです。
        /// </remarks>
        private async Awaitable CompletePranamInitializationAsync()
        {
            try
            {
                PranamBase failedPranam =
                    null;

                int stableFrameCount =
                    0;

                for (int frameIndex = 0;
                    frameIndex <
                        k_PranamInitializationRetryFrameCount;
                    frameIndex++)
                {
                    await Awaitable.NextFrameAsync(
                        destroyCancellationToken);

                    if (TryEnsureAllPranamsReady(
                            out failedPranam))
                    {
                        stableFrameCount++;

                        if (stableFrameCount < 2)
                        {
                            continue;
                        }

                        MicroUnitBehavior microUnitBehavior =
                            _unitBehavior?.GetMicroUnitBehavior();

                        if (microUnitBehavior == null)
                        {
                            FailPranamInitialization(
                                $"{nameof(MicroUnitBehavior)}が存在しないため、" +
                                "PranamManagerを初期化できません。",
                                this);

                            return;
                        }

                        microUnitBehavior.InitializePranam();

                        RefreshPranamRangeVisibility();

                        _isPranamInitialized =
                            true;

                        _isPranamInitializationStarted =
                            false;

                        _hasPranamInitializationFailed =
                            false;

                        if (_enableDebugLogging)
                        {
                            Debug.Log(
                                $"{nameof(TacticalMapUnitBehavior)}: " +
                                "Pranamと扇形範囲の初期化が完了しました。" +
                                $" PlayerId={_unitStatus?.PlayerId}," +
                                $" RetryFrame={frameIndex + 1}",
                                this);
                        }

                        return;
                    }

                    stableFrameCount =
                        0;
                }

                string failedPranamName =
                    failedPranam != null
                        ? failedPranam.name
                        : "unknown";

                FailPranamInitialization(
                    "Pranam本体または必要な扇形範囲を初期化できませんでした。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" Pranam={failedPranamName}," +
                    $" RetryFrames={k_PranamInitializationRetryFrameCount}",
                    failedPranam != null
                        ? failedPranam
                        : this);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception exception)
            {
                FailPranamInitialization(
                    "Pranam初期化中に例外が発生しました。",
                    this);

                Debug.LogException(
                    exception,
                    this);
            }
        }

        /// <summary>
        /// 配下のすべてのPranamについて、
        /// 本体と扇形範囲の初期化完了を確認します。
        /// </summary>
        /// <param name="failedPranam">
        /// 初期化が完了していないPranamです。
        /// </param>
        /// <returns>
        /// すべて初期化済みの場合は<c>true</c>です。
        /// </returns>
        private bool TryEnsureAllPranamsReady(
            out PranamBase failedPranam)
        {
            failedPranam =
                null;

            PranamBase[] pranams =
                GetComponentsInChildren<PranamBase>(
                    true);

            foreach (PranamBase pranam in
                    pranams)
            {
                if (pranam == null)
                {
                    continue;
                }

                if (pranam.EnsureRuntimeReady())
                {
                    continue;
                }

                failedPranam =
                    pranam;

                return false;
            }

            return true;
        }

        /// <summary>
        /// Pranam初期化の失敗状態を設定し、
        /// 原因をログへ出力します。
        /// </summary>
        private void FailPranamInitialization(
            string message,
            UnityEngine.Object context)
        {
            _isPranamInitialized =
                false;

            _isPranamInitializationStarted =
                false;

            _hasPranamInitializationFailed =
                true;

            Debug.LogError(
                $"{nameof(TacticalMapUnitBehavior)}: " +
                message,
                context);
        }

        /// <summary>
        /// MainまたはSubのPranamスロットを初期化します。
        /// </summary>
        /// <param name="slotRoot">
        /// Pranamスロットを格納するTransformです。
        /// </param>
        /// <param name="pranamBehavior">
        /// Pranamの使用と同期を管理するコンポーネントです。
        /// </param>
        /// <param name="pranamDataList">
        /// スロットへ設定するPranamデータです。
        /// </param>
        /// <param name="yRotation">
        /// Pranamの初期水平角度です。
        /// </param>
        /// <param name="isMain">
        /// Main側を初期化する場合は<c>true</c>です。
        /// </param>
        /// <remarks>
        /// 水平角度は照準と同期が参照する親スロットへ適用します。
        /// 各武器はローカル角度0で初期化し、検出扇形と親の向きを一致させます。
        /// </remarks>
        private void InitializePranamSlots(
            Transform slotRoot,
            PranamBehavior pranamBehavior,
            List<PranamData> pranamDataList,
            int yRotation,
            bool isMain)
        {
            string sideName =
                isMain
                    ? "Main"
                    : "Sub";

            if (slotRoot == null ||
                pranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{sideName}側のPranam参照が設定されていません。",
                    this);

                return;
            }

            if (pranamDataList == null)
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{sideName}側のPranamデータがnullです。" +
                    "すべてのスロットを無効化します。",
                    this);

                DisableRemainingSlots(
                    slotRoot,
                    0);

                return;
            }

            slotRoot.rotation =
                Quaternion.Euler(
                    0.0f,
                    yRotation,
                    0.0f);

            if (slotRoot.childCount <
                pranamDataList.Count)
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{sideName}側のPranamスロットが不足しています。" +
                    $" Unit={_unitStatus.UnitName}," +
                    $" DataCount={pranamDataList.Count}," +
                    $" SlotCount={slotRoot.childCount}",
                    this);
            }

            int initializedSlotCount =
                Mathf.Min(
                    pranamDataList.Count,
                    slotRoot.childCount);

            int initialSlotIndex =
                GetInitialPranamSlotIndex(
                    pranamDataList,
                    initializedSlotCount);

            for (int index = 0;
                index < initializedSlotCount;
                index++)
            {
                GameObject slotObject =
                    slotRoot.GetChild(
                            index)
                        .gameObject;

                InitializePranamSlot(
                    slotObject,
                    pranamBehavior,
                    pranamDataList[index],
                    0,
                    isMain,
                    index,
                    index == initialSlotIndex);
            }

            DisableRemainingSlots(
                slotRoot,
                initializedSlotCount);
        }

        /// <summary>
        /// 初期使用するPranamのスロット番号を取得します。
        /// </summary>
        /// <param name="pranamDataList">
        /// Pranam設定一覧です。
        /// </param>
        /// <param name="slotCount">
        /// 初期化対象のスロット数です。
        /// </param>
        /// <returns>
        /// 初期使用するスロット番号です。
        /// 有効なスロットが存在しない場合は<c>-1</c>です。
        /// </returns>
        /// <remarks>
        /// 初期化時のみNONEを除外し、
        /// NONE以外で最も番号の小さいスロットを選択します。
        ///
        /// すべてNONEの場合はスロット0を選択します。
        /// </remarks>
        private static int GetInitialPranamSlotIndex(
            List<PranamData> pranamDataList,
            int slotCount)
        {
            if (pranamDataList == null ||
                slotCount <= 0)
            {
                return -1;
            }

            int validSlotCount =
                Mathf.Min(
                    pranamDataList.Count,
                    slotCount);

            for (int index = 0;
                index < validSlotCount;
                index++)
            {
                PranamData pranamData =
                    pranamDataList[index];

                if (pranamData == null ||
                    IsNonePranam(
                        pranamData))
                {
                    continue;
                }

                return index;
            }

            return validSlotCount > 0
                ? 0
                : -1;
        }

        /// <summary>
        /// 1つのPranamスロットを初期化します。
        /// </summary>
        private void InitializePranamSlot(
            GameObject slotObject,
            PranamBehavior pranamBehavior,
            PranamData pranamData,
            int yRotation,
            bool isMain,
            int slotIndex,
            bool isActive)
        {
            if (slotObject == null)
            {
                return;
            }

            slotObject.SetActive(
                false);

            ResetPranamSlotTransform(
                slotObject.transform);

            if (pranamData == null ||
                IsNonePranam(
                    pranamData))
            {
                InitializeNonePranamSlot(
                    slotObject,
                    pranamData,
                    isMain,
                    slotIndex,
                    isActive);

                return;
            }

            string displayName =
                string.IsNullOrWhiteSpace(
                    pranamData.DisplayName)
                    ? pranamData.Range
                    : pranamData.DisplayName.Trim();

            slotObject.name =
                displayName;

            if (!TryResolvePranamType(
                    pranamData,
                    out Type pranamType))
            {
                return;
            }

            PranamBase pranamBase =
                GetOrAddPranamComponent(
                    slotObject,
                    pranamType);

            if (pranamBase == null)
            {
                return;
            }

            pranamBase.PrepareRuntimeInitialization();

            pranamBase.Initialize(
                _unitStatus.PlayerId,
                isMain,
                slotIndex,
                _unitBehavior,
                pranamBehavior,
                pranamData,
                yRotation);

            slotObject.SetActive(
                true);

            if (!IsLocalPlayerUnit())
            {
                int defaultLayer =
                    LayerMask.NameToLayer(
                        k_DefaultLayerName);

                if (defaultLayer >= 0)
                {
                    PranamBase.SetLayerRecursively(
                        slotObject,
                        defaultLayer);
                }
            }

            InitializePranamSlotUi(
                slotObject,
                pranamData.Icon,
                displayName,
                pranamData.ActionPointCost,
                isMain,
                slotIndex,
                isActive);

            slotObject.SetActive(
                isActive);

            if (!pranamBase.EnsureRuntimeReady())
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "Pranamの最終実行時状態が未完成です。" +
                    $" PlayerId={_unitStatus.PlayerId}," +
                    $" Side={(isMain ? "Main" : "Sub")}," +
                    $" Slot={slotIndex}," +
                    $" Pranam={displayName}," +
                    $" RequiresInput={pranamBase.RequiresInput}," +
                    $" RuntimeInitialized={pranamBase.IsRuntimeInitialized}," +
                    $" RequiresArc={pranamBase.RequiresArcInitialization}," +
                    $" ArcInitialized={pranamBase.IsArcInitialized}," +
                    $" RuntimeReady={pranamBase.IsRuntimeReady}," +
                    $" ActiveSelf={slotObject.activeSelf}," +
                    $" ActiveInHierarchy={slotObject.activeInHierarchy}," +
                    $" LocalPosition={slotObject.transform.localPosition}," +
                    $" LocalRotation={slotObject.transform.localEulerAngles}," +
                    $" LocalScale={slotObject.transform.localScale}",
                    slotObject);
            }
        }

        /// <summary>
        /// PranamスロットのローカルTransformを
        /// 親オブジェクト基準の初期状態へ戻します。
        /// </summary>
        /// <param name="slotTransform">
        /// 初期化するスロットのTransformです。
        /// </param>
        private static void ResetPranamSlotTransform(
            Transform slotTransform)
        {
            if (slotTransform == null)
            {
                return;
            }

            slotTransform.localPosition =
                Vector3.zero;

            slotTransform.localRotation =
                Quaternion.identity;

            slotTransform.localScale =
                Vector3.one;
        }

        /// <summary>
        /// 空のPranamスロットを初期化します。
        /// </summary>
        private void InitializeNonePranamSlot(
            GameObject slotObject,
            PranamData pranamData,
            bool isMain,
            int slotIndex,
            bool isActive)
        {
            slotObject.name =
                k_NonePranamName;

            InitializePranamSlotUi(
                slotObject,
                pranamData != null
                    ? pranamData.Icon
                    : null,
                k_NonePranamName,
                0,
                isMain,
                slotIndex,
                isActive);

            slotObject.SetActive(
                isActive);
        }

        /// <summary>
        /// Pranamデータに対応するクラス型を取得します。
        /// </summary>
        private bool TryResolvePranamType(
            PranamData pranamData,
            out Type pranamType)
        {
            pranamType =
                null;

            if (pranamData == null ||
                string.IsNullOrWhiteSpace(
                    pranamData.Range))
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "Pranamのクラス名が設定されていません。" +
                    $" Pranam={pranamData?.DisplayName}",
                    this);

                return false;
            }

            string rangeName =
                pranamData.Range.Trim();

            string fullClassName =
                rangeName.StartsWith(
                    "PranaTactics.",
                    StringComparison.Ordinal)
                    ? rangeName
                    : k_PranamNamespace +
                    rangeName;

            Type resolvedType =
                typeof(PranamBase)
                    .Assembly
                    .GetType(
                        fullClassName);

            if (resolvedType == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "PranamBase派生クラスが見つかりません。" +
                    $" ClassName={fullClassName}," +
                    $" Pranam={pranamData.DisplayName}",
                    pranamData);

                return false;
            }

            if (!typeof(PranamBase).IsAssignableFrom(
                    resolvedType) ||
                resolvedType.IsAbstract)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "指定されたクラスは生成可能なPranamBase派生型ではありません。" +
                    $" ClassName={fullClassName}",
                    pranamData);

                return false;
            }

            pranamType =
                resolvedType;

            return true;
        }

        /// <summary>
        /// スロットへ指定されたPranamBase派生コンポーネントを設定します。
        /// </summary>
        private PranamBase GetOrAddPranamComponent(
            GameObject slotObject,
            Type pranamType)
        {
            PranamBase existingPranam =
                slotObject.GetComponent<PranamBase>();

            if (existingPranam != null)
            {
                if (existingPranam.GetType() ==
                    pranamType)
                {
                    return existingPranam;
                }

                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "スロットには異なるPranamBase派生クラスが" +
                    "既に設定されています。" +
                    $" Slot={slotObject.name}," +
                    $" Existing={existingPranam.GetType().FullName}," +
                    $" Requested={pranamType.FullName}",
                    slotObject);

                return null;
            }

            Component addedComponent =
                slotObject.AddComponent(
                    pranamType);

            PranamBase pranamBase =
                addedComponent as PranamBase;

            if (pranamBase == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "PranamBase派生コンポーネントの追加に失敗しました。" +
                    $" Type={pranamType.FullName}",
                    slotObject);
            }

            return pranamBase;
        }

        /// <summary>
        /// ローカルプレイヤー用のPranamスロットUIを初期化します。
        /// </summary>
        private void InitializePranamSlotUi(
            GameObject slotObject,
            Sprite icon,
            string displayName,
            int actionPointCost,
            bool isMain,
            int slotIndex,
            bool isActive)
        {
            if (!IsLocalPlayerUnit())
            {
                return;
            }

            PranamUIManager uiManager =
                PranamUIManager.Instance;

            if (uiManager == null)
            {
                return;
            }

            if (isMain)
            {
                uiManager.InitializeMainPranamSlotUI(
                    slotObject,
                    icon,
                    displayName,
                    actionPointCost,
                    slotIndex,
                    isActive);

                return;
            }

            uiManager.InitializeSubPranamSlotUI(
                slotObject,
                icon,
                displayName,
                actionPointCost,
                slotIndex,
                isActive);
        }

        /// <summary>
        /// 指定された位置以降のPranamスロットを無効化します。
        /// </summary>
        private static void DisableRemainingSlots(
            Transform slotRoot,
            int startIndex)
        {
            if (slotRoot == null)
            {
                return;
            }

            for (int index = startIndex;
                index < slotRoot.childCount;
                index++)
            {
                GameObject slotObject =
                    slotRoot.GetChild(
                            index)
                        .gameObject;

                ResetPranamSlotTransform(
                    slotObject.transform);

                slotObject.SetActive(
                    false);
            }
        }

        /// <summary>
        /// ローカルプレイヤーとのチーム関係に応じて、
        /// Pranamの扇型範囲の表示状態を更新します。
        /// </summary>
        public void RefreshPranamRangeVisibility()
        {
            bool shouldShow =
                IsSameTeamAsLocalPlayer();

            PranamBase[] pranams =
                GetComponentsInChildren<PranamBase>(
                    true);

            foreach (PranamBase pranam in
                    pranams)
            {
                if (pranam == null)
                {
                    continue;
                }

                MeshRenderer arcRenderer =
                    pranam.GetComponent<MeshRenderer>();

                if (arcRenderer != null)
                {
                    arcRenderer.enabled =
                        shouldShow;
                }
            }
        }

        /// <summary>
        /// このユニットがローカルプレイヤーと
        /// 同じチームかどうかを判定します。
        /// </summary>
        private bool IsSameTeamAsLocalPlayer()
        {
            if (_unitStatus == null)
            {
                return false;
            }

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            int localPlayerId =
                unitManager.ControlledUnitId;

            if (localPlayerId < 0)
            {
                return false;
            }

            int localTeamId =
                unitManager.GetTeamId(
                    localPlayerId);

            if (localTeamId < 0)
            {
                return false;
            }

            return _unitStatus.TeamId ==
                localTeamId;
        }

        /// <summary>
        /// 指定されたPranamデータが空スロットかどうかを判定します。
        /// </summary>
        private static bool IsNonePranam(
            PranamData pranamData)
        {
            if (pranamData == null)
            {
                return true;
            }

            return string.Equals(
                    pranamData.DisplayName,
                    k_NonePranamName,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    pranamData.Range,
                    k_NonePranamName,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Pranam初期化に必要な参照を検証します。
        /// </summary>
        private bool ValidatePranamInitializationReferences()
        {
            if (_unitStatus == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{nameof(UnitStatus)}が設定されていません。",
                    this);

                return false;
            }

            if (_unitBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    $"{nameof(UnitBehavior)}が設定されていません。",
                    this);

                return false;
            }

            if (_mainPranamSlotsRoot == null ||
                _subPranamSlotsRoot == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "MainまたはSubのPranamスロットルートが" +
                    "設定されていません。" +
                    $" Main={_mainPranamSlotsRoot != null}," +
                    $" Sub={_subPranamSlotsRoot != null}",
                    this);

                return false;
            }

            if (_mainPranamBehavior == null ||
                _subPranamBehavior == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "MainまたはSubのPranamBehaviorが設定されていません。" +
                    $" Main={_mainPranamBehavior != null}," +
                    $" Sub={_subPranamBehavior != null}",
                    this);

                return false;
            }

            return true;
        }

        /// <summary>
        /// 戦術マップ上の指定座標に対応する位置を取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinatePosition(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            return boardManager != null
                ? boardManager.GetMapHexCellPosition(
                    x,
                    y)
                : transform.position;
        }

        /// <summary>
        /// 戦術マップ上の指定座標に対応するマスのスケールを取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinateScale(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            return boardManager != null
                ? boardManager.GetMapHexCellScale(
                    x,
                    y)
                : Vector3.zero;
        }

        /// <summary>
        /// テレポート移動完了後の座標と移動状態を更新します。
        /// </summary>
        protected override void TeleportMoveEnd(
            Vector2Int coordinate)
        {
            _currentCoordinate =
                coordinate;

            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _hasMoveTarget =
                false;

            _unitStatus?.SetMovable(
                false);
        }

        /// <summary>
        /// 戦術マップユニットの補間移動完了状態を反映します。
        /// </summary>
        protected override void LerpMoveEnd()
        {
            _currentCoordinate =
                _nextCoordinate;

            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _hasMoveTarget =
                false;
        }

        /// <summary>
        /// マクロの経路表示を通常速度で追従し、ミクロでは実際の操作速度を使用します。
        /// </summary>
        protected override float GetHorizontalMoveSpeed()
        {
            return MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro
                ? _unitStatus.MoveSpeed
                : base.GetHorizontalMoveSpeed();
        }

        /// <summary>
        /// Microユニットの移動完了時に、
        /// 戦術マップユニットを目的地へ即座に配置します。
        /// </summary>
        public void ForceCompleteLerpMove()
        {
            if (!_hasMoveTarget)
            {
                return;
            }

            transform.position =
                _towardPosition;

            _velocity =
                Vector3.zero;

            LerpMoveEnd();
        }

        /// <summary>
        /// 共有の移動可否を変更せず、ミクロ移動の確定マスと進捗を表示します。
        /// </summary>
        public void ApplyMicroMovementPosition(
            Vector2Int coordinate,
            Vector2Int destination,
            float progress)
        {
            if (!TryCalculateMoveTargetPosition(coordinate.x, coordinate.y, out Vector3 source))
            {
                return;
            }

            Vector3 target = source;
            if (progress > 0.0f &&
                !TryCalculateMoveTargetPosition(destination.x, destination.y, out target))
            {
                return;
            }

            transform.position = Vector3.Lerp(source, target, Mathf.Clamp01(progress));
            _currentCoordinate = coordinate;
            _nextCoordinate = default;
            _towardPosition = default;
            _velocity = Vector3.zero;
            _hasMoveTarget = false;
        }

        /// <summary>
        /// 指定された戦術マップ座標への補間移動を開始します。
        /// </summary>
        public override void LerpMoveToCoordinate(
            int x,
            int y)
        {
            if (IsMovementLocked)
            {
                return;
            }

            _nextCoordinate =
                new Vector2Int(
                    x,
                    y);

            _hasMoveTarget =
                true;

            base.LerpMoveToCoordinate(
                x,
                y);
        }

        /// <summary>
        /// Main Pranamの射程方向を回転します。
        /// </summary>
        public void RotateMainPranam(
            float yRotation)
        {
            Transform mainTransform =
                GetMainPranamTransform();

            if (mainTransform == null)
            {
                return;
            }

            mainTransform.Rotate(
                0.0f,
                yRotation,
                0.0f,
                Space.Self);
        }

        /// <summary>
        /// Sub Pranamの射程方向を回転します。
        /// </summary>
        public void RotateSubPranam(
            float yRotation)
        {
            Transform subTransform =
                GetSubPranamTransform();

            if (subTransform == null)
            {
                return;
            }

            subTransform.Rotate(
                0.0f,
                yRotation,
                0.0f,
                Space.Self);
        }

        /// <summary>
        /// Main Pranamの射程方向を回転します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// </remarks>
        [Obsolete(
            "Use RotateMainPranam instead.")]
        public void RotateMainTrigger(
            float yRotation)
        {
            RotateMainPranam(
                yRotation);
        }

        /// <summary>
        /// Sub Pranamの射程方向を回転します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// </remarks>
        [Obsolete(
            "Use RotateSubPranam instead.")]
        public void RotateSubTrigger(
            float yRotation)
        {
            RotateSubPranam(
                yRotation);
        }

        /// <summary>
        /// ミニマップカメラが現在描画可能な状態かどうかを取得します。
        /// </summary>
        public bool IsMinimapCameraActive()
        {
            return UnitCamera != null &&
                UnitCamera.gameObject.activeInHierarchy &&
                UnitCamera.enabled;
        }

        /// <summary>
        /// ミニマップカメラが現在描画可能な状態かどうかを取得します。
        /// </summary>
        /// <remarks>
        /// 既存コードとの互換性維持用です。
        /// 新しいコードでは<see cref="IsMinimapCameraActive"/>を
        /// 使用してください。
        /// </remarks>
        [Obsolete(
            "Use IsMinimapCameraActive instead.")]
        public bool IsLocalShouldToggleMapCameraActive()
        {
            return IsMinimapCameraActive();
        }

        /// <summary>
        /// ローカルプレイヤー変更後に、
        /// ミニマップと通常HUDを再初期化します。
        /// </summary>
        public void RefreshLocalMapView()
        {
            if (_unitStatus == null)
            {
                return;
            }

            LocalCameraOn();

            if (!IsLocalPlayerUnit())
            {
                return;
            }

            SetMapMode(
                false);

            RestartMinimapInitializationRetry();

            LogMinimapState(
                "RefreshLocalMapView");
        }

        /// <summary>
        /// 引き継ぎ先の既存兵装をスロットUIへ接続し、選択状態と残り硬直時間を反映します。
        /// </summary>
        public void RefreshControlledPranamUi()
        {
            if (!IsLocalPlayerUnit() || _unitStatus == null || GameDataRepository.Instance == null)
            {
                return;
            }

            List<List<PranamData>> lists = GameDataRepository.Instance.GetPranams(_unitStatus.UnitName);

            if (lists == null || lists.Count < 2)
            {
                return;
            }

            RefreshControlledPranamSlots(_mainPranamSlotsRoot, _mainPranamBehavior, lists[0], true);
            RefreshControlledPranamSlots(_subPranamSlotsRoot, _subPranamBehavior, lists[1], false);
        }

        private void RefreshControlledPranamSlots(
            GameObject root,
            PranamBehavior behavior,
            List<PranamData> data,
            bool isMain)
        {
            if (root == null || behavior == null || data == null)
            {
                return;
            }

            int selectedIndex = behavior.SelectedSlotIndex;
            int rangeLayer = LayerMask.NameToLayer("TriggerRange");

            for (int index = 0; index < root.transform.childCount; index++)
            {
                if (rangeLayer >= 0)
                {
                    PranamBase.SetLayerRecursively(root.transform.GetChild(index).gameObject, rangeLayer);
                }

                PranamData item = index < data.Count ? data[index] : null;

                InitializePranamSlotUi(root.transform.GetChild(index).gameObject, item != null ? item.Icon : null,
                    item != null ? item.DisplayName : k_NonePranamName, item != null ? item.ActionPointCost : 0, isMain, index, index == selectedIndex);
                PranamUIManager.Instance?.SetCooldownGauge(isMain, index, 0.0f);
            }

            behavior.RefreshControlledUi();
        }

        /// <summary>
        /// フルマップ表示状態を設定します。
        /// </summary>
        /// <param name="isMapMode">
        /// フルマップを表示する場合は<c>true</c>です。
        /// </param>
        public void SetMapMode(
            bool isMapMode)
        {
            if (!IsLocalPlayerUnit())
            {
                return;
            }

            _isMapMode =
                isMapMode;

            HUDManager.Instance?.SetMapMode(
                _isMapMode);

            GameObject fullMapCamera =
                BoardManager.Instance?
                    .GetMinimapCameraObject();

            if (fullMapCamera != null)
            {
                fullMapCamera.SetActive(
                    _isMapMode);
            }

            SwitchCamera(
                !_isMapMode);

            if (!_isMapMode)
            {
                EnsureMinimapRendering(
                    "ReturnFromFullMap");
            }
        }

        /// <summary>
        /// ミニマップとフルマップの表示を切り替えます。
        /// </summary>
        public void ToggleMapMode()
        {
            SetMapMode(
                !_isMapMode);
        }

        /// <summary>
        /// 移動ロック、マップ切り替え入力、
        /// ミニマップ状態および補間移動を更新します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (IsLocalPlayerUnit())
            {
                UpdateMapModeInput();
                RepairMinimapIfNeeded();
            }

            if (_hasMoveTarget)
            {
                UnitHorizontalMove();
            }
        }

        /// <summary>
        /// ユニットの移動後にカメラの高さを確定します。
        /// </summary>
        private void LateUpdate()
        {
            if (IsLocalPlayerUnit())
            {
                UpdateMinimapCameraHeight();
            }
        }

        /// <summary>
        /// 実行中のミニマップ初期化処理を停止します。
        /// </summary>
        private void OnDisable()
        {
            StopMinimapInitializationRetry();
        }

        /// <summary>
        /// 使用中の扇形範囲または固定倍率からミニマップカメラを配置します。
        /// </summary>
        private void UpdateMinimapCameraHeight()
        {
            Camera camera = UnitCamera;

            if (camera == null)
            {
                return;
            }

            UserSettings settings = SettingsManager.Current;
            Transform cameraTransform = camera.transform;
            Vector3 localPosition = cameraTransform.localPosition;

            if (settings.IsMinimapVariable)
            {
                float requiredWorldHeight = MinimapViewMath.CalculateCameraHeight(
                    GetActiveAttackRangeRadius(_mainPranamSlotsRoot),
                    GetActiveAttackRangeRadius(_subPranamSlotsRoot),
                    camera.fieldOfView, camera.aspect);
                Vector3 worldPosition = cameraTransform.position;

                worldPosition.y = transform.position.y + requiredWorldHeight;
                Vector3 requiredLocalPosition = cameraTransform.parent != null
                    ? cameraTransform.parent.InverseTransformPoint(worldPosition)
                    : worldPosition;

                localPosition.y = MinimapViewMath.ClampLocalCameraHeight(requiredLocalPosition.y);
            }
            else
            {
                localPosition.y = MinimapViewMath.CalculateFixedCameraHeight(settings.MinimapZoom);
            }

            cameraTransform.localPosition = localPosition;
            float worldHeight = Mathf.Max(0.0f, cameraTransform.position.y - transform.position.y);

            camera.farClipPlane = Mathf.Max(
                camera.farClipPlane,
                worldHeight +
                MinimapViewMath.MinimumCameraHeight);

            if (camera.orthographic)
            {
                camera.orthographicSize = worldHeight * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            }
        }

        /// <summary>
        /// 現在選択されているスロットだけから扇形の半径を取得します。
        /// </summary>
        private static float GetActiveAttackRangeRadius(GameObject slotsRoot)
        {
            if (slotsRoot == null)
            {
                return 0.0f;
            }

            foreach (Transform slot in slotsRoot.transform)
            {
                if (slot.gameObject.activeSelf && slot.TryGetComponent(out PranamBase pranam))
                {
                    return pranam.GetAttackRangeRadius();
                }
            }

            return 0.0f;
        }

        /// <summary>
        /// マップ切り替え入力を処理します。
        /// </summary>
        private void UpdateMapModeInput()
        {
            if (MatchPhaseManager.Instance != null &&
                (MatchPhaseManager.Instance.CurrentPhase == MatchPhase.MicroTransition ||
                 MatchPhaseManager.Instance.CurrentPhase == MatchPhase.MacroTransition))
            {
                if (_isMapMode)
                {
                    SetMapMode(false);
                }

                return;
            }

            InputManager inputManager =
                InputManager.Instance;

            if (inputManager == null)
            {
                return;
            }

            bool isInputBlocked = !Application.isFocused ||
                (PranaTactics.UI.Screens.SettingsUIManager.Instance != null &&
                 PranaTactics.UI.Screens.SettingsUIManager.Instance.IsOpen);
            UnitInputState state = inputManager.CurrentState;

            if (SettingsManager.Current.Controls.HoldMapDisplay)
            {
                bool shouldShowMap = !isInputBlocked && state.IsMapHeld;

                if (_isMapMode != shouldShowMap)
                {
                    SetMapMode(shouldShowMap);
                }
            }
            else if (!isInputBlocked && state.ShouldToggleMapCamera)
            {
                ToggleMapMode();
            }
        }

        /// <summary>
        /// 通常表示中にミニマップカメラの状態が失われた場合、
        /// 一定間隔で状態を復元します。
        /// </summary>
        private void RepairMinimapIfNeeded()
        {
            if (_isMapMode ||
                Time.unscaledTime <
                _nextMinimapRepairTime)
            {
                return;
            }

            _nextMinimapRepairTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    _minimapRepairInterval);

            if (IsMinimapRenderingValid())
            {
                return;
            }

            EnsureMinimapRendering(
                "UpdateSelfRepair");
        }

        /// <summary>
        /// ミニマップカメラとRenderTextureが
        /// 描画可能な状態かどうかを確認します。
        /// </summary>
        private bool IsMinimapRenderingValid()
        {
            return UnitCamera != null &&
                UnitCamera.gameObject.activeInHierarchy &&
                UnitCamera.enabled &&
                UnitCamera.targetTexture != null &&
                UnitCamera.targetTexture.IsCreated();
        }

        /// <summary>
        /// ミニマップカメラ、RenderTextureおよび
        /// RawImageの状態を設定します。
        /// </summary>
        private void EnsureMinimapRendering(
            string reason)
        {
            if (!IsLocalPlayerUnit() ||
                _isMapMode)
            {
                return;
            }

            Camera minimapCamera =
                UnitCamera;

            if (minimapCamera == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "ミニマップ用カメラが設定されていません。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" Reason={reason}",
                    this);

                return;
            }

            GameObject cameraObject =
                minimapCamera.gameObject;

            if (!cameraObject.activeSelf)
            {
                cameraObject.SetActive(
                    true);
            }

            minimapCamera.enabled =
                true;

            RenderTexture targetTexture =
                minimapCamera.targetTexture;

            if (targetTexture == null)
            {
                Debug.LogError(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "ミニマップカメラのRenderTextureが設定されていません。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" Camera={minimapCamera.name}," +
                    $" Reason={reason}",
                    minimapCamera);

                return;
            }

            if (!targetTexture.IsCreated())
            {
                bool wasCreated =
                    targetTexture.Create();

                if (!wasCreated)
                {
                    Debug.LogWarning(
                        $"{nameof(TacticalMapUnitBehavior)}: " +
                        "ミニマップRenderTextureを生成できませんでした。" +
                        $" Texture={targetTexture.name}," +
                        $" Reason={reason}",
                        targetTexture);

                    return;
                }
            }

            BindMinimapTextureToUi(
                targetTexture,
                reason);
        }

        /// <summary>
        /// RenderTextureをミニマップRawImageへ設定します。
        /// </summary>
        private void BindMinimapTextureToUi(
            RenderTexture targetTexture,
            string reason)
        {
            if (targetTexture == null)
            {
                return;
            }

            RawImage rawImage =
                FindMinimapRawImage(
                    targetTexture);

            if (rawImage == null)
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "ミニマップ表示用RawImageを取得できません。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" Texture={targetTexture.name}," +
                    $" Reason={reason}",
                    this);

                return;
            }

            Texture previousTexture =
                rawImage.texture;

            if (!ReferenceEquals(
                    previousTexture,
                    targetTexture))
            {
                rawImage.texture =
                    targetTexture;
            }

            if (!rawImage.gameObject.activeSelf)
            {
                rawImage.gameObject.SetActive(
                    true);
            }

            rawImage.enabled =
                true;

            Color imageColor =
                rawImage.color;

            imageColor.a =
                1.0f;

            rawImage.color =
                imageColor;

            rawImage.canvasRenderer.SetAlpha(
                1.0f);

            CanvasGroup[] canvasGroups =
                rawImage.GetComponentsInParent<CanvasGroup>(
                    true);

            foreach (CanvasGroup canvasGroup in
                    canvasGroups)
            {
                if (canvasGroup == null ||
                    !ContainsMinimapName(
                        GetHierarchyPath(
                            canvasGroup.transform)))
                {
                    continue;
                }

                canvasGroup.alpha =
                    1.0f;
            }

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "ミニマップUIへRenderTextureを設定しました。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" RawImage={GetHierarchyPath(rawImage.transform)}," +
                    $" OldTexture={previousTexture?.name ?? "null"}," +
                    $" NewTexture={targetTexture.name}," +
                    $" Reason={reason}",
                    this);
            }
        }

        /// <summary>
        /// ミニマップ表示に使用するRawImageを取得します。
        /// </summary>
        private RawImage FindMinimapRawImage(
            RenderTexture targetTexture)
        {
            if (_minimapRawImage != null)
            {
                return _minimapRawImage;
            }

            if (!_allowAutomaticMinimapUiSearch ||
                Time.unscaledTime <
                _nextMinimapUiSearchTime)
            {
                return null;
            }

            _nextMinimapUiSearchTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    _minimapRepairInterval);

            RawImage bestCandidate =
                null;

            int bestScore =
                int.MinValue;

            RawImage[] rawImages =
                Resources.FindObjectsOfTypeAll<RawImage>();

            foreach (RawImage rawImage in
                    rawImages)
            {
                if (rawImage == null ||
                    !rawImage.gameObject.scene.IsValid())
                {
                    continue;
                }

                int score =
                    CalculateMinimapRawImageScore(
                        rawImage,
                        targetTexture);

                if (score <=
                    bestScore)
                {
                    continue;
                }

                bestScore =
                    score;

                bestCandidate =
                    rawImage;
            }

            if (bestScore <
                100)
            {
                return null;
            }

            _minimapRawImage =
                bestCandidate;

            return _minimapRawImage;
        }

        /// <summary>
        /// RawImageがミニマップ表示用である可能性を評価します。
        /// </summary>
        private static int CalculateMinimapRawImageScore(
            RawImage rawImage,
            RenderTexture targetTexture)
        {
            int score =
                0;

            if (ReferenceEquals(
                    rawImage.texture,
                    targetTexture))
            {
                score +=
                    1000;
            }

            if (rawImage.texture != null &&
                targetTexture != null &&
                string.Equals(
                    rawImage.texture.name,
                    targetTexture.name,
                    StringComparison.OrdinalIgnoreCase))
            {
                score +=
                    500;
            }

            string hierarchyPath =
                GetHierarchyPath(
                    rawImage.transform);

            if (ContainsMinimapName(
                    hierarchyPath))
            {
                score +=
                    300;
            }

            if (ContainsMinimapName(
                    rawImage.name))
            {
                score +=
                    100;
            }

            return score;
        }

        /// <summary>
        /// ミニマップ初期化の再試行を開始します。
        /// </summary>
        private void RestartMinimapInitializationRetry()
        {
            StopMinimapInitializationRetry();

            _minimapInitializationCoroutine =
                StartCoroutine(
                    RetryInitializeMinimap());
        }

        /// <summary>
        /// ミニマップ初期化の再試行を停止します。
        /// </summary>
        private void StopMinimapInitializationRetry()
        {
            if (_minimapInitializationCoroutine == null)
            {
                return;
            }

            StopCoroutine(
                _minimapInitializationCoroutine);

            _minimapInitializationCoroutine =
                null;
        }

        /// <summary>
        /// 初期化順序の差に備え、
        /// 複数フレームにわたってミニマップ状態を復元します。
        /// </summary>
        private IEnumerator RetryInitializeMinimap()
        {
            int retryCount =
                Mathf.Max(
                    1,
                    _minimapRetryFrameCount);

            for (int frameIndex = 0;
                frameIndex < retryCount;
                frameIndex++)
            {
                yield return null;

                if (!isActiveAndEnabled ||
                    !IsLocalPlayerUnit())
                {
                    _minimapInitializationCoroutine =
                        null;

                    yield break;
                }

                EnsureMinimapRendering(
                    $"RetryFrame{frameIndex + 1}");

                HUDManager.Instance?.SetMapMode(
                    false);
            }

            _minimapInitializationCoroutine =
                null;

            LogMinimapState(
                "RetryCompleted");
        }

        /// <summary>
        /// 現在のユニットがローカルプレイヤー所有か判定します。
        /// </summary>
        private bool IsLocalPlayerUnit()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return _unitStatus != null &&
                unitManager != null &&
                _unitStatus.PlayerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// オブジェクトの階層パスを取得します。
        /// </summary>
        private static string GetHierarchyPath(
            Transform target)
        {
            if (target == null)
            {
                return "null";
            }

            string path =
                target.name;

            Transform current =
                target.parent;

            while (current != null)
            {
                path =
                    current.name +
                    "/" +
                    path;

                current =
                    current.parent;
            }

            return path;
        }

        /// <summary>
        /// 文字列がミニマップに関連する名前を含むか確認します。
        /// </summary>
        private static bool ContainsMinimapName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return false;
            }

            string normalizedValue =
                value
                    .Replace(
                        "_",
                        string.Empty)
                    .Replace(
                        " ",
                        string.Empty)
                    .ToLowerInvariant();

            return normalizedValue.Contains(
                    "minimap") ||
                normalizedValue.Contains(
                    "smallmap") ||
                normalizedValue.Contains(
                    "ミニマップ");
        }

        /// <summary>
        /// 現在のミニマップ状態を必要に応じてログへ出力します。
        /// </summary>
        private void LogMinimapState(
            string reason)
        {
            if (!_enableDebugLogging)
            {
                return;
            }

            RenderTexture targetTexture =
                UnitCamera != null
                    ? UnitCamera.targetTexture
                    : null;

            Debug.Log(
                $"{nameof(TacticalMapUnitBehavior)}: " +
                "ミニマップ状態を確認しました。" +
                $" PlayerId={_unitStatus?.PlayerId}," +
                $" CameraActive={IsMinimapCameraActive()}," +
                $" TargetTexture={targetTexture?.name ?? "null"}," +
                $" TextureCreated={targetTexture != null && targetTexture.IsCreated()}," +
                $" Reason={reason}",
                this);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定された参照と値を検証します。
        /// </summary>
        private void OnValidate()
        {
            _minimapRepairInterval =
                Mathf.Max(
                    0.1f,
                    _minimapRepairInterval);

            _minimapRetryFrameCount =
                Mathf.Max(
                    1,
                    _minimapRetryFrameCount);

            if (_mainPranamSlotsRoot == null)
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "Main Pranam Slots Rootが設定されていません。",
                    this);
            }

            if (_subPranamSlotsRoot == null)
            {
                Debug.LogWarning(
                    $"{nameof(TacticalMapUnitBehavior)}: " +
                    "Sub Pranam Slots Rootが設定されていません。",
                    this);
            }
        }
#endif
    }
}
