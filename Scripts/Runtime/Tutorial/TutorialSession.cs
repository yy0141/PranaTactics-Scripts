using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

using PranaTactics.AI.Solo;
using PranaTactics.Controls;
using PranaTactics.Match.Phases;
using PranaTactics.Settings;
using PranaTactics.UI.Gameplay;
using PranaTactics.UI.Screens;
using PranaTactics.Unit;
using PranaTactics.Pranam;

namespace PranaTactics.Tutorial
{
    /// <summary>
    /// 専用シーンの訓練コンテキスト。UIと実際のユニットを各課題Stateへ提供します。
    /// </summary>
    public sealed class TutorialSession : MonoBehaviour
    {
        public static TutorialSession Instance
        {
            get; private set;
        }

        public static bool BlocksInput => Instance != null &&
            (!Instance._isReady ||
            (Instance._isExplaining &&
            Instance.CurrentPage.waitFor == "confirm") ||
            Instance._isMenuOpen ||
            Instance._isDocumentOpen ||
            Time.frameCount <= Instance._blockInputFrame);

        private readonly TutorialStateMachine _machine = new TutorialStateMachine();

        private TutorialView _view;

        private UnitBehavior _player;

        private UnitBehavior _target;

        private Vector2Int _start;

        private Vector2Int _targetStart;

        private bool _isReady;

        private bool _isMenuOpen;

        private bool _isObjectiveComplete;

        private bool _isDocumentOpen;

        private readonly bool[] _completedLessons = new bool[(int)TutorialStep.Complete - 1];

        public bool IsMainPerspective => _player.GetMicroUnitBehavior().IsMainPranamPerspective;

        public bool IsMapOpen => _player.GetTacticalMapUnitBehavior().IsMapMode;

        public void CloseMap()
        {
            if (_player != null)
            {
                _player.GetTacticalMapUnitBehavior().SetMapMode(false);
            }
        }

        public bool IsPerspectiveTransitioning => _player.GetMicroUnitBehavior().IsPerspectiveTransitioning;

        public int MainWeaponSlot => _player.GetMicroUnitBehavior().MainPranamSlotIndex;

        public int SubWeaponSlot => _player.GetMicroUnitBehavior().SubPranamSlotIndex;

        private const int k_PlanningActionPoints = 10;

        private const int k_CombatActionPoints = 20;

        private ITutorialInput _input;

        private readonly TutorialConfirmation _confirmation = new TutorialConfirmation();

        private readonly TutorialConfirmation _pageConfirmation = new TutorialConfirmation();

        private TutorialLessonContent _lessonContent;

        private int _explanationPage;

        private TutorialPage[] _explanationPages;

        private TutorialPage CurrentPage => _explanationPages[_explanationPage];

        private int _pageStartedFrame;

        private int _pageStartedSteps;

        private int _pageStartedPlannedSteps;

        private int _pageMainSlot;

        private int _pageSubSlot;

        private bool _isPageMainPerspective;

        private bool _hasSeenSpeedInput;

        private readonly HashSet<KeyCode> _pagePressedKeys = new HashSet<KeyCode>();

        private KeyCode[] _pageRequiredKeys = Array.Empty<KeyCode>();

        private PranamManager _pranamManager;

        private int _pagePrimaryUses;

        private int _pageCameraZoomOutInputs;

        private int _pageCameraZoomInInputs;
        private int _pageCameraPanInputs;
        private int _pageCameraResetInputs;
        private int _pageMacroHudChanges;
        private int _pageTargetConfirmations;
        private GameplayUiView _gameplayView;
        private MacroPlanningUiView _planningView;

        private int _pageSecondaryUses;

        private bool _hasClickedBlockedClimb;

        private bool _hasPerformedPlanningAction;

        private PranaTactics.Board.HexCell _planningGuideCell;

        /// <summary>
        /// 段差実習中は指定マスだけを移動先として案内し、他の課題では自由な設計を許可します。
        /// </summary>
        public static bool AllowsPlanningTarget(MacroUnitBehavior unit, Vector2Int coordinate)
        {
            TutorialSession session = Instance;

            if (session == null || !session._isExplaining || session._player == null ||
                session._player.GetMacroUnitBehavior() != unit)
            {
                return true;
            }

            TutorialPage page = session.CurrentPage;

            if (page.waitFor == "blockedClimb")
            {
                return false;
            }

            if (page.waitFor == "planTarget")
            {
                return coordinate == new Vector2Int(page.targetX, page.targetY);
            }

            return page.waitFor == "plannedCells" || page.waitFor == "planningFinished";
        }

