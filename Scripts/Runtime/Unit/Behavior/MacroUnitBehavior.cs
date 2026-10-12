using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using PranaTactics.Board;
using PranaTactics.Controls;
using PranaTactics.Core;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Gameplay;
using PranaTactics.AI.Solo;
using PranaTactics.UI.Screens;

namespace PranaTactics.Unit
{
    /// <summary>
    /// Macro Phaseにおけるユニットの移動先選択、
    /// 移動処理および候補経路の表示を管理します。
    /// </summary>
    /// <remarks>
    /// ローカルプレイヤーのユニットは盤面クリックから
    /// 移動先を選択します。
    ///
    /// プレイヤーは目的地を指定し、公開情報に基づく危険度評価で経路を補助します。
    /// AI操作ユニットの計画はSoloAiDirectorが担当します。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MacroUnitBehavior : UnitBehaviorBase
    {
        /// <summary>
        /// AIが移動先を再評価する既定間隔です。
        /// </summary>
        private const float k_DefaultAiMoveDecisionInterval =
        0.2f;

        /// <summary>
        /// AI移動先の評価に使用する座標係数です。
        /// </summary>
        private const float k_StableCoordinateOrderMultiplier =
            1000.0f;

        /// <summary>
        /// 盤面クリックのレイキャスト対象レイヤーです。
        /// </summary>
        [Header("Board")]
        [SerializeField]
        private LayerMask _boardLayerMask;

        /// <summary>
        /// AIが移動先を再評価する間隔です。
        /// </summary>
        [Header("AI")]
        [FormerlySerializedAs("_aiMoveDecisionInterval")]
        [Min(0.05f)]
        [SerializeField]
        private float _aiMoveDecisionInterval =
            k_DefaultAiMoveDecisionInterval;

        /// <summary>
        /// AIの移動先決定をログへ出力するかどうかを示します。
        /// </summary>
        [Header("Debug")]
        [SerializeField]
        private bool _enableDebugLogging;

        /// <summary>
        /// 1回の移動で上昇可能な最大段差です。
        /// </summary>
        private int _maxStepHeight;

        /// <summary>
        /// UnitDataで指定されたマクロフェイズ専用の移動速度です。
        /// </summary>
        private float _macroUnitSpeed;

        /// <summary>
        /// UnitDataで指定されたマクロフェイズのAI専用移動速度です。
        /// </summary>
        private float _macroAiUnitSpeed;

        private readonly MacroPathHistory _pathHistory = new();

        private bool _isPathEditPending;

        /// <summary>
        /// 履歴の再表示中は通常の移動速度を使い、行動ポイント消費と経路追加を抑止します。
        /// </summary>
        private bool _isPathEditPlayback;

        /// <summary>
        /// 右クリックとドラッグを区別する操作カメラです。
        /// </summary>
        private MacroCameraZoom _cameraNavigation;

        private int _planningInputBlockedFrame = -1;

        private readonly Queue<Vector2Int> _pathPlayback = new();

        private readonly MacroRoutePreview _routePreview = new();

        private MacroRouteSearch _routeSearch;

        private IEnumerator _routeBuild;

        private AiBoardAdapter _routeBoard;

        private AiDangerEvaluator _routeDanger;

        private float _routeDangerWeight;

        private bool _isRouteDirty = true;

        private int _routeBalance;

        private int _routeMoveCost;

        private bool _routeUsesActionPoint;

        private Vector2Int? _hoverCoordinate;

        private Vector2Int[] _hoverPath = Array.Empty<Vector2Int>();

        public bool IsMacroAssistEnabled { get; private set; } = true;

#if UNITY_EDITOR || PV_CAPTURE
        /// <summary>
        /// 撮影用の経路補助を切り替え、確定済みの経路を維持して候補だけを再計算します。
        /// </summary>
        public void SetCaptureMacroAssistEnabled(bool enabled)
        {
            if (IsMacroAssistEnabled == enabled)
            {
                return;
            }

            IsMacroAssistEnabled = enabled;
            InvalidateRouteSearch();
            BlockPlanningInputForCurrentFrame();
        }

#endif

        public MacroRoutePolicy RoutePolicy
        {
            get; private set;
        }

        public string RoutePreviewStatus { get; private set; } = string.Empty;

        /// <summary>
        /// 同じMacro Phase内の再訓練でも、経路編集と探索を開始位置から初期化します。
        /// </summary>
        public void PrepareForMacroPlanning()
        {
            CancelAiMacroMovement();
            _isPathEditPending = false;
            _isPathEditPlayback = false;
            _pathPlayback.Clear();
            _isMacroPhaseInitialized = false;
            InvalidateRouteSearch();
        }

        /// <summary>
        /// チュートリアルの案内対象が変わったとき、ホバー表示を再評価します。
        /// </summary>
        public void RefreshRoutePreview()
        {
            ClearRoutePreview();
        }

        public void SetRoutePolicy(MacroRoutePolicy policy)
        {
            if (!CanEditPath || !Enum.IsDefined(typeof(MacroRoutePolicy), policy) || RoutePolicy == policy)
            {
                return;
            }

            RoutePolicy = policy;
            InvalidateRouteSearch();
        }

        public bool CanEditPath => Time.frameCount != _planningInputBlockedFrame &&
            _isMacroPhaseInitialized && IsLocalPlayerUnit() &&
            _unitStatus != null && !_unitStatus.IsDead && !_unitStatus.IsMovable &&
            !_hasSelectedCoordinate && !_hasMoveTarget && !_isMoveRequestPending && !_isPathEditPending &&
            MatchPhaseManager.Instance != null && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro &&
            MatchPhaseManager.Instance.MacroPlanning != null && !MatchPhaseManager.Instance.MacroPlanning.IsLocalConfirmed;

        public bool CanUndoPath => CanEditPath && _pathHistory.CanUndo;

        public bool CanRedoPath => CanEditPath && _pathHistory.CanRedo;

        public bool CanResetPath => CanEditPath && (_pathHistory.CanUndo || _pathHistory.CanRedo);

        /// <summary>
        /// 確認UIを閉じたクリックが同じフレームで盤面や背後のボタンへ伝わるのを防ぎます。
        /// </summary>
        public void BlockPlanningInputForCurrentFrame()
        {
            _planningInputBlockedFrame = Time.frameCount;
        }

        public void StepPath(bool isRedo)
        {
            if (!CanEditPath)
            {
                return;
            }

            Vector2Int[] previousPath = _pathHistory.GetPath();

            if (_pathHistory.TryStep(isRedo))
            {
                SendPathSnapshot(true, _pathHistory.GetTransitionFrom(previousPath));
            }
        }

        public void ResetPath()
        {
            if (!CanResetPath)
            {
                return;
            }

            _pathHistory.Clear();
            SendPathSnapshot(false);
        }

        private void SendPathSnapshot(bool shouldInterpolate, Vector2Int[] playbackPath = null)
        {
            InvalidateRouteSearch();
            _isPathEditPending = true;
            var snapshot = new MacroPathSnapshot
            {
                Turn = MatchPhaseManager.Instance.MacroPlanning.Turn,
                Coordinate = _pathHistory.Coordinate,
                Proactiveness = _pathHistory.Proactiveness,
                Path = _pathHistory.GetPath(),
                ShouldInterpolate = shouldInterpolate,
                PlaybackPath = playbackPath
            };

            _unitBehavior.GetComponent<PranaTactics.Unit.Networking.IUnitNetwork>()
                .SetMacroPath(JsonUtility.ToJson(snapshot));
        }

        /// <summary>
        /// 同期された経路編集を適用します。Micro Unitの開始位置は変更しません。
        /// </summary>
        public void ApplyPathSnapshot(string json)
        {
            if (MatchPhaseManager.Instance == null ||
                MatchPhaseManager.Instance.CurrentPhase != MatchPhase.Macro ||
                MatchPhaseManager.Instance.MacroPlanning == null || _unitStatus == null || string.IsNullOrEmpty(
                    json))
            {
                return;
            }

            MacroPathSnapshot snapshot = JsonUtility.FromJson<MacroPathSnapshot>(json);

            if (snapshot == null || snapshot.Turn != MatchPhaseManager.Instance.MacroPlanning.Turn ||
                snapshot.Path == null)
            {
                return;
            }

            _isPathEditPending = false;
            ClearRoutePreview();
            _pathPlayback.Clear();
            _unitStatus.SetMigrationPath(snapshot.Path);
            SetProactiveness(snapshot.Proactiveness);

            if (snapshot.ShouldInterpolate)
            {
                _isPathEditPlayback = true;

                if (snapshot.PlaybackPath != null)
                {
                    foreach (Vector2Int step in snapshot.PlaybackPath)
                    {
                        _pathPlayback.Enqueue(step);
                    }
                }

                if (_pathPlayback.Count == 0)
                {
                    _pathPlayback.Enqueue(snapshot.Coordinate);
                }

                BeginNextPathPlaybackStep();

                return;
            }

            _isPathEditPlayback = false;
            TeleportMoveToCoordinate(snapshot.Coordinate.x, snapshot.Coordinate.y);
            _unitBehavior.GetTacticalMapUnitBehavior()?.TeleportMoveToCoordinate(
                snapshot.Coordinate.x,
                snapshot.Coordinate.y);
        }

        /// <summary>
        /// 同期済みの表示経路を一マスずつ再生し、行動力と履歴の二重更新を防ぎます。
        /// </summary>
        private void BeginNextPathPlaybackStep()
        {
            _nextCoordinate = _pathPlayback.Dequeue();
            _hasSelectedCoordinate = true;
            base.LerpMoveToCoordinate(_nextCoordinate.x, _nextCoordinate.y);
            _unitBehavior.GetTacticalMapUnitBehavior()?.LerpMoveToCoordinate(
                _nextCoordinate.x,
                _nextCoordinate.y);
        }

        /// <summary>
        /// AIが次に移動先を評価するまでの残り時間です。
        /// </summary>
        private float _aiMoveDecisionTimer;

        /// <summary>
        /// Macro Phase開始時の初期化が完了しているかどうかです。
        /// </summary>
        private bool _isMacroPhaseInitialized;

        /// <summary>
        /// 移動設定データの読み込みが完了しているかどうかです。
        /// </summary>
        private bool _isMovementSettingsLoaded;

        /// <summary>
        /// 移動先座標が選択済みかどうかです。
        /// </summary>
        private bool _hasSelectedCoordinate;

        /// <summary>
        /// 移動要求をネットワークへ送信済みで、
        /// 適用を待っているかどうかです。
        /// </summary>
        private bool _isMoveRequestPending;

        /// <summary>
        /// 実際の移動先位置が設定されているかどうかです。
        /// </summary>
        private bool _hasMoveTarget;

        /// <summary>
        /// SoloAiDirectorが確定済み経路をMacro Phase上で
        /// 可視化しているかどうかです。
        /// </summary>
        /// <remarks>
        /// 可視化中の補間移動は表示専用です。
        /// 行動ポイントを消費せず、MigrationPathも変更しません。
        /// </remarks>
        private bool _isAiPlannedPathPlayback;

        public int MaxStepHeight
        {
            get
            {
                return _maxStepHeight;
            }
        }

        public bool HasSelectedCoordinate
        {
            get
            {
                return _hasSelectedCoordinate;
            }
        }

        public bool IsAiPlannedPathPlayback
        {
            get
            {
                return _isAiPlannedPathPlayback;
            }
        }

        /// <summary>
        /// ユニットカメラを初期状態では無効にし、
        /// 移動設定の読み込みを試みます。
        /// </summary>
        private void Awake()
        {
            if (UnitCamera != null)
            {
                MacroCameraZoom zoom = GetComponent<MacroCameraZoom>();

                if (zoom == null)
                {
                    zoom = gameObject.AddComponent<MacroCameraZoom>();
                }

                zoom.Initialize(this, UnitCamera);
                _cameraNavigation = zoom;
            }

            SwitchCamera(
                false);

            TryLoadMovementSettings();
        }

        /// <summary>
        /// 経路表示をマクロフェイズ専用の速度で再生します。
        /// </summary>
        /// <remarks>
        /// 共有ステータスの速度を変更せず、ミクロフェイズのキャラクター固有速度と停止・歩行入力を維持します。
        /// </remarks>
        protected override float GetHorizontalMoveSpeed()
        {
            return IsAiControlledUnit() ? _macroAiUnitSpeed : _macroUnitSpeed;
        }

        /// <summary>
        /// 移動ロック、Macro Phaseおよび移動処理を更新します。
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (_unitStatus == null)
            {
                return;
            }

            if (!TryLoadMovementSettings())
            {
                return;
            }

            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null)
            {
                return;
            }