        /// <summary>
        /// 実際の移動判定で拒否された隣接マスへの上り操作を記録します。
        /// </summary>
        public static bool AllowsPlanningClick(MacroUnitBehavior unit, Vector2Int coordinate, bool canMove)
        {
            TutorialSession session = Instance;

            if (session != null && session._isExplaining && session._player != null &&
                session._player.GetMacroUnitBehavior() == unit &&
                session.CurrentPage.waitFor == "blockedClimb")
            {
                var board = new AiBoardAdapter();
                Vector2Int origin = unit.GetCurrentCoordinate();
                TutorialPage page = session.CurrentPage;

                if (!canMove && coordinate == new Vector2Int(page.targetX, page.targetY) &&
                    board.GetNeighbors(
                    origin).Contains(
                    coordinate) &&
                    board.GetFloor(
                    coordinate) - board.GetFloor(
                    origin) > unit.MaxStepHeight)
                {
                    session._hasClickedBlockedClimb = true;
                }
            }

            return AllowsPlanningTarget(unit, coordinate);
        }

        /// <summary>
        /// 案内中は要求した経路操作だけを許可し、順序外の決定やリセットによる進行不能を防ぎます。
        /// </summary>
        public static bool AllowsPlanningAction(MacroPlanningAction action)
        {
            TutorialSession session = Instance;

            if (session == null)
            {
                return true;
            }

            if (BlocksInput)
            {
                return false;
            }

            if (!session._isExplaining)
            {
                return true;
            }

            string waitFor = session.CurrentPage.waitFor;

            if (action == MacroPlanningAction.CancelConfirmation)
            {
                return true;
            }

            return waitFor switch
            {
                "routeShortest" => action == MacroPlanningAction.ShortestRoute,
                "routeBalanced" => action == MacroPlanningAction.BalancedRoute,
                "routeSafest" => action == MacroPlanningAction.SafestRoute,
                "pathUndo" => action == MacroPlanningAction.Undo,
                "pathRedo" => action == MacroPlanningAction.Redo,
                "pathReset" => action == MacroPlanningAction.Reset ||
                    action == MacroPlanningAction.AcceptConfirmation,
                "targetConfirmed" => action == MacroPlanningAction.AcceptConfirmation,
                "planningConfirmed" => action == MacroPlanningAction.Confirm ||
                    action == MacroPlanningAction.AcceptConfirmation,
                _ => false
            };
        }

        /// <summary>
        /// 有効なボタン操作をページ単位で記録します。確認画面を開いただけではリセットを完了しません。
        /// </summary>
        public static void NotifyPlanningAction(MacroPlanningAction action)
        {
            TutorialSession session = Instance;

            if (session == null || !session._isExplaining || !AllowsPlanningAction(action))
            {
                return;
            }

            if (action != MacroPlanningAction.Reset && action != MacroPlanningAction.Confirm &&
                action != MacroPlanningAction.CancelConfirmation)
            {
                session._hasPerformedPlanningAction = true;
            }
        }

        /// <summary>
        /// 操作待機中は経路だけを止め、照準・視点・兵装の操作を維持します。
        /// </summary>
        public static bool AllowsMigration(MicroUnitBehavior unit)
        {
            TutorialSession session = Instance;

            if (session == null || !session._isExplaining || session._player == null ||
                session._player.GetMicroUnitBehavior() != unit)
            {
                return true;
            }

            return session.CurrentPage.waitFor == "moveCells" &&
                session.ExecutedSteps - session._pageStartedSteps < session.CurrentPage.cells;
        }

        private bool _isExplaining;

        private bool _isExplanationUsesDocument;

        private float _explanationTimeScale;

        private bool _isPlanningActive;

        public void SetInputSource(ITutorialInput input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        private int _blockInputFrame = -1;

        private float _savedTimeScale = 1;

        private float _targetHealth;

        private string _title;

        private string _description;

        private string _objective;

        private Action<bool> _finish;

        public bool HasInspectedWeapon
        {
            get; private set;
        }

        public bool PlanningFinished => _player != null && !_player.GetUnitStatus().IsMovable &&
            MatchPhaseManager.Instance?.MacroPlanning?.IsLocalConfirmed == true;

        public int PlannedSteps => _player != null ? _player.GetUnitStatus().MigrationPath.Count : 0;

        public int ExecutedSteps => _player != null ? _player.GetUnitStatus().CompletedMigrationStepCount : 0;

        public bool TargetWasHit => _target != null &&
            _target.GetUnitStatus().CurrentHealth < _targetHealth - 0.01f;

        public bool IsFiring
        {
            get
            {
                UnitInputState input = InputManager.Instance != null ? InputManager.Instance.CurrentState : default;

                return input.IsPrimaryPressed || input.IsPrimaryHeld || input.IsSecondaryPressed ||
                    input.IsSecondaryHeld;
            }
        }

        public void ResetHitObservation()
        {
            _targetHealth = _target.GetUnitStatus().CurrentHealth;
        }

        public void ResetWeaponInspection()
        {
            HasInspectedWeapon = false;
        }

        public bool IsWalkingOrStopping
        {
            get
            {
                UnitInputState input = InputManager.Instance != null ? InputManager.Instance.CurrentState : default;

                return input.IsWalking || input.IsStopping;
            }
        }

        private void Awake()
        {
            Instance = this;
            _input = new TutorialPlayerInput(TutorialUiResources.Current.InputBindings);
        }

        public void Initialize(UnitBehavior player, UnitBehavior target, Action<bool> finish)
        {
            _player = player;
            _pranamManager = player.GetComponentInChildren<PranamManager>(true);
            _target = target;
            _finish = finish;
            _start = player.GetMicroUnitBehavior().GetCurrentCoordinate();
            _targetStart = target.GetMicroUnitBehavior().GetCurrentCoordinate();
            _target.GetUnitStatus().SetMaxHealth(1000000, true);
            _view = TutorialView.Create(transform, TutorialUiResources.Font);
            _view.Retry += () => GoTo(_machine.CurrentState.Step);
            _view.Previous += () => GoTo((TutorialStep)Mathf.Max(0, (int)_machine.CurrentState.Step - 1));
            _view.Plan += () =>
            {
                if (_machine.CurrentState.Step == TutorialStep.Plan)
                {
                    GoTo(TutorialStep.Plan);

                    return;
                }

                BeginPlanning();
                RefreshDescription();
                SetMenu(false);
            };
            _view.Leave += () => _finish?.Invoke(true);
            HUDManager.Instance?.StopTimer();
            HUDManager.Instance?.SetHealthGauge(
                player.GetUnitStatus().CurrentHealth / player.GetUnitStatus().MaxHealth,
                false);
            _isReady = true;
            GoTo(TutorialStep.Briefing);
        }

        private void Update()
        {
            if (!_isReady)
            {
                return;
            }

            TutorialInputState input = _input.Read();

            if (input.MenuPressed)
            {
                if (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen)
                {
                    SettingsUIManager.Instance.Close();
                }

                CloseDocument();
                SetMenu(!_isMenuOpen);

                if (_isMenuOpen)
                {
                    RefreshDescription();
                }
            }

            if (input.DocumentPressed)
            {
                if (SettingsUIManager.Instance != null && SettingsUIManager.Instance.IsOpen)
                {
                    return;
                }

                if (_isDocumentOpen)
                {
                    CloseDocument();
                }
                else
                {
                    SetMenu(false);
                    InspectWeapons();
                }
            }

            if (_isMenuOpen || (_isDocumentOpen && !(_isExplaining && _isExplanationUsesDocument)) ||
                !Application.isFocused ||
                (SettingsUIManager.Instance != null &&
                SettingsUIManager.Instance.IsOpen))
            {
                return;
            }

            if (_player == null || _target == null)
            {
                SetMenu(true);
                _view.Loading("訓練ユニットが失われました。ロビーから訓練をやり直してください。");

                return;
            }

            if (_isExplaining)
            {
                if (CurrentPage.waitFor == "confirm")
                {
                    if (_pageConfirmation.Accept(
                        Time.frameCount,
                        input.ConfirmPressed,
                        input.ConfirmHeld,
                        Time.frameCount <= _blockInputFrame))
                    {
                        AdvanceExplanation();
                    }
                }
                else if (Time.frameCount > _pageStartedFrame && Time.frameCount > _blockInputFrame &&
                    PageConditionMet())
                {
                    AdvanceExplanation();
                }

                return;
            }

            _machine.UpdateState();

            if (_isPlanningActive && MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro)
            {
                if (PlanningFinished)
                {
                    _blockInputFrame = Time.frameCount;

                    if (_machine.CurrentState.Step == TutorialStep.Plan)
                    {
                        Continue();
                    }
                    else
                    {
                        BeginCombat();
                        RefreshDescription();
                    }
                }

                return;
            }

            if (_isObjectiveComplete &&
                _confirmation.Accept(
                Time.frameCount,
                input.ConfirmPressed,
                input.ConfirmHeld,
                Time.frameCount <= _blockInputFrame))
            {
                _blockInputFrame = Time.frameCount;
                Continue();
            }
        }

        private void LateUpdate()
        {
            if (_isMenuOpen && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void GoTo(TutorialStep step)
        {
            CloseDocument();
            SetMenu(false);
            EndExplanation();
            TutorialLessonState next;

            switch (step)
            {
                case TutorialStep.Briefing:
                    next = new TutorialBriefingState(this);
                    break;
                case TutorialStep.Plan:
                    next = new TutorialPlanState(this);
                    break;
                case TutorialStep.Move:
                    next = new TutorialMoveState(this);
                    break;
                case TutorialStep.Perspective:
                    next = new TutorialPerspectiveState(this);
                    break;
                case TutorialStep.Map:
                    next = new TutorialMapState(this);
                    break;
                case TutorialStep.Weapons:
                    next = new TutorialWeaponsState(this);
                    break;

                case TutorialStep.Aim:
                    next = new TutorialAimState(this);
                    break;
                case TutorialStep.Exercise:
                    next = new TutorialExerciseState(this);
                    break;
                default:
                    next = new TutorialCompleteState(this);
                    break;
            }

            CloseDocument();
            _isObjectiveComplete = false;
            _isPlanningActive = false;

            if (step > TutorialStep.Briefing && step < TutorialStep.Complete)
            {
                _completedLessons[(int)step - 1] = false;
            }

            _machine.ChangeState(next);
            _view.SetProgress(step, _completedLessons);
            SetMenu(false);
        }

        public void PresentLesson(TutorialStep step)
        {
            if (step == TutorialStep.Perspective)
            {
                _player.GetMicroUnitBehavior().ResetToMainPerspective();
            }

            _lessonContent = TutorialLessonContent.Load(step);

            if (step == TutorialStep.Plan && _lessonContent.planningStartX > 0 &&
                _lessonContent.planningStartY > 0)
            {
                _player.TeleportMoveToCoordinate(_lessonContent.planningStartX, _lessonContent.planningStartY);
                BeginPlanning();
            }

            _explanationPages = _lessonContent.GetPages(SettingsManager.Current.HoldMovementSpeed);
            _isExplanationUsesDocument = Array.Exists(
                _explanationPages,
                page => page.waitFor == "documentOpen" ||
                page.waitFor == "documentClosed");
            int movementCells = 0;

            foreach (TutorialPage page in _explanationPages)
            {
                if (page.waitFor == "moveCells")
                {
                    movementCells += page.cells;
                }
            }

            if (movementCells > 0)
            {
                EnsureMovementRoute(movementCells);
                var route = new List<Vector2Int>(_player.GetUnitStatus().MigrationPath);

                if (route.Count > movementCells)
                {
                    route.RemoveRange(movementCells, route.Count - movementCells);
                }

                _player.GetUnitStatus().SetMigrationPath(route);
                _player.GetUnitStatus().BeginMigrationExecution();
            }

            _title = _lessonContent.title;
            _description = "";
            _objective = _lessonContent.objective;
            _explanationPage = 0;
            _isExplaining = true;
            _explanationTimeScale = Time.timeScale;
            Time.timeScale = 0;
            ShowExplanationPage();
        }

        /// <remarks>
        /// <para>空欄・省略は表示中の説明を引き継ぎ、待機条件だけを切り替えます。</para>
        /// </remarks>
        private void ShowExplanationPage()
        {
            ClearPlanningGuide();

            if (!string.IsNullOrWhiteSpace(CurrentPage.text))
            {
                _description = CurrentPage.text;
            }

            _pageStartedFrame = Time.frameCount;
            MacroCameraZoom cameraZoom = _player.GetMacroUnitBehavior().GetComponent<MacroCameraZoom>();

            _pageCameraZoomOutInputs = cameraZoom != null ? cameraZoom.ZoomOutInputCount : 0;
            _pageCameraZoomInInputs = cameraZoom != null ? cameraZoom.ZoomInInputCount : 0;
            _pageCameraPanInputs = cameraZoom != null ? cameraZoom.PanInputCount : 0;
            _pageCameraResetInputs = cameraZoom != null ? cameraZoom.ViewResetInputCount : 0;
            _gameplayView = FindObjectOfType<GameplayUiView>();
            _planningView = FindObjectOfType<MacroPlanningUiView>();
            _pageMacroHudChanges = _gameplayView != null ? _gameplayView.MacroHudChangeCount : 0;
            _pageTargetConfirmations = _planningView != null ? _planningView.TargetConfirmationCount : 0;
            _hasClickedBlockedClimb = false;
            _hasPerformedPlanningAction = false;
            _hasSeenSpeedInput = false;
            _pagePressedKeys.Clear();
            _pageRequiredKeys = CurrentPage.waitFor == "keysAll" ? Array.ConvertAll(
                CurrentPage.keys,
                key => (KeyCode)Enum.Parse(
                typeof(KeyCode),
                key)) : Array.Empty<KeyCode>();
            _pagePrimaryUses = _pranamManager?.MainPranamBehavior?.UseCount ?? 0;
            _pageSecondaryUses = _pranamManager?.SubPranamBehavior?.UseCount ?? 0;
            _pageStartedSteps = ExecutedSteps;
            _pageStartedPlannedSteps = PlannedSteps;
            _isPageMainPerspective = IsMainPerspective;
            _pageMainSlot = MainWeaponSlot;
            _pageSubSlot = SubWeaponSlot;
            SetExplanationTimeScale(CurrentPage.waitFor == "confirm" ? 0 : _explanationTimeScale);

            if (CurrentPage.waitFor == "moveCells")
            {
                EnsureMovementRoute(CurrentPage.cells);
            }

            _pageConfirmation.Arm(Time.frameCount);

            if (_isPlanningActive)
            {
                _player.GetMacroUnitBehavior().RefreshRoutePreview();
            }

            if (CurrentPage.waitFor == "targetCandidate")
            {
                _target.TeleportMoveToCoordinate(CurrentPage.targetX, CurrentPage.targetY);
            }

            if (CurrentPage.waitFor == "targetCandidate" || CurrentPage.waitFor == "blockedClimb" || CurrentPage.waitFor == "planTarget")
            {
                _planningGuideCell = PranaTactics.Board.BoardManager.Instance.GetMacroPhaseHexCell(
                    CurrentPage.targetX,
                    CurrentPage.targetY);
                TutorialTheme theme = TutorialUiResources.Current.Theme;

                _planningGuideCell?.ShowGuideOutline(CurrentPage.waitFor == "blockedClimb"
                    ? theme.BlockedClimbOutlineColor : new Color(0.2f, 0.85f, 0.65f), theme.BlockedClimbOutlineWidth);
            }

            RefreshDescription();
        }

        private bool PageConditionMet()
        {
            foreach (KeyCode key in _pageRequiredKeys)
            {
                if (_input.IsKeyPressed(key))
                {
                    _pagePressedKeys.Add(key);
                }
            }

            if (CurrentPage.waitFor == "walking")
            {
                _hasSeenSpeedInput |= _input.IsKeyPressed(KeyCode.LeftShift);
            }

            if (CurrentPage.waitFor == "stopping")
            {
                _hasSeenSpeedInput |= _input.IsKeyPressed(KeyCode.LeftControl);
            }

            if (_pageMainSlot < 0)
            {
                _pageMainSlot = MainWeaponSlot;
            }

            if (_pageSubSlot < 0)
            {
                _pageSubSlot = SubWeaponSlot;
            }

            UnitInputState input = InputManager.Instance.CurrentState;
            MacroCameraZoom cameraZoom = _player.GetMacroUnitBehavior().GetComponent<MacroCameraZoom>();
            var before = new TutorialPageObservation
            {
                CameraPanInputs = _pageCameraPanInputs,
                CameraResetInputs = _pageCameraResetInputs,
                MacroHudChanges = _pageMacroHudChanges,
                TargetConfirmations = _pageTargetConfirmations,
                CameraZoomOutInputs = _pageCameraZoomOutInputs,
                CameraZoomInInputs = _pageCameraZoomInInputs,
                Steps = _pageStartedSteps,
                MainSlot = _pageMainSlot,
                SubSlot = _pageSubSlot,
                PlannedSteps = _pageStartedPlannedSteps,
                PrimaryUses = _pagePrimaryUses,
                SecondaryUses = _pageSecondaryUses,
                MainPerspective = _isPageMainPerspective
            };
            var now = new TutorialPageObservation
            {
                CameraPanInputs = cameraZoom != null ? cameraZoom.PanInputCount : 0,
                CameraResetInputs = cameraZoom != null ? cameraZoom.ViewResetInputCount : 0,
                MacroHudChanges = _gameplayView != null ? _gameplayView.MacroHudChangeCount : 0,
                MacroHudHidden = _gameplayView != null && _gameplayView.IsMacroHudHidden,
                HasTargetCandidate = _planningView != null && _planningView.IsConfirmationOpen &&
                    _planningView.PendingTargetPlayerId == _target.PlayerId,
                TargetConfirmations = _planningView != null ? _planningView.TargetConfirmationCount : 0,
                IsExpectedTargetConfirmed = SoloAiDirector.EnsureExists().OrderedTargetPlayerId == _target.PlayerId,
                CameraZoomOutInputs = cameraZoom != null ? cameraZoom.ZoomOutInputCount : 0,
                CameraZoomInInputs = cameraZoom != null ? cameraZoom.ZoomInInputCount : 0,
                HasClickedBlockedClimb = _hasClickedBlockedClimb,
                HasPerformedPlanningAction = _hasPerformedPlanningAction,
                IsPlanningIdle = _player.GetMacroUnitBehavior().CanEditPath,
                IsAtPlanningTarget = _player.GetMacroUnitBehavior().GetCurrentCoordinate() == new Vector2Int(
                    CurrentPage.targetX,
                    CurrentPage.targetY),
                Steps = ExecutedSteps,
                Moving = _player.GetUnitStatus().IsMovable,
                PlannedSteps = PlannedSteps,
                PlanningFinished = PlanningFinished,
                DocumentOpen = _isDocumentOpen,
                RequiredKeyCount = _pageRequiredKeys.Length,
                PressedKeyCount = _pagePressedKeys.Count,
                PrimaryUses = _pranamManager?.MainPranamBehavior?.UseCount ?? 0,
                SecondaryUses = _pranamManager?.SubPranamBehavior?.UseCount ?? 0,
                PrimaryBusy = _pranamManager?.MainPranamBehavior?.IsUseInProgress ?? false,
                SecondaryBusy = _pranamManager?.SubPranamBehavior?.IsUseInProgress ?? false,
                MainSlot = MainWeaponSlot,
                SubSlot = SubWeaponSlot,
                MapOpen = IsMapOpen,
                MainPerspective = IsMainPerspective,
                Turning = IsPerspectiveTransitioning,
                Walking = input.IsWalking,
                Stopping = input.IsStopping,
                KeyPressed = _hasSeenSpeedInput ||
                    (CurrentPage.waitFor == "key" &&
                    _input.IsKeyPressed(
                    (KeyCode)Enum.Parse(
                    typeof(KeyCode),
                    CurrentPage.key)))
            };

            return TutorialPageCondition.IsSatisfied(CurrentPage.waitFor, CurrentPage.cells, before, now);
        }

        /// <remarks>
        /// <para>元の計画を優先し、不足分だけ到達可能な隣接マスで補います。再挑戦にも対応。</para>
        /// <para>SetMigrationPathは実行マス数をリセットするため、必要な場合だけ更新します。</para>
        /// </remarks>
        private void EnsureMovementRoute(int cells)
        {
            UnitStatus status = _player.GetUnitStatus();
            var path = new List<Vector2Int>(status.MigrationPath);
            var board = new AiBoardAdapter();
            Vector2Int tail = path.Count > 0 ? path[path.Count - 1] : _player.GetMicroUnitBehavior().GetCurrentCoordinate();

            while (path.Count < cells)
            {
                bool found = false;

                foreach (Vector2Int neighbor in board.GetNeighbors(tail))
                {
                    if (!board.Contains(neighbor) || board.GetFloor(neighbor) != board.GetFloor(tail) ||
                        neighbor == _target.GetMicroUnitBehavior().GetCurrentCoordinate())
                    {
                        continue;
                    }

                    path.Add(neighbor);
                    tail = neighbor;
                    found = true;
                    break;
                }

                if (!found)
                {
                    throw new InvalidOperationException("移動訓練に必要な隣接経路がありません。");
                }
            }

            if (path.Count != status.MigrationPath.Count)
            {
                status.SetMigrationPath(path);
                status.BeginMigrationExecution();
                _pageStartedSteps = ExecutedSteps;
            }
        }

        private void AdvanceExplanation()
        {
            _blockInputFrame = Time.frameCount;

            if (++_explanationPage < _explanationPages.Length)
            {
                ShowExplanationPage();

                return;
            }

            EndExplanation();
            _description = "";
            TutorialStep step = _machine.CurrentState.Step;

            if (step == TutorialStep.Briefing || step == TutorialStep.Complete)
            {
                MarkObjectiveComplete();
                Continue();
            }
            else
            {
                RefreshDescription();
            }

            if (_lessonContent.completeOnSequenceEnd && step != TutorialStep.Briefing &&
                step != TutorialStep.Complete)
            {
                MarkObjectiveComplete();
            }
        }

        private void EndExplanation()
        {
            ClearPlanningGuide();

            if (!_isExplaining)
            {
                return;
            }

            _isExplaining = false;
            SetExplanationTimeScale(_explanationTimeScale);
        }

        /// <summary>
        /// ページ終了や課題の再開始時に対象マスの強調表示を解除します。
        /// </summary>
        private void ClearPlanningGuide()
        {
            if (_planningGuideCell != null)
            {
                _planningGuideCell.ClearGuideOutline();
            }

            _planningGuideCell = null;
        }

        /// <remarks>
        /// <para>資料中も説明は進めますが、ゲームの停止は資料を閉じるまで維持します。</para>
        /// </remarks>
        private void SetExplanationTimeScale(float value)
        {
            if (_isDocumentOpen)
            {
                _savedTimeScale = value;
            }

            Time.timeScale = _isDocumentOpen ? 0 : value;
        }

        /// <remarks>
        /// <para>再挑戦時に経路を使い切っていても、その場で引き直せます。</para>
        /// </remarks>
        private void RefreshDescription()
        {
            TutorialStep step = _machine.CurrentState.Step;
            PranaTactics.Settings.ControlsSettings controls = PranaTactics.Settings.SettingsManager.Current.Controls;
            string description = (_description ?? "")
                .Replace("{walkAction}", controls.HoldWalk ? "押し続けて" : "押して")
                .Replace("{stopAction}", controls.HoldStop ? "押し続けて" : "押して")
                .Replace("{walkKey}", PranaTactics.Settings.KeyboardBinding.Display(controls.WalkKey))
                .Replace("{stopKey}", PranaTactics.Settings.KeyboardBinding.Display(controls.StopKey))
                .Replace("{mapKey}", PranaTactics.Settings.KeyboardBinding.Display(controls.MapKey))
                .Replace("{macroViewResetKey}", PranaTactics.Settings.KeyboardBinding.Display(controls.MacroViewResetKey));
            bool canPlan = step == TutorialStep.Move || step == TutorialStep.Aim ||
                step == TutorialStep.Exercise;

            if (_isExplaining)
            {
                string hint = _explanationPage < _explanationPages.Length - 1 ? "次の説明へ" : step == TutorialStep.Complete ? "ロビーへ戻る" : "操作を始める";

                _view.Present(_title, description, "", false);

                if (CurrentPage.waitFor == "confirm")
                {
                    _view.ShowExplanationHint(hint);
                }
                else
                {
                    _view.HideContinueHint();
                }

                return;
            }

            _view.Present(_title, description, _objective, canPlan);
            _view.ShowExerciseChecklist(step == TutorialStep.Exercise);

            if (_isObjectiveComplete && step != TutorialStep.Briefing)
            {
                _view.Complete(step == TutorialStep.Complete);
            }

            if (_isObjectiveComplete)
            {
                _view.ShowContinueHint(step);
            }

        }

        public void MarkObjectiveComplete()
        {
            if (_isExplaining || _isObjectiveComplete)
            {
                return;
            }

            _isObjectiveComplete = true;
            TutorialStep step = _machine.CurrentState.Step;

            if (step > TutorialStep.Briefing && step < TutorialStep.Complete)
            {
                _completedLessons[(int)step - 1] = true;
            }

            _view.SetProgress(step, _completedLessons);
            _confirmation.Arm(Time.frameCount);

            if (step != TutorialStep.Briefing)
            {
                _view.Complete(step == TutorialStep.Complete);
            }

            _view.ShowContinueHint(step);
        }

        public void SetExerciseChecklist(bool planned, bool moved, bool hit)
        {
            if (_isExplaining)
            {
                return;
            }

            _view.SetExerciseChecklist(planned, moved, hit);
        }

        private void Continue()
        {
            if (!_isObjectiveComplete)
            {
                SetMenu(false);

                return;
            }

            if (_machine.CurrentState.Step == TutorialStep.Complete)
            {
                _finish?.Invoke(false);

                return;
            }

            GoTo(_machine.CurrentState.Step + 1);
        }

        public void ResetArena()
        {
            _player.TeleportMoveToCoordinate(_start.x, _start.y);
            _target.TeleportMoveToCoordinate(_targetStart.x, _targetStart.y);
            _player.GetUnitStatus().SetHealth(_player.GetUnitStatus().MaxHealth);
            _target.GetUnitStatus().SetHealth(_target.GetUnitStatus().MaxHealth);
            _player.GetUnitStatus().SetMigrationPath(null);
            _targetHealth = _target.GetUnitStatus().CurrentHealth;
        }

        public void BeginPlanning()
        {
            _isPlanningActive = true;
            _isObjectiveComplete = false;

            if (_player == null)
            {
                return;
            }

            Vector2Int coordinate = _player.GetMicroUnitBehavior().GetCurrentCoordinate();

            _player.TeleportMoveToCoordinate(coordinate.x, coordinate.y);
            _player.GetUnitStatus().SetMigrationPath(null);
            _player.GetMicroUnitBehavior().PrepareForMicroPhase();
            _player.GetUnitStatus().SetMoveProactivenessCost(1);
            _player.GetUnitStatus().SetActionPoint(k_PlanningActionPoints);
            _player.GetMacroUnitBehavior().SetProactiveness(k_PlanningActionPoints);
            _player.GetMacroUnitBehavior().PrepareForMacroPlanning();
            MatchPhaseManager.Instance.MacroPlanning = new MacroPlanningSession(null, 0);
            MatchPhaseManager.Instance.ChangePhase(MatchPhase.Macro);
        }

        public void BeginCombat()
        {
            _isPlanningActive = false;

            if (_player == null)
            {
                return;
            }

            Vector2Int coordinate = _player.GetMicroUnitBehavior().GetCurrentCoordinate();

            _player.TeleportMoveToCoordinate(coordinate.x, coordinate.y);
            _player.GetMicroUnitBehavior().PrepareForMicroPhase();
            _player.GetUnitStatus().SetActionPoint(k_CombatActionPoints);
            _player.GetMicroUnitBehavior().SetProactiveness(k_CombatActionPoints);
            MatchPhaseManager.Instance.MacroPlanning = null;
            MatchPhaseManager.Instance.ChangePhase(MatchPhase.Micro);
        }

        /// <summary>
        /// 実装済みの兵装射程に合わせ、同じ高さの標的を置きます。
        /// </summary>
        /// <remarks>
        /// <para>視点と独立している攻撃範囲も、標的の方向へ揃えます。</para>
        /// </remarks>
        public void PrepareTarget()
        {
            _player.GetUnitStatus().SetHealth(_player.GetUnitStatus().MaxHealth);
            _target.GetUnitStatus().SetHealth(_target.GetUnitStatus().MaxHealth);
            _targetHealth = _target.GetUnitStatus().CurrentHealth;
            var board = new AiBoardAdapter();
            Vector2Int origin = _player.GetMicroUnitBehavior().GetCurrentCoordinate();
            float spacing = board.GetCellSpacing(origin);
            List<AiWeaponProfile> profiles = new AiWeaponSelector().BuildProfiles(_player, true, spacing);

            profiles.AddRange(new AiWeaponSelector().BuildProfiles(_player, false, spacing));
            AiWeaponProfile weapon = profiles.Find(p => p.IsUsable && p.UseMode == AiWeaponUseMode.Attack);

            if (weapon == null)
            {
                return;
            }

            float desired = Mathf.Lerp(weapon.MinimumRange, weapon.MaximumRange, 0.5f);
            float bestDistance = float.MaxValue;
            Vector2Int best = _targetStart;
            var visited = new HashSet<Vector2Int>
            {
                origin
            };
            var queue = new Queue<Vector2Int>();

            queue.Enqueue(origin);

            while (queue.Count > 0 && visited.Count < 800)
            {
                Vector2Int current = queue.Dequeue();

                foreach (Vector2Int cell in board.GetNeighbors(current))
                {
                    if (!board.Contains(cell) || !visited.Add(cell))
                    {
                        continue;
                    }

                    queue.Enqueue(cell);

                    if (board.GetFloor(cell) != board.GetFloor(origin))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(
                        board.GetWorldPosition(origin),
                        board.GetWorldPosition(
                        cell));

                    if (distance < weapon.MinimumRange || distance > weapon.MaximumRange)
                    {
                        continue;
                    }

                    if (Mathf.Abs(distance - desired) >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = Mathf.Abs(distance - desired);
                    best = cell;
                }
            }

            _target.TeleportMoveToCoordinate(best.x, best.y);
            Vector3 direction = board.GetWorldPosition(best) - board.GetWorldPosition(origin);
            MicroUnitBehavior micro = _player.GetMicroUnitBehavior();

            micro.AlignPerspectiveAndPranamYaw(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
            micro.SetPitch(0.0f);
        }

        private void InspectWeapons()
        {
            var board = new AiBoardAdapter();
            float spacing = Mathf.Max(
                0.1f,
                board.GetCellSpacing(
                _player.GetMicroUnitBehavior().GetCurrentCoordinate()));
            var selector = new AiWeaponSelector();
            List<AiWeaponProfile> profiles = selector.BuildProfiles(_player, true, spacing);

            profiles.AddRange(selector.BuildProfiles(_player, false, spacing));
            var text = new StringBuilder("装備中の兵装資料\n\n");

            foreach (AiWeaponProfile profile in profiles)
            {
                if (!profile.IsUsable)
                {
                    continue;
                }

                text.AppendLine(
                    $"{(profile.IsMain ? "主" : "副")}{profile.Slot + 1}  {profile.DisplayName}：射程 {profile.MinimumRange / spacing:0.#}〜{profile.MaximumRange / spacing:0.#}マス / 消費 {profile.PranaCost:0.#}");
            }

            text.Append("\n射程に加え、遮蔽物と狙う方向を確認しましょう。{document}で資料を閉じます。");
            _isDocumentOpen = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0;
            _view.ShowDocument(TutorialUiResources.Current.InputBindings.Format(text.ToString()));
            HasInspectedWeapon = true;
        }

        private void CloseDocument()
        {
            if (!_isDocumentOpen)
            {
                return;
            }

            _isDocumentOpen = false;
            Time.timeScale = _savedTimeScale;
            _blockInputFrame = Time.frameCount;
            _view.HideDocument();
        }

        private void SetMenu(bool open)
        {
            if (_isMenuOpen != open)
            {
                if (open)
                {
                    _savedTimeScale = Time.timeScale;
                    Time.timeScale = 0;
                }
                else
                {
                    Time.timeScale = _savedTimeScale;
                }
            }

            _isMenuOpen = open;
            _blockInputFrame = Time.frameCount;
            _view.SetMenu(open);

            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                MatchPhaseManager.Instance?.RefreshCursorState();
            }
        }

        public void Stop()
        {
            _machine.Stop();

            if (_view != null)
            {
                _view.gameObject.SetActive(false);
            }

            _finish = null;

            if (_isMenuOpen || _isDocumentOpen)
            {
                Time.timeScale = _savedTimeScale;
            }

            EndExplanation();
            _isMenuOpen = false;
            _isReady = false;

            if (Instance == this)
            {
                Instance = null;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