            bool isLocalPlayerUnit =
                IsLocalPlayerUnit();

            if (phaseManager.CurrentPhase !=
                MatchPhase.Macro)
            {
                HandleOutsideMacroPhase(
                    isLocalPlayerUnit);

                return;
            }

            bool canExecuteMoveLogic =
                CanExecuteMoveLogic();

            if (canExecuteMoveLogic)
            {
                UpdateMacroPhaseLogic(
                    isLocalPlayerUnit);
            }

            UnitHorizontalMove();
        }

        /// <summary>
        /// 指定されたボード座標へ対応する
        /// Macro Phase盤面上の位置を取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinatePosition(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                return transform.position;
            }

            return boardManager.GetMacroPhaseHexCellPosition(
                x,
                y);
        }

        /// <summary>
        /// 指定されたボード座標へ対応する
        /// Macro Phase盤面上のマスのスケールを取得します。
        /// </summary>
        protected override Vector3 GetMoveCoordinateScale(
            int x,
            int y)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                return Vector3.zero;
            }

            return boardManager.GetMacroPhaseHexCellScale(
                x,
                y);
        }

        /// <summary>
        /// ネットワークから受信した移動先座標を保存し、
        /// 補間移動を開始します。
        /// </summary>
        /// <param name="x">
        /// 移動先のX座標です。
        /// </param>
        /// <param name="y">
        /// 移動先のY座標です。
        /// </param>
        /// <remarks>
        /// 送信側だけでなく受信側でも
        /// <see cref="_nextCoordinate"/>を設定する必要があります。
        /// これを行わない場合、移動完了時の現在座標が
        /// 正しく更新されません。
        /// </remarks>
        public override void LerpMoveToCoordinate(
            int x,
            int y)
        {
            if (!CanAffordMovement())
            {
                ClearMoveSelection();

                return;
            }

            _nextCoordinate =
                new Vector2Int(
                    x,
                    y);

            _hasSelectedCoordinate =
                true;

            _isMoveRequestPending =
                false;

            _hasMoveTarget =
                true;

            base.LerpMoveToCoordinate(
                x,
                y);
        }

        /// <summary>
        /// テレポート移動を開始する前に、
        /// 現在表示している候補経路を非表示にします。
        /// </summary>
        protected override void TeleportMoveStart()
        {
            ClearRoutePreview();
        }

        /// <summary>
        /// テレポート移動完了後の座標と状態を更新します。
        /// </summary>
        /// <param name="coordinate">
        /// テレポート後のボード座標です。
        /// </param>
        protected override void TeleportMoveEnd(
            Vector2Int coordinate)
        {
            _currentCoordinate =
                coordinate;

            ClearMoveSelection();

            if (_unitStatus != null)
            {
                _unitStatus.SetMovable(
                    false);
            }

        }

        /// <summary>
        /// 補間移動開始時に候補経路を非表示にし、
        /// 行動ポイントを消費します。
        /// </summary>
        protected override void LerpMoveStart()
        {
            ClearRoutePreview();

            _hasMoveTarget =
                true;

            if (_unitStatus == null)
            {
                return;
            }

            _unitStatus.SetMovable(
                true);

            if (_isAiPlannedPathPlayback || _isPathEditPlayback)
            {
                return;
            }

            CalcProactiveness(
                _unitStatus.MoveProactivenessCost);
        }

        /// <summary>
        /// 補間移動完了後に現在座標と移動経路を更新します。
        /// </summary>
        protected override void LerpMoveEnd()
        {
            if (!_hasSelectedCoordinate)
            {
                Debug.LogWarning(
                    $"{nameof(MacroUnitBehavior)}: " +
                    "移動先座標が設定されていない状態で" +
                    "補間移動が完了しました。",
                    this);
            }
            else
            {
                _currentCoordinate =
                    _nextCoordinate;

                if (!_isAiPlannedPathPlayback && !_isPathEditPlayback)
                {
                    if (IsLocalPlayerUnit())
                    {
                        _pathHistory.Append(_currentCoordinate, _unitStatus.Proactiveness);
                    }

                    _unitBehavior?.AddMigrationCoordinate(
                        _currentCoordinate.x,
                        _currentCoordinate.y);
                }
            }

            if (_isPathEditPlayback)
            {
                _unitBehavior.GetTacticalMapUnitBehavior()?.TeleportMoveToCoordinate(
                    _currentCoordinate.x,
                    _currentCoordinate.y);

                if (_pathPlayback.Count > 0)
                {
                    BeginNextPathPlaybackStep();

                    return;
                }

                _isPathEditPlayback = false;
            }

            ClearMoveSelection();

            if (_unitStatus != null)
            {
                _unitStatus.SetMovable(
                    false);
            }
        }

        /// <summary>
        /// Macro Phase中の操作と移動先選択を更新します。
        /// </summary>
        /// <param name="isLocalPlayerUnit">
        /// ローカルプレイヤーのユニットである場合は<c>true</c>です。
        /// </param>
        private void UpdateMacroPhaseLogic(
            bool isLocalPlayerUnit)
        {
            if (!_isMacroPhaseInitialized)
            {
                BeginMacroPhase(
                    isLocalPlayerUnit);
            }

            if (isLocalPlayerUnit && MatchPhaseManager.Instance.MacroPlanning?.IsLocalConfirmed == true)
            {
                ClearRoutePreview();

                return;
            }

            if (IsAiControlledUnit())
            {
                if (!_isAiPlannedPathPlayback)
                {
                    CancelAiMacroMovement();
                }

                return;
            }

            if (_unitStatus != null &&
                !_unitStatus.IsMovable &&
                isLocalPlayerUnit)
            {
                UpdateRouteSearch();
                TrySelectMoveCoordinateFromInput();
            }

            RequestSelectedMove();
        }

        /// <summary>
        /// AIユニットに残っているMacro Phase用の
        /// 移動要求と移動状態を解除します。
        /// </summary>
        /// <remarks>
        /// <see cref="UnitStatus.MigrationPath"/>は削除しません。
        /// SoloAiDirectorが作成した経路はMicro Phaseで使用します。
        /// </remarks>
        public void CancelAiMacroMovement()
        {
            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _hasSelectedCoordinate =
                false;

            _isMoveRequestPending =
                false;

            _hasMoveTarget =
                false;

            _unitStatus?.SetMovable(
                false);
        }

        /// <summary>
        /// AIが計画したMigrationPathの表示用再生を開始します。
        /// </summary>
        /// <returns>
        /// Macro Phase中のAIユニットで再生を開始できた場合は
        /// <c>true</c>です。
        /// </returns>
        public bool BeginAiPlannedPathPlayback()
        {
            MatchPhaseManager phaseManager =
                MatchPhaseManager.Instance;

            if (phaseManager == null ||
                phaseManager.CurrentPhase !=
                    MatchPhase.Macro ||
                !IsAiControlledUnit() ||
                _unitStatus == null)
            {
                return false;
            }

            CancelAiMacroMovement();

            _isAiPlannedPathPlayback =
                true;

            return true;
        }

        /// <summary>
        /// AIの確定済み経路の表示用再生を終了し、
        /// 未完了の表示用移動状態を解除します。
        /// </summary>
        public void EndAiPlannedPathPlayback()
        {
            if (!_isAiPlannedPathPlayback)
            {
                return;
            }

            _isAiPlannedPathPlayback =
                false;

            CancelAiMacroMovement();
        }

        /// <summary>
        /// Macro Phase開始時に行動ポイント、カメラおよび
        /// HUD表示を初期化します。
        /// </summary>
        /// <param name="isLocalPlayerUnit">
        /// ローカルプレイヤーのユニットである場合は<c>true</c>です。
        /// </param>
        private void BeginMacroPhase(
            bool isLocalPlayerUnit)
        {
            if (_unitStatus != null)
            {
                SetProactiveness(
                    _unitStatus.ActionPoint);
            }

            if (isLocalPlayerUnit)
            {
                SwitchCamera(
                    true);

                HUDManager hudManager =
                    HUDManager.Instance;

                hudManager?.SetMapMode(
                    false);

            }

            _pathHistory.Reset(_currentCoordinate, _unitStatus.Proactiveness);
            _pathPlayback.Clear();

            if (isLocalPlayerUnit)
            {
                _routeBoard = new AiBoardAdapter();
                AiWorldStateProvider world = SoloAiDirector.EnsureExists().WorldState;

                world.Update(AiWorldStateProvider.ObservationInterval, MatchPhase.Macro);
                _routeDanger = new AiDangerEvaluator(
                    _routeBoard,
                    world.BuildSnapshot(
                    _unitBehavior.GetTeamId()).Enemies,
                    _unitBehavior);
                AiRoleProfile profile = new AiWeaponSelector().BuildMovementProfile(_unitBehavior,
                    AiRoleResolver.ResolveRoleId(_unitBehavior), _routeBoard.GetCellSpacing(
                        _currentCoordinate), out AiWeaponProfile weapon);

                _routeDangerWeight = profile.DangerWeight;
                InvalidateRouteSearch();
            }

            _isPathEditPending = false;
            _isMacroPhaseInitialized =
                true;
        }

        /// <summary>
        /// Macro Phase以外の状態でカメラと盤面表示を終了します。
        /// </summary>
        /// <param name="isLocalPlayerUnit">
        /// ローカルプレイヤーのユニットである場合は<c>true</c>です。
        /// </param>
        private void HandleOutsideMacroPhase(
            bool isLocalPlayerUnit)
        {
            _isPathEditPlayback = false;
            _pathPlayback.Clear();
            _routeBuild = null;
            _routeSearch = null;
            EndAiPlannedPathPlayback();

            if (isLocalPlayerUnit)
            {
                SwitchCamera(
                    false);

                ClearRoutePreview();
            }

            _isMacroPhaseInitialized =
                false;
        }

        /// <summary>
        /// 盤面クリックからローカルプレイヤーの
        /// 移動先座標を選択します。
        /// </summary>
        private void TrySelectMoveCoordinateFromInput()
        {
            if (!CanEditPath || (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen) ||
                PranaTactics.Tutorial.TutorialSession.BlocksInput)
            {
                ClearRoutePreview();

                return;
            }

            if (Time.frameCount == _planningInputBlockedFrame ||
                MatchPhaseManager.Instance.MacroPlanning?.IsPlanningConfirmationOpen == true)
            {
                ClearRoutePreview();

                return;
            }

            if (_cameraNavigation != null && _cameraNavigation.IsPointerGestureActive)
            {
                ClearRoutePreview();
                return;
            }

            if (_isPathEditPending || InputRaycastUtility.GetUIRaycastResults().Count > 0 ||
                !IsLocalPlayerUnit() ||
                _hasSelectedCoordinate ||
                _isMoveRequestPending ||
                _hasMoveTarget)
            {
                ClearRoutePreview();

                return;
            }

            Camera unitCamera =
                UnitCamera;

            InputManager inputManager =
                InputManager.Instance;

            BoardManager boardManager =
                BoardManager.Instance;

            if (unitCamera == null ||
                inputManager == null ||
                inputManager.InputContext == null ||
                boardManager == null)
            {
                return;
            }

            if (!InputRaycastUtility.TryGetWorldObject(
                    unitCamera,
                    _boardLayerMask,
                    out RaycastHit hit))
            {
                ClearRoutePreview();

                return;
            }

            if (hit.collider == null)
            {
                return;
            }

            HexCell hexCell =
                hit.collider.GetComponent<HexCell>();

            if (hexCell == null)
            {
                hexCell =
                    hit.collider.GetComponentInParent<HexCell>();
            }

            if (hexCell == null)
            {
                return;
            }

            Vector2Int coordinate =
                hexCell.GetCoordinate();

            if (_cameraNavigation != null && _cameraNavigation.WasTargetClickReleased)
            {
                SoloAiDirector director = SoloAiDirector.EnsureExists();
                if (director.TryGetMacroTarget(coordinate, out UnitBehavior target))
                {
                    MacroPlanningUiView view = FindObjectOfType<MacroPlanningUiView>();
                    view?.OpenTargetConfirmation(target);
                }
                return;
            }

            if (_routeSearch == null || !_routeSearch.IsComplete)
            {
                ClearRoutePreview();
                RoutePreviewStatus = "候補経路を計算中…";

                return;
            }

            if (_hoverCoordinate != coordinate)
            {
                ClearRoutePreview();
                _hoverCoordinate = coordinate;
                _hoverPath = _routeSearch.GetPath(coordinate);

                if (IsMacroAssistEnabled &&
                    PranaTactics.Tutorial.TutorialSession.AllowsPlanningTarget(
                    this,
                    coordinate))
                {
                    _routePreview.Show(boardManager, _hoverPath);
                }
            }

            bool canMove = _hoverPath.Length > 0 &&
                PranaTactics.Tutorial.TutorialSession.AllowsPlanningTarget(
                this,
                coordinate);
            int cost = _routeUsesActionPoint ? _hoverPath.Length * Math.Max(
                0,
                _unitStatus.MoveProactivenessCost) : 0;

            RoutePreviewStatus = canMove
                ? $"候補 {_hoverPath.Length}マス ／ 消費 {cost} ／ 残り {_unitStatus.Proactiveness - cost}"
                : "このマスには移動できません";

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (!PranaTactics.Tutorial.TutorialSession.AllowsPlanningClick(this, coordinate, canMove) ||
                !canMove)
            {
                return;
            }

            TryAppendRoute(coordinate);
        }

        /// <summary>
        /// 最新の探索結果を一クリック分の経路として追加し、残量とともに同期します。
        /// </summary>
        public bool TryAppendRoute(Vector2Int destination)
        {
            if (!CanEditPath || _routeSearch == null || !_routeSearch.IsComplete || _isRouteDirty ||
                _routeBalance != _unitStatus.Proactiveness || _routeMoveCost != _unitStatus.MoveProactivenessCost ||
                _routeUsesActionPoint != (GameRuleManager.Instance?.CurrentRule?.UseActionPoint == true) ||
                MatchPhaseManager.Instance.MacroPlanning.IsPlanningConfirmationOpen ||
                (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen) ||
                !PranaTactics.Tutorial.TutorialSession.AllowsPlanningTarget(this, destination))
            {
                return false;
            }

            Vector2Int[] path = _routeSearch.GetPath(destination);

            if (path.Length == 0)
            {
                return false;
            }

            int cost = _routeUsesActionPoint ? path.Length * Math.Max(0, _routeMoveCost) : 0;

            _pathHistory.AppendPath(path, _unitStatus.Proactiveness - cost);
            SendPathSnapshot(true, path);

            return true;
        }

        /// <summary>
        /// 経路・残量・方針の変更時だけ探索を作り直し、フレームに分割して進めます。
        /// </summary>
        private void UpdateRouteSearch()
        {
            bool usesActionPoint = GameRuleManager.Instance?.CurrentRule?.UseActionPoint == true;

            if (_routeBalance != _unitStatus.Proactiveness ||
                _routeMoveCost != _unitStatus.MoveProactivenessCost ||
                _routeUsesActionPoint != usesActionPoint)
            {
                InvalidateRouteSearch();
            }

            if (_isRouteDirty && _routeBoard != null)
            {
                _routeBalance = _unitStatus.Proactiveness;
                _routeMoveCost = _unitStatus.MoveProactivenessCost;
                _routeUsesActionPoint = usesActionPoint;
                int maximumSteps = usesActionPoint && _routeMoveCost > 0
                    ? Math.Max(0, _routeBalance / _routeMoveCost) : int.MaxValue;

                if (!IsMacroAssistEnabled)
                {
                    maximumSteps = Math.Min(maximumSteps, 1);
                }

                _routeSearch = new MacroRouteSearch(c => _routeBoard.GetNeighbors(c),
                    (from, to) => _routeBoard.CanStep(from, to, _maxStepHeight),
                    c => IsMacroAssistEnabled ? _routeDanger.Evaluate(c) : 0);
                MacroRoutePolicy policy = IsMacroAssistEnabled ? RoutePolicy : MacroRoutePolicy.Shortest;

                _routeBuild = _routeSearch.Build(_currentCoordinate, maximumSteps, policy, _routeDangerWeight);
                _isRouteDirty = false;
            }

            if (_routeBuild != null && !_routeBuild.MoveNext())
            {
                _routeBuild = null;
            }
        }

        private void InvalidateRouteSearch()
        {
            _isRouteDirty = true;
            _routeSearch = null;
            _routeBuild = null;
            ClearRoutePreview();
        }

        private void ClearRoutePreview()
        {
            _routePreview.Clear();
            _hoverCoordinate = null;
            _hoverPath = Array.Empty<Vector2Int>();
            RoutePreviewStatus = string.Empty;
        }

        private void OnDisable()
        {
            ClearRoutePreview();
        }

        /// <summary>
        /// AIが選択した移動先座標を設定します。
        /// </summary>
        private void TrySelectMoveCoordinateByAi()
        {
            if (!IsAiControlledUnit() ||
                !IsAiMoveEnabled() ||
                _hasSelectedCoordinate ||
                _isMoveRequestPending ||
                _hasMoveTarget)
            {
                return;
            }

            _aiMoveDecisionTimer -=
                Time.deltaTime;

            if (_aiMoveDecisionTimer >
                0.0f)
            {
                return;
            }

            _aiMoveDecisionTimer =
                Mathf.Max(
                    0.05f,
                    _aiMoveDecisionInterval);

            if (!TryGetBestAiMoveCoordinate(
                    out Vector2Int coordinate))
            {
                return;
            }

            SelectMoveCoordinate(
                coordinate);

            if (_enableDebugLogging)
            {
                Debug.Log(
                    $"{nameof(MacroUnitBehavior)}: " +
                    "AIが移動先を選択しました。" +
                    $" PlayerId={_unitStatus?.PlayerId}," +
                    $" From={_currentCoordinate}," +
                    $" To={coordinate}",
                    this);
            }
        }

        /// <summary>
        /// 選択された移動先をネットワークへ要求します。
        /// </summary>
        private void RequestSelectedMove()
        {
            if (!_hasSelectedCoordinate ||
                _isMoveRequestPending ||
                _hasMoveTarget ||
                _unitBehavior == null ||
                _unitStatus == null)
            {
                return;
            }

            if (!CanAffordMovement())
            {
                ClearMoveSelection();

                return;
            }

            _isMoveRequestPending =
                true;

            _unitBehavior.LerpMoveToCoordinate(
                _unitStatus.TeamId,
                _nextCoordinate.x,
                _nextCoordinate.y);
        }

        /// <summary>
        /// 移動先座標を選択状態として保存します。
        /// </summary>
        /// <param name="coordinate">
        /// 選択する移動先座標です。
        /// </param>
        private void SelectMoveCoordinate(
            Vector2Int coordinate)
        {
            _nextCoordinate =
                coordinate;

            _hasSelectedCoordinate =
                true;

            _isMoveRequestPending =
                false;
        }

        /// <summary>
        /// AIが移動可能な隣接マスを評価し、
        /// 最も適した座標を取得します。
        /// </summary>
        /// <param name="bestCoordinate">
        /// 評価結果として選択された座標です。
        /// </param>
        /// <returns>
        /// 移動可能な座標を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetBestAiMoveCoordinate(
            out Vector2Int bestCoordinate)
        {
            bestCoordinate =
                default;

            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                return false;
            }

            bool hasEnemyTarget =
                TryGetNearestEnemyCoordinate(
                    out Vector2Int targetCoordinate);

            bool hasBestCoordinate =
                false;

            float bestScore =
                float.MaxValue;

            foreach (Vector2Int coordinate in
                    boardManager.GetAdjacentHexCellCoordinates(
                        _currentCoordinate.x,
                        _currentCoordinate.y))
            {
                if (!CanMoveToCoordinate(
                        coordinate))
                {
                    continue;
                }

                float score =
                    hasEnemyTarget
                        ? CalculateSquaredCoordinateDistance(
                            coordinate,
                            targetCoordinate)
                        : CalculateStableCoordinateOrder(
                            coordinate);

                if (hasBestCoordinate &&
                    score >= bestScore)
                {
                    continue;
                }

                bestScore =
                    score;

                bestCoordinate =
                    coordinate;

                hasBestCoordinate =
                    true;
            }

            return hasBestCoordinate;
        }

        /// <summary>
        /// 現在位置から最も近い敵ユニットの座標を取得します。
        /// </summary>
        /// <param name="nearestCoordinate">
        /// 最も近い敵ユニットの座標です。
        /// </param>
        /// <returns>
        /// 有効な敵座標を取得できた場合は<c>true</c>です。
        /// </returns>
        private bool TryGetNearestEnemyCoordinate(
            out Vector2Int nearestCoordinate)
        {
            nearestCoordinate =
                default;

            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                _unitStatus == null)
            {
                return false;
            }

            bool hasNearestEnemy =
                false;

            float minimumDistance =
                float.MaxValue;

            foreach (UnitBehavior unit in
                    unitManager.GetEnemyUnits(
                        _unitStatus.TeamId))
            {
                if (unit == null)
                {
                    continue;
                }

                MicroUnitBehavior microUnitBehavior =
                    unit.GetMicroUnitBehavior();

                if (microUnitBehavior == null)
                {
                    continue;
                }

                Vector2Int coordinate =
                    microUnitBehavior.GetCurrentCoordinate();

                float distance =
                    CalculateSquaredCoordinateDistance(
                        coordinate,
                        _currentCoordinate);

                if (hasNearestEnemy &&
                    distance >= minimumDistance)
                {
                    continue;
                }

                minimumDistance =
                    distance;

                nearestCoordinate =
                    coordinate;

                hasNearestEnemy =
                    true;
            }

            return hasNearestEnemy;
        }

        /// <summary>
        /// 指定された座標へ移動可能かどうかを判定します。
        /// </summary>
        /// <param name="coordinate">
        /// 判定する移動先座標です。
        /// </param>
        /// <returns>
        /// 現在位置に隣接し，盤面上に存在し，
        /// 段差制限内の場合は<c>true</c>です。
        /// </returns>
        /// <remarks>
        /// アウトラインの表示状態は移動可否の判定に使用しません。
        /// 表示状態を判定条件にすると，初期状態でアウトラインが
        /// 無効な場合に移動可能マスを表示できなくなるためです。
        /// </remarks>
        private bool CanMoveToCoordinate(
            Vector2Int coordinate)
        {
            if (!CanAffordMovement())
            {
                return false;
            }

            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null ||
                !boardManager.ContainsHexCell(
                    coordinate.x,
                    coordinate.y))
            {
                return false;
            }

            return IsAdjacentCoordinate(
                    coordinate) &&
                IsStepMovable(
                    _currentCoordinate,
                    coordinate);
        }

        /// <summary>
        /// 確定済みAI経路の表示再生を除き、移動に必要な残量を確認します。
        /// </summary>
        public bool CanAffordMovement()
        {
            if (_unitStatus == null)
            {
                return false;
            }

            if (_isAiPlannedPathPlayback)
            {
                return true;
            }

            var rule = GameRuleManager.Instance?.CurrentRule;

            return rule != null && (!rule.UseActionPoint ||
                _unitStatus.CanAffordProactiveness(_unitStatus.MoveProactivenessCost));
        }

        /// <summary>
        /// 指定された座標が現在位置に隣接しているか判定します。
        /// </summary>
        /// <param name="coordinate">
        /// 判定する座標です。
        /// </param>
        /// <returns>
        /// 現在位置に隣接している場合は<c>true</c>です。
        /// </returns>
        private bool IsAdjacentCoordinate(
            Vector2Int coordinate)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null)
            {
                return false;
            }

            foreach (Vector2Int adjacentCoordinate in
                    boardManager.GetAdjacentHexCellCoordinates(
                        _currentCoordinate.x,
                        _currentCoordinate.y))
            {
                if (adjacentCoordinate ==
                    coordinate)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 2つの座標間の高さ差が移動制限内か判定します。
        /// </summary>
        /// <param name="from">
        /// 移動元座標です。
        /// </param>
        /// <param name="to">
        /// 移動先座標です。
        /// </param>
        /// <returns>
        /// 移動可能な高さ差の場合は<c>true</c>です。
        /// </returns>
        private bool IsStepMovable(
            Vector2Int from,
            Vector2Int to)
        {
            BoardManager boardManager =
                BoardManager.Instance;

            if (boardManager == null ||
                !boardManager.ContainsHexCell(
                    from.x,
                    from.y) ||
                !boardManager.ContainsHexCell(
                    to.x,
                    to.y))
            {
                return false;
            }

            HexCell fromHexCell =
                boardManager.GetMacroPhaseHexCell(
                    from.x,
                    from.y);

            HexCell toHexCell =
                boardManager.GetMacroPhaseHexCell(
                    to.x,
                    to.y);

            if (fromHexCell == null ||
                toHexCell == null)
            {
                return false;
            }

            int heightDifference =
                toHexCell.GetFloor() -
                fromHexCell.GetFloor();

            return heightDifference <= 0 ||
                heightDifference <=
                _maxStepHeight;
        }

        /// <summary>
        /// 現在のユニットがローカルプレイヤー所有か判定します。
        /// </summary>
        private bool IsLocalPlayerUnit()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            if (unitManager == null ||
                _unitStatus == null)
            {
                return false;
            }

            return _unitStatus.PlayerId ==
                unitManager.ControlledUnitId;
        }

        /// <summary>
        /// 現在のユニットがAI操作か判定します。
        /// </summary>
        private bool IsAiControlledUnit()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                _unitStatus != null &&
                unitManager.IsAIControlledUnit(
                    _unitStatus.PlayerId);
        }

        /// <summary>
        /// 現在のAIユニットで移動処理が有効か判定します。
        /// </summary>
        private bool IsAiMoveEnabled()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                _unitStatus != null &&
                unitManager.IsAIMoveEnabled(
                    _unitStatus.PlayerId);
        }

        /// <summary>
        /// このクライアントでユニット処理を実行する権限があるか判定します。
        /// </summary>
        private bool CanExecuteMoveLogic()
        {
            UnitManager unitManager =
                UnitManager.Instance;

            return unitManager != null &&
                _unitStatus != null &&
                unitManager.CanExecuteUnitLogic(
                    _unitStatus.PlayerId);
        }

        /// <summary>
        /// 移動設定データを読み込みます。
        /// </summary>
        /// <returns>
        /// 設定データを読み込めた場合は<c>true</c>です。
        /// </returns>
        private bool TryLoadMovementSettings()
        {
            if (_isMovementSettingsLoaded)
            {
                return true;
            }

            GameDataRepository repository =
                GameDataRepository.Instance;

            if (repository == null ||
                !repository.IsLoaded)
            {
                return false;
            }

            UnitData unitData =
                repository.GetUnitData();

            if (unitData == null)
            {
                return false;
            }

            _maxStepHeight =
                Mathf.Max(
                    0,
                    unitData.MaxClimbStep);

            _macroUnitSpeed = Mathf.Max(0.0f, unitData.MacroUnitSpeed);
            _macroAiUnitSpeed = Mathf.Max(0.0f, unitData.MacroAiUnitSpeed);

            _isMovementSettingsLoaded =
                true;

            return true;
        }

        /// <summary>
        /// 選択中および実行中の移動情報を消去します。
        /// </summary>
        private void ClearMoveSelection()
        {
            _nextCoordinate =
                default;

            _towardPosition =
                default;

            _hasSelectedCoordinate =
                false;

            _isMoveRequestPending =
                false;

            _hasMoveTarget =
                false;
        }

        /// <summary>
        /// まだ開始していない移動予約だけを解除します。
        /// </summary>
        private void ClearPendingMoveSelection()
        {
            if (_hasMoveTarget)
            {
                return;
            }

            _nextCoordinate =
                default;

            _hasSelectedCoordinate =
                false;

            _isMoveRequestPending =
                false;
        }

        /// <summary>
        /// 座標間の単純な二乗距離を計算します。
        /// </summary>
        private static float CalculateSquaredCoordinateDistance(
            Vector2Int first,
            Vector2Int second)
        {
            int xDifference =
                first.x -
                second.x;

            int yDifference =
                first.y -
                second.y;

            return xDifference *
                xDifference +
                yDifference *
                yDifference;
        }

        /// <summary>
        /// 敵座標を取得できない場合に使用する、
        /// 再現性のある座標順の評価値を計算します。
        /// </summary>
        private static float CalculateStableCoordinateOrder(
            Vector2Int coordinate)
        {
            return coordinate.x *
                k_StableCoordinateOrderMultiplier +
                coordinate.y;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspectorに設定されたAI判断間隔を有効範囲へ補正します。
        /// </summary>
        private void OnValidate()
        {
            _aiMoveDecisionInterval =
                Mathf.Max(
                    0.05f,
                    _aiMoveDecisionInterval);
        }
#endif
    }
}
