using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.GameFlow;
using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.UI.Gameplay;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// ソロプレイ中の全CPUをローカル権威側で管理します。
    /// AIはAiWorldSnapshotに公開された情報だけを使用します。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class SoloAiDirector : MonoBehaviour
    {
        private static SoloAiDirector s_instance;

        [Header("AI Definition")]
        [SerializeField]
        private AiDefinitionCatalog _definitionCatalog;

        [Header("Difficulty")]
        [SerializeField]
        private AiDifficulty _difficulty = AiDifficulty.Normal;

        [Header("Team Tactics")]
        [Tooltip("0の場合は対戦開始時にSeedを生成します。" + "0以外を指定すると戦術抽選を再現できます。")]
        [SerializeField]
        private int _fixedTeamTacticSeed;

        /// <summary>
        /// 1フレームで1体の経路探索へ使用する最大ノード数です。
        /// </summary>
        /// <remarks>
        /// 従来値の24では長距離経路が数秒単位で遅延するため、
        /// フレーム時間の上限と併用して探索を高速化します。
        /// </remarks>
        [Header("Macro Planning Performance")]
        [Min(32)]
        [SerializeField]
        private int _macroPlanningNodesPerFrame = 512;

        /// <summary>
        /// 1フレームで1体の経路探索へ使用する最大時間です。
        /// </summary>
        [Min(0.5f)]
        [SerializeField]
        private float _macroPlanningBudgetMilliseconds = 4.0f;

        /// <summary>
        /// AI全体の経路計画に割り当てる目標秒数です。
        /// </summary>
        [Tooltip("AI全員の計画時間です。プレイヤーのMacro Phase制限時間とは別です。")]
        [SerializeField, Range(1.0f, 38.0f)]
        private float _macroPlanningTargetSeconds = 30.0f;

        /// <summary>
        /// 設定の目標秒数が長い場合も計画に使用しない最大秒数です。
        /// </summary>
        [SerializeField, Range(1.0f, 38.0f)]
        private float _macroPlanningMaximumSeconds = 38.0f;

        /// <summary>
        /// 同一セッションの再探索でも保持する全体時間配分です。
        /// </summary>
        private AiMacroPlanningBudget _macroPlanningBudget;

        [Tooltip("経路の反映とフェーズ遷移のために残す秒数です。")]
        [Min(0.1f)]
        [SerializeField]
        private float _macroPlanningFinishMarginSeconds = 1.0f;

        /// <summary>
        /// ターゲット変更の再探索で1体あたりに確保する最低秒数です。
        /// </summary>
        [Min(0.1f)]
        [SerializeField]
        private float _macroRetargetMinimumSecondsPerAgent = 0.5f;

        /// <summary>
        /// 今回のマクロフェイズで経路が確定したAIです。
        /// </summary>
        private readonly HashSet<int> _completedMacroAgents = new HashSet<int>();

        /// <summary>
        /// 再探索時に停止する味方AIの経路表示です。
        /// </summary>
        private readonly Dictionary<int, Coroutine> _macroPathPlaybacks = new Dictionary<int, Coroutine>();

        /// <summary>
        /// 優先ターゲットと計画済み経路を所有するセッションです。
        /// </summary>
        private MacroPlanningSession _targetOrderSession;

        /// <summary>
        /// プレイヤーが指定した敵のIDです。
        /// </summary>
        private int _orderedTargetPlayerId = -1;

        /// <summary>
        /// ターゲット指定の受付結果を表示します。
        /// </summary>
        private string _targetOrderStatus = string.Empty;

        public int OrderedTargetPlayerId => _orderedTargetPlayerId;

        public string TargetOrderStatus => IsMacroPhase() &&
            _targetOrderSession == MatchPhaseManager.Instance.MacroPlanning
                ? _targetOrderStatus : string.Empty;

        private readonly Dictionary<int, SoloAiAgent> _agents = new Dictionary<int, SoloAiAgent>();

        private readonly AiWorldStateProvider _worldState = new AiWorldStateProvider();

        private readonly AiSearchPlanner _searchPlanner = new AiSearchPlanner();

        private readonly AiTeamTargetSelector _targetSelector = new AiTeamTargetSelector();

        private readonly AiTeamTacticSelector _teamTacticSelector = new AiTeamTacticSelector();

        private readonly AiCoordinatedStrikeController _coordinatedStrikeController = new AiCoordinatedStrikeController();

        private AiDifficultyProfile _difficultyProfile;

        /// <summary>
        /// 今回のステージJSONで指定した敵専用の難易度です。
        /// </summary>
        private AiDifficultyProfile _enemyDifficultyProfile;

        private bool _hasPlannedMacroPhase;

        private bool _isMacroPlanning;

        private Coroutine _macroPlanningCoroutine;

        private int _registrationVersion;

        private int _plannedRegistrationVersion;

        private int _planGenerationId;

        private int _activeTeamTacticSeed;

        private bool _hasActiveTeamTacticMatch;

        private MatchPhase _lastPhase;

        public static bool IsMacroPlanningComplete => s_instance == null ||
            s_instance._agents.Count == 0 ||
            (s_instance._lastPhase == MatchPhase.Macro && s_instance._hasPlannedMacroPhase &&
             !s_instance._isMacroPlanning && s_instance._plannedRegistrationVersion == s_instance._registrationVersion);

        public static SoloAiDirector EnsureExists()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            SoloAiDirector existing = FindObjectOfType<SoloAiDirector>();

            if (existing != null)
            {
                s_instance = existing;

                return s_instance;
            }

            GameObject gameObject = new GameObject("SoloAiDirector");

            s_instance = gameObject.AddComponent<SoloAiDirector>();
            DontDestroyOnLoad(gameObject);

            return s_instance;
        }

        public AiWorldStateProvider WorldState => _worldState;

        /// <summary>
        /// 撃破または手動操作への切り替え時にAI登録と実行中の経路計画を解除します。
        /// </summary>
        public static void Unregister(int playerId)
        {
            if (s_instance == null || !s_instance._agents.Remove(playerId))
            {
                return;
            }

            s_instance.CancelMacroPlanning();
            s_instance._planGenerationId++;
            s_instance._registrationVersion++;
            s_instance._hasPlannedMacroPhase = false;
        }

        public AiDifficulty Difficulty => _difficulty;

        public int ActiveTeamTacticSeed => _activeTeamTacticSeed;

        /// <summary>
        /// 追加のチーム戦術を登録します。
        /// 既存IDを指定した場合は、その実装を置き換えます。
        /// </summary>
        public void RegisterTeamTactic(AiTeamTacticBase tactic)
        {
            _teamTacticSelector.Register(tactic);
            _hasPlannedMacroPhase = false;
        }

        /// <summary>
        /// 新しい対戦用の戦術抽選状態を開始します。
        /// matchSeedが0の場合は新しいSeedを生成します。
        /// </summary>
        public void BeginMatchTeamTactics(int matchSeed = 0)
        {
            RefreshEnemyDifficulty();
            int configuredSeed = matchSeed != 0 ? matchSeed : _fixedTeamTacticSeed;

            _activeTeamTacticSeed = configuredSeed != 0 ? configuredSeed : Guid.NewGuid().GetHashCode();
            _teamTacticSelector.BeginMatch(_activeTeamTacticSeed);
            _hasActiveTeamTacticMatch = true;
            _coordinatedStrikeController.Reset();
            _completedMacroAgents.Clear();
            _targetOrderSession = null;
            _orderedTargetPlayerId = -1;
            _targetOrderStatus = string.Empty;
            CancelMacroPlanning();
            _hasPlannedMacroPhase = false;
            Debug.Log("SoloAI TeamTactic match started: seed=" + _activeTeamTacticSeed);
        }

        public SoloAiAgent Register(UnitBehavior unit)
        {
            if (unit == null || unit.PlayerId < 0)
            {
                return null;
            }

            RemoveDestroyedAgents();
            RefreshEnemyDifficulty();

            if (_agents.Count == 0 && !_hasActiveTeamTacticMatch)
            {
                BeginMatchTeamTactics();
            }

            int playerId = unit.PlayerId;

            if (_agents.TryGetValue(playerId, out SoloAiAgent existingAgent))
            {
                if (existingAgent != null && existingAgent.Unit == unit)
                {
                    return existingAgent;
                }

                _agents.Remove(playerId);
            }

            SoloAiAgent newAgent = new SoloAiAgent(unit, _worldState, GetDifficultyProfile(unit.GetTeamId()));

            _agents[playerId] = newAgent;
            _registrationVersion++;
            _hasPlannedMacroPhase = false;

            return newAgent;
        }

        /// <summary>
        /// 対戦開始時に敵専用設定を読み込み、味方の難易度とは分離します。
        /// </summary>
        private void RefreshEnemyDifficulty()
        {
            GameFlowManager flow = GameFlowManager.Instance;
            AiDifficultyOverrides overrides = flow != null && flow.IsSoloPlay
                ? flow.SoloBattleConfiguration?.EnemyAi : null;
            _enemyDifficultyProfile = AiDifficultyProfile.Create(_difficulty, overrides);
        }

        /// <summary>
        /// JSON編成ではチーム0を味方として、それ以外に敵専用難易度を適用します。
        /// </summary>
        private AiDifficultyProfile GetDifficultyProfile(int teamId)
        {
            return teamId > 0 && _enemyDifficultyProfile != null ? _enemyDifficultyProfile : _difficultyProfile;
        }

        /// <summary>
        /// 連携射撃の許容時間をチーム別の難易度から取得します。
        /// </summary>
        private float GetCoordinationTolerance(int teamId)
        {
            return GetDifficultyProfile(teamId).CoordinationTolerance;
        }

        public void SetDifficulty(AiDifficulty difficulty)
        {
            if (_difficulty == difficulty)
            {
                return;
            }

            _difficulty = difficulty;
            _difficultyProfile = AiDifficultyProfile.Create(difficulty);
            RefreshEnemyDifficulty();
            List<int> playerIds = new List<int>(_agents.Keys);

            foreach (int playerId in playerIds)
            {
                SoloAiAgent oldAgent = _agents[playerId];

                if (oldAgent == null || oldAgent.Unit == null)
                {
                    continue;
                }

                SoloAiAgent newAgent = new SoloAiAgent(oldAgent.Unit, _worldState, GetDifficultyProfile(oldAgent.Unit.GetTeamId()));

                newAgent.SetTarget(oldAgent.Target);
                newAgent.SetTeamTask(oldAgent.TeamTaskId);

                if (oldAgent.TacticalDirective != null)
                {
                    newAgent.ApplyTacticalDirective(oldAgent.TacticalDirective);
                }

                if (MatchPhaseManager.Instance != null &&
                    MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Micro)
                {
                    newAgent.OnMicroPhaseEntered();
                }

                _agents[playerId] = newAgent;
            }

            _registrationVersion++;
            _hasPlannedMacroPhase = false;
        }

        /// <summary>
        /// 指定したヘックスにいる観測可能な敵を確認UIの候補として取得します。
        /// </summary>
        public bool TryGetMacroTarget(Vector2Int coordinate, out UnitBehavior target)
        {
            target = null;
            if (!IsMacroPhase())
            {
                return false;
            }

            AiPublicUnitState state = AiMacroTargetOrder.FindTarget(
                _worldState.BuildSnapshot(ResolveLocalPlayerTeamId()).Enemies, coordinate);
            target = state != null ? state.Unit : null;
            return target != null;
        }

        /// <summary>
        /// 確認画面を開いた時の敵が、引き続き指定できるか確認します。
        /// </summary>
        public bool TryGetMacroTarget(int playerId, out UnitBehavior target)
        {
            target = null;
            if (!IsMacroPhase())
            {
                return false;
            }

            AiPublicUnitState state = AiMacroTargetOrder.FindTarget(
                _worldState.BuildSnapshot(ResolveLocalPlayerTeamId()).Enemies, playerId);
            target = state != null ? state.Unit : null;
            return target != null;
        }

        /// <summary>
        /// 公開された敵のいるヘックスを味方AIの優先ターゲットに指定します。
        /// </summary>
        /// <remarks>
        /// 再探索時間の検証が成功してから、味方AIだけを開始地点に戻します。
        /// 完了済みの他チームの計画とプレイヤー自身の経路は保持します。
        /// </remarks>
        /// <param name="targetPlayerId">
        /// 確認画面で確定した敵のプレイヤーIDです。
        /// </param>
        /// <returns>
        /// ターゲット指定を受理した場合は<c>true</c>です。時間不足や味方AI不在でも指定を保持します。
        /// </returns>
        public bool TryOrderMacroTarget(int targetPlayerId)
        {
            MacroPlanningSession session = MatchPhaseManager.Instance?.MacroPlanning;
            UnitManager manager = UnitManager.Instance;
            if (!IsMacroPhase() || session == null || session.IsLocalConfirmed ||
                session.IsPlanningConfirmationOpen || manager == null)
            {
                return false;
            }

            RefreshTargetOrderSession();
            int teamId = ResolveLocalPlayerTeamId();
            _worldState.Update(AiWorldStateProvider.ObservationInterval, MatchPhase.Macro);
            AiPublicUnitState target = AiMacroTargetOrder.FindTarget(
                _worldState.BuildSnapshot(teamId).Enemies, targetPlayerId);
            if (target == null)
            {
                return false;
            }

            _orderedTargetPlayerId = target.PlayerId;
            List<SoloAiAgent> allies = CollectMacroTargetOrderAgents(teamId, out int pendingOthers);
            float remainingSeconds = HUDManager.Instance != null ? HUDManager.Instance.PhaseRemainingSeconds : 0.0f;
            if (!_hasPlannedMacroPhase && _macroPlanningBudget != null)
            {
                remainingSeconds = Mathf.Min(remainingSeconds,
                    (float)_macroPlanningBudget.GetRemainingSeconds(Time.timeAsDouble) +
                    _macroPlanningFinishMarginSeconds);
            }
            if (allies.Count == 0 || !AiMacroTargetOrder.HasPlanningTime(
                    remainingSeconds, _macroPlanningFinishMarginSeconds,
                    _macroRetargetMinimumSecondsPerAgent, allies.Count + pendingOthers))
            {
                _targetOrderStatus = allies.Count == 0
                    ? "移動できる味方AIがいません"
                    : "再探索の時間不足：現在の経路を維持";
                _targetOrderStatus = $"優先ターゲット：敵 {target.PlayerId} ／ " + _targetOrderStatus;
                return true;
            }

            CancelMacroPlanning();
            foreach (SoloAiAgent agent in allies)
            {
                ResetAllyMacroPath(agent.Unit);
            }

            _targetOrderStatus = $"優先ターゲット：敵 {target.PlayerId}";
            PlanMacroPhaseImmediately(true);
            return true;
        }

        /// <summary>
        /// 再探索できる味方AIと、時間を配分する未完了の他AIを取得します。
        /// </summary>
        private List<SoloAiAgent> CollectMacroTargetOrderAgents(int teamId, out int pendingOthers)
        {
            UnitManager manager = UnitManager.Instance;
            var allies = new List<SoloAiAgent>();
            pendingOthers = 0;
            foreach (SoloAiAgent agent in _agents.Values)
            {
                if (agent == null || agent.Unit == null || agent.Unit.GetUnitStatus() == null ||
                    !agent.Unit.GetUnitStatus().IsAlive || !manager.IsAIMoveEnabled(agent.Unit.PlayerId))
                {
                    continue;
                }

                if (agent.Unit.GetTeamId() == teamId && agent.Unit.PlayerId != manager.ControlledUnitId &&
                    agent.Unit.GetMicroUnitBehavior() != null && agent.Unit.GetMacroUnitBehavior() != null &&
                    agent.Unit.GetTacticalMapUnitBehavior() != null)
                {
                    allies.Add(agent);
                }
                else if (!_completedMacroAgents.Contains(agent.Unit.PlayerId))
                {
                    pendingOthers++;
                }
            }

            return allies;
        }

        /// <summary>
        /// 味方AIの経路表示を停止し、実際のユニットの開始地点から再探索できる状態に戻します。
        /// </summary>
        private void ResetAllyMacroPath(UnitBehavior unit)
        {
            if (_macroPathPlaybacks.TryGetValue(unit.PlayerId, out Coroutine playback))
            {
                StopCoroutine(playback);
                _macroPathPlaybacks.Remove(unit.PlayerId);
            }

            unit.EndAiMacroPathVisualization();
            Vector2Int start = unit.GetMicroUnitBehavior().GetCurrentCoordinate();
            unit.GetMacroUnitBehavior().CancelAiMacroMovement();
            unit.GetMacroUnitBehavior().TeleportMoveToCoordinate(start.x, start.y);
            unit.GetTacticalMapUnitBehavior().TeleportMoveToCoordinate(start.x, start.y);
            unit.GetUnitStatus().SetProactiveness(unit.GetUnitStatus().ActionPoint);
            ApplyMigrationPath(unit, null);
            _completedMacroAgents.Remove(unit.PlayerId);
        }

        /// <summary>
        /// 新しいマクロフェイズではターゲット指定と計画の完了履歴を破棄します。
        /// </summary>
        private void RefreshTargetOrderSession()
        {
            MacroPlanningSession session = MatchPhaseManager.Instance?.MacroPlanning;
            if (_targetOrderSession == session)
            {
                return;
            }

            _targetOrderSession = session;
            _macroPlanningBudget = null;
            _orderedTargetPlayerId = -1;
            _targetOrderStatus = string.Empty;
            _completedMacroAgents.Clear();
        }

        /// <summary>
        /// マクロ経路の探索を開始し、必要に応じて他AIの完了済み計画を保持します。
        /// </summary>
        /// <param name="preserveCompletedPlans">
        /// ターゲット指定の再探索で、変更対象外の計画を保持する場合は<c>true</c>です。
        /// </param>
        public void PlanMacroPhaseImmediately(bool preserveCompletedPlans = false)
        {
            RefreshTargetOrderSession();
            if (_macroPlanningBudget == null || (preserveCompletedPlans && _hasPlannedMacroPhase))
            {
                _macroPlanningBudget = new AiMacroPlanningBudget(Time.timeAsDouble,
                    Mathf.Clamp(_macroPlanningTargetSeconds, 1.0f, 38.0f),
                    Mathf.Clamp(_macroPlanningMaximumSeconds, 1.0f, 38.0f));
            }
            if (!preserveCompletedPlans)
            {
                _completedMacroAgents.Clear();
            }
            RemoveDestroyedAgents();
            CancelMacroPlanning();
            _hasPlannedMacroPhase = false;
            _isMacroPlanning = true;
            _planGenerationId++;
            int planningRegistrationVersion = _registrationVersion;
            int planningGenerationId = _planGenerationId;

            _macroPlanningCoroutine = StartCoroutine(
                PlanMacroPhaseIncremental(
                planningRegistrationVersion,
                planningGenerationId));
        }

        public void InitializeMicroPhaseImmediately()
        {
            RemoveDestroyedAgents();
            _worldState.Update(0.0f, MatchPhase.Micro);

            foreach (SoloAiAgent agent in _agents.Values)
            {
                if (agent != null && agent.Unit != null)
                {
                    agent.OnMicroPhaseEntered();
                }
            }
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);

                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);

            if (_definitionCatalog == null)
            {
                _definitionCatalog = Resources.Load<AiDefinitionCatalog>("AI/AiDefinitionCatalog");
            }

            AiDefinitionRegistry.Configure(_definitionCatalog);
            _difficultyProfile = AiDifficultyProfile.Create(_difficulty);
            _activeTeamTacticSeed = 0;
            _hasActiveTeamTacticMatch = false;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
                _worldState.Reset();
                _searchPlanner.Reset();
                AiPublicStateRegistry.Clear();
                _teamTacticSelector.Reset();
                _coordinatedStrikeController.Reset();
            }
        }

        private void Update()
        {
            RemoveDestroyedAgents();
            MatchPhaseManager phaseManager = MatchPhaseManager.Instance;

            if (phaseManager == null)
            {
                return;
            }

            MatchPhase phase = phaseManager.CurrentPhase;

            _worldState.Update(Time.deltaTime, phase);

            if (phase != _lastPhase)
            {
                if (phase == MatchPhase.Micro)
                {
                    _coordinatedStrikeController.Reset();
                    InitializeMicroPhaseImmediately();
                }

                if (phase != MatchPhase.Macro)
                {
                    CancelMacroPlanning();
                    _hasPlannedMacroPhase = false;
                }

                _lastPhase = phase;
            }

            if (phase == MatchPhase.Micro)
            {
                _coordinatedStrikeController.Update(
                    _agents.Values,
                    Time.deltaTime,
                    _difficultyProfile.CoordinationTolerance,
                    GetCoordinationTolerance);
            }

            if (phase == MatchPhase.Macro && !_isMacroPlanning &&
                (!_hasPlannedMacroPhase ||
                _plannedRegistrationVersion != _registrationVersion))
            {
                PlanMacroPhaseImmediately();
            }
        }

        /// <summary>
        /// 全体期限とフレーム内予算を守り、AI全員のマクロ計画を構築します。
        /// </summary>
        /// <remarks>
        /// <para>実際の残り時間を毎回配分し直し、早く終わった分を後続へ回します。</para>
        /// <para>HUDのタイマーと同じゲーム内時間を使い、ポーズにも追従します。</para>
        /// </remarks>
        private IEnumerator PlanMacroPhaseIncremental(
            int planningRegistrationVersion,
            int planningGenerationId)
        {
            if (_agents.Count == 0)
            {
                CompleteMacroPlanning(planningRegistrationVersion, planningGenerationId);
                yield break;
            }

            AiBoardAdapter board;

            try
            {
                board = new AiBoardAdapter();
                board.RefreshOccupiedCoordinates();
            }
            catch (Exception exception)
            {
                Debug.LogError("SoloAiDirector: Board初期化失敗。" + exception);
                CompleteMacroPlanning(planningRegistrationVersion, planningGenerationId);
                yield break;
            }

            Dictionary<int, List<SoloAiAgent>> teams = GroupAgentsByTeam();
            List<KeyValuePair<int, List<SoloAiAgent>>> orderedTeams = OrderTeamsForPlanning(teams);
            int remainingAgents = 0;

            foreach (KeyValuePair<int, List<SoloAiAgent>> team in orderedTeams)
            {
                foreach (SoloAiAgent agent in team.Value)
                {
                    if (!_completedMacroAgents.Contains(agent.Unit.PlayerId))
                    {
                        remainingAgents++;
                    }
                }
            }

            foreach (KeyValuePair<int, List<SoloAiAgent>> teamPair in orderedTeams)
            {
                if (!IsPlanCurrent(planningRegistrationVersion, planningGenerationId))
                {
                    CancelMacroPlanning();
                    yield break;
                }

                int teamId = teamPair.Key;
                AiMacroPlanner planner = new AiMacroPlanner(board, GetDifficultyProfile(teamId));
                List<SoloAiAgent> teamAgents = teamPair.Value;
                if (teamAgents.TrueForAll(agent => _completedMacroAgents.Contains(agent.Unit.PlayerId)))
                {
                    continue;
                }

                if (_macroPlanningBudget.GetRemainingSeconds(Time.timeAsDouble) <= 0.0)
                {
                    foreach (SoloAiAgent pendingAgent in teamAgents)
                    {
                        if (!_completedMacroAgents.Contains(pendingAgent.Unit.PlayerId))
                        {
                            ApplyMigrationPath(pendingAgent.Unit, null);
                            _completedMacroAgents.Add(pendingAgent.Unit.PlayerId);
                        }
                    }

                    continue;
                }

                AiWorldSnapshot snapshot = _worldState.BuildSnapshot(teamId);
                AiTeamTacticContext tacticContext = new AiTeamTacticContext(
                    teamId,
                    teamAgents,
                    snapshot,
                    board);
                AiTeamTacticPlan tacticPlan = _teamTacticSelector.SelectAndBuild(
                    tacticContext,
                    _targetSelector);
                var searchDestinations = new List<Vector2Int>();

                foreach (SoloAiAgent teamAgent in teamAgents)
                {
                    if (teamAgent == null || teamAgent.Unit == null ||
                        _completedMacroAgents.Contains(teamAgent.Unit.PlayerId))
                    {
                        continue;
                    }

                    AiUnitTacticDirective assignedDirective = tacticPlan.GetDirective(teamAgent.Unit.PlayerId);

                    AiPublicUnitState orderedTarget = teamId == ResolveLocalPlayerTeamId()
                        ? AiMacroTargetOrder.FindTarget(snapshot.Enemies, _orderedTargetPlayerId) : null;
                    if (orderedTarget != null)
                    {
                        assignedDirective = AiUnitTacticDirective.CreateEngage(
                            teamAgent, orderedTarget, AiTeamTaskIds.EngagePrimary);
                        tacticPlan.SetDirective(assignedDirective);
                    }
                    else
                    {
                        AiPlayerSupportPlanner.ApplyDirective(
                            assignedDirective, snapshot, board,
                            UnitManager.Instance != null ? UnitManager.Instance.ControlledUnitId : -1);
                    }
                    CompleteSearchDirective(teamAgent, assignedDirective, snapshot, board, searchDestinations);
                    teamAgent.ApplyTacticalDirective(assignedDirective);
                }

                Debug.Log(
                    "SoloAI TeamTactic: team=" + teamId + ", seed=" + _activeTeamTacticSeed +
                    ", selected=" +
                    _teamTacticSelector.GetSelectedTacticId(
                    teamId) +
                    ", members=" +
                    tacticContext.AgentCount +
                    ", executing=" +
                    tacticPlan.TacticId +
                    ", phase=" +
                    tacticPlan.PhaseId +
                    ", utility=" +
                    tacticPlan.Utility.ToString(
                    "0.00") +
                    ", primary=" +
                    (tacticPlan.PrimaryTarget != null ? tacticPlan.PrimaryTarget.PlayerId.ToString() : "none"));
                AiReservationTable reservations = new AiReservationTable();
                foreach (SoloAiAgent completedAgent in teamAgents)
                {
                    if (!_completedMacroAgents.Contains(completedAgent.Unit.PlayerId) ||
                        completedAgent.Unit.GetMicroUnitBehavior() == null)
                    {
                        continue;
                    }

                    var reservedPath = new List<Vector2Int>();
                    reservedPath.Add(completedAgent.Unit.GetMicroUnitBehavior().GetCurrentCoordinate());
                    reservedPath.AddRange(completedAgent.Unit.GetUnitStatus().MigrationPath);
                    reservations.Reserve(completedAgent.Unit.PlayerId, reservedPath);
                    UpdatePlannedAllyCoordinate(snapshot, completedAgent.Unit.PlayerId, reservedPath);
                }

                teamAgents.Sort(
                    (left, right) => GetPlanningPriority(left).CompareTo(
                    GetPlanningPriority(
                    right)));

                foreach (SoloAiAgent agent in teamAgents)
                {
                    if (agent != null && agent.Unit != null &&
                        _completedMacroAgents.Contains(agent.Unit.PlayerId))
                    {
                        continue;
                    }

                    int agentsSharingBudget = Mathf.Max(1, remainingAgents);

                    remainingAgents--;

                    if (!IsPlanCurrent(planningRegistrationVersion, planningGenerationId))
                    {
                        CancelMacroPlanning();
                        yield break;
                    }

                    if (agent == null || agent.Unit == null)
                    {
                        continue;
                    }

                    int playerId = agent.Unit.PlayerId;
                    AiUnitTacticDirective directive = tacticPlan.GetDirective(playerId);

                    if (directive == null ||
                        (directive.Target == null && !directive.HasTargetCoordinate &&
                        !directive.HasReferenceCoordinate))
                    {
                        ApplyMigrationPath(agent.Unit, null);
                        _completedMacroAgents.Add(playerId);
                        continue;
                    }

                    UnitManager unitManager = UnitManager.Instance;

                    if (unitManager == null || !unitManager.IsAIMoveEnabled(playerId))
                    {
                        ApplyMigrationPath(agent.Unit, null);
                        _completedMacroAgents.Add(playerId);
                        continue;
                    }

                    AiUnitPlan plan = null;
                    float remainingSeconds = HUDManager.Instance != null ? HUDManager.Instance.PhaseRemainingSeconds : 0.0f;
                    float availableSeconds = Mathf.Max(
                        0.0f,
                        remainingSeconds - Mathf.Max(
                        0.1f,
                        _macroPlanningFinishMarginSeconds));
                    double deadline = _macroPlanningBudget.GetAgentDeadline(
                        Time.timeAsDouble, agentsSharingBudget, availableSeconds);
                    double agentStartedAt = Time.realtimeSinceStartupAsDouble;

                    yield return planner.PlanIncremental(
                        agent.Unit,
                        directive.Target,
                        snapshot.Enemies,
                        snapshot.Allies,
                        reservations,
                        completedPlan => plan = completedPlan,
                        _macroPlanningNodesPerFrame,
                        _macroPlanningBudgetMilliseconds,
                        planningGenerationId,
                        directive,
                        () => Time.timeAsDouble >= deadline);

                    if (!IsPlanCurrent(planningRegistrationVersion, planningGenerationId))
                    {
                        CancelMacroPlanning();
                        yield break;
                    }

                    if (plan == null || plan.PlanGenerationId != planningGenerationId)
                    {
                        continue;
                    }

                    ApplyMigrationPath(agent.Unit, plan.Path);
                    _completedMacroAgents.Add(playerId);
                    reservations.Reserve(playerId, plan.Path);
                    UpdatePlannedAllyCoordinate(snapshot, playerId, plan.Path);
                    StartAllyMacroPathPlayback(
                        agent.Unit,
                        new List<Vector2Int>(plan.Path),
                        planningRegistrationVersion,
                        planningGenerationId);
                    Debug.Log(
                        "SoloAI MacroPlan: unit=" + playerId + ", roleId=" + agent.RoleId + ", tactic=" +
                        tacticPlan.TacticId +
                        ", task=" +
                        agent.TeamTaskId +
                        ", target=" +
                        (agent.Target != null ? agent.Target.PlayerId.ToString() : "none") +
                        ", plannedWeapon=" +
                        plan.PlannedWeaponName +
                        ", weaponRoleId=" +
                        plan.PlannedWeaponRoleId +
                        ", preferredRange=" +
                        plan.PreferredMinimumCells.ToString(
                        "0.0") +
                        "-" +
                        plan.PreferredMaximumCells.ToString(
                        "0.0") +
                        " cells, steps=" +
                        Mathf.Max(
                        0,
                        plan.Path.Count - 1) +
                        ", utility=" +
                        plan.Utility.ToString(
                        "0.00") +
                        ", timeLimited=" +
                        plan.WasPlanningTimeLimited +
                        ", evaluatedNodes=" +
                        plan.EvaluatedNodes +
                        ", evaluatedCoordinates=" + plan.EvaluatedCoordinates +
                        ", elapsedMs=" + ((Time.realtimeSinceStartupAsDouble - agentStartedAt) * 1000.0).ToString("0.0") +
                        ", generation=" +
                        planningGenerationId);
                }
            }

            CompleteMacroPlanning(planningRegistrationVersion, planningGenerationId);
        }

        /// <summary>
        /// 攻撃対象を失ったAIへ、最終観測地点または未探索領域への移動を補完します。
        /// </summary>
        private void CompleteSearchDirective(
            SoloAiAgent agent,
            AiUnitTacticDirective directive,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board,
            List<Vector2Int> searchDestinations)
        {
            if (directive == null || directive.SuppressWeaponUse)
            {
                return;
            }

            if (directive.Target != null)
            {
                _searchPlanner.ResetPlayer(agent.Unit.PlayerId);

                return;
            }

            TacticalMapUnitBehavior map = agent.Unit.GetTacticalMapUnitBehavior();

            if (map == null)
            {
                return;
            }

            object unitData = GameDataRepository.Instance != null ? GameDataRepository.Instance.GetUnitData() : null;
            int maximumClimbHeight = Mathf.Max(
                0,
                AiReflection.ReadInt(
                unitData,
                2,
                "CAN_MOVE_STEP",
                "CanMoveStep",
                "MaximumClimbHeight",
                "_canMoveStep",
                "_maximumClimbHeight"));

            if (_searchPlanner.TrySelectDestination(
                agent.Unit.PlayerId,
                map.GetCurrentCoordinate(),
                snapshot.SearchCoordinates,
                board,
                maximumClimbHeight,
                out Vector2Int destination,
                searchDestinations))
            {
                AiSearchPlanner.ApplyDestination(directive, destination);
                searchDestinations.Add(destination);
            }
        }

        private void StartAllyMacroPathPlayback(
            UnitBehavior unit,
            IReadOnlyList<Vector2Int> path,
            int registrationVersion,
            int generationId)
        {
            if (unit == null || path == null || path.Count <= 1 || !ShouldShowAiMacroPath(unit))
            {
                return;
            }

            _macroPathPlaybacks[unit.PlayerId] = StartCoroutine(
                PlayAiMacroPath(unit, path, registrationVersion, generationId));
        }

        private IEnumerator PlayAiMacroPath(
            UnitBehavior unit,
            IReadOnlyList<Vector2Int> path,
            int registrationVersion,
            int generationId)
        {
            if (unit == null || path == null || path.Count <= 1)
            {
                yield break;
            }

            MacroUnitBehavior macro = unit.GetMacroUnitBehavior();
            TacticalMapUnitBehavior tacticalMap = unit.GetTacticalMapUnitBehavior();
            UnitStatus status = unit.GetUnitStatus();

            if (macro == null || tacticalMap == null || status == null)
            {
                yield break;
            }

            if (!unit.BeginAiMacroPathVisualization())
            {
                yield break;
            }

            try
            {
                for (int index = 1; index < path.Count; index++)
                {
                    if (!IsMacroPhase() || registrationVersion != _registrationVersion ||
                        generationId != _planGenerationId)
                    {
                        break;
                    }

                    Vector2Int coordinate = path[index];

                    if (!unit.LerpAiMacroPathVisualizationToCoordinate(coordinate.x, coordinate.y))
                    {
                        break;
                    }

                    while (status.IsMovable)
                    {
                        if (!IsPlanCurrent(registrationVersion, generationId))
                        {
                            yield break;
                        }

                        yield return null;
                    }

                    if (!IsMacroPhase())
                    {
                        yield break;
                    }

                    unit.CompleteAiMacroPathVisualizationStep();

                    if (macro.GetCurrentCoordinate() != coordinate ||
                        tacticalMap.GetCurrentCoordinate() != coordinate)
                    {
                        Debug.LogWarning(
                            "SoloAI MacroPath表示の座標が一致しません。" + $" Player={unit.PlayerId}," +
                            $" Expected={coordinate}," +
                            $" Macro={macro.GetCurrentCoordinate()}," +
                            $" Map={tacticalMap.GetCurrentCoordinate()}");
                    }

                    yield return null;
                }
            }
            finally
            {
                if (unit != null)
                {
                    unit.EndAiMacroPathVisualization();
                }
            }
        }

        private static bool IsMacroPhase()
        {
            return MatchPhaseManager.Instance != null &&
                MatchPhaseManager.Instance.CurrentPhase == MatchPhase.Macro;
        }

        /// <summary>
        /// 指定されたAIユニットのMacro移動経路を
        /// ローカルプレイヤーへ表示するか判定します。
        /// </summary>
        /// <param name="unit">
        /// 判定対象のユニットです。
        /// </param>
        /// <returns>
        /// ローカルプレイヤーと同じチームのAIユニットの場合は
        /// <c>true</c>です。
        /// </returns>
        private static bool ShouldShowAiMacroPath(UnitBehavior unit)
        {
            if (unit == null)
            {
                return false;
            }

            UnitManager unitManager = UnitManager.Instance;

            if (unitManager == null)
            {
                return false;
            }

            int localPlayerId = unitManager.LocalPlayerId;

            if (localPlayerId < 0)
            {
                return false;
            }

            if (unit.PlayerId == localPlayerId)
            {
                return false;
            }

            if (!unitManager.IsAIControlledUnit(unit.PlayerId))
            {
                return false;
            }

            int localTeamId = unitManager.GetTeamId(localPlayerId);

            if (localTeamId < 0)
            {
                return false;
            }

            return unit.GetTeamId() == localTeamId;
        }

        private bool IsPlanCurrent(int registrationVersion, int generationId)
        {
            return IsMacroPhase() && registrationVersion == _registrationVersion &&
                generationId == _planGenerationId;
        }

        private void CompleteMacroPlanning(int planningRegistrationVersion, int planningGenerationId)
        {
            if (planningGenerationId != _planGenerationId)
            {
                return;
            }

            _hasPlannedMacroPhase = true;
            _isMacroPlanning = false;
            _plannedRegistrationVersion = planningRegistrationVersion;
            _macroPlanningCoroutine = null;
            if (_macroPlanningBudget != null)
            {
                Debug.Log("SoloAI MacroPlanningComplete: generation=" + planningGenerationId +
                    ", agents=" + _completedMacroAgents.Count +
                    ", elapsedSeconds=" + (Time.timeAsDouble - _macroPlanningBudget.StartedAt).ToString("0.00"));
            }
        }

        private void CancelMacroPlanning()
        {
            if (_macroPlanningCoroutine != null)
            {
                StopCoroutine(_macroPlanningCoroutine);
            }

            _macroPlanningCoroutine = null;
            _isMacroPlanning = false;
        }

        private static void ApplyMigrationPath(UnitBehavior unit, IReadOnlyList<Vector2Int> path)
        {
            if (unit == null || unit.GetUnitStatus() == null)
            {
                return;
            }

            List<Vector2Int> migrationPath = new List<Vector2Int>();

            if (path != null)
            {
                for (int index = 1; index < path.Count; index++)
                {
                    migrationPath.Add(path[index]);
                }
            }

            unit.GetUnitStatus().SetMigrationPath(migrationPath);
        }

        /// <summary>
        /// 先に計画した味方の到達予定地点をSnapshotへ反映し、
        /// 後続ユニットが可変人数でも相互カバー位置を評価できるようにします。
        /// </summary>
        private static void UpdatePlannedAllyCoordinate(
            AiWorldSnapshot snapshot,
            int playerId,
            IReadOnlyList<Vector2Int> path)
        {
            if (snapshot == null || path == null || path.Count == 0)
            {
                return;
            }

            Vector2Int destination = path[path.Count - 1];

            foreach (AiPublicUnitState ally in snapshot.Allies)
            {
                if (ally != null && ally.PlayerId == playerId)
                {
                    ally.MapCoordinate = destination;

                    return;
                }
            }
        }

        private Dictionary<int, List<SoloAiAgent>> GroupAgentsByTeam()
        {
            Dictionary<int, List<SoloAiAgent>> result = new Dictionary<int, List<SoloAiAgent>>();

            foreach (SoloAiAgent agent in _agents.Values)
            {
                if (agent == null || agent.Unit == null || agent.Unit.GetUnitStatus() == null ||
                    !agent.Unit.GetUnitStatus().IsAlive)
                {
                    continue;
                }

                int teamId = agent.Unit.GetTeamId();

                if (!result.TryGetValue(teamId, out List<SoloAiAgent> list))
                {
                    list = new List<SoloAiAgent>();
                    result[teamId] = list;
                }

                list.Add(agent);
            }

            return result;
        }

        /// <summary>
        /// ローカルプレイヤーと同じチームを先頭にして、
        /// Macro経路の計画順を生成します。
        /// </summary>
        /// <remarks>
        /// 味方AIの経路を先に確定することで、敵チーム全員の探索完了を
        /// 待たずにプレイヤーへ移動予定を表示できます。
        /// </remarks>
        private static List<KeyValuePair<int, List<SoloAiAgent>>> OrderTeamsForPlanning(
            Dictionary<int, List<SoloAiAgent>> teams)
        {
            List<KeyValuePair<int, List<SoloAiAgent>>> orderedTeams = teams != null ? new List<KeyValuePair<int, List<SoloAiAgent>>>(
                teams) : new List<KeyValuePair<int, List<SoloAiAgent>>>();
            int localTeamId = ResolveLocalPlayerTeamId();

            orderedTeams.Sort(delegate (
                KeyValuePair<int, List<SoloAiAgent>> left,
                KeyValuePair<int, List<SoloAiAgent>> right)
            {
                bool isLeftLocalTeam = left.Key == localTeamId;
                bool isRightLocalTeam = right.Key == localTeamId;

                if (isLeftLocalTeam != isRightLocalTeam)
                {
                    return isLeftLocalTeam ? -1 : 1;
                }

                return left.Key.CompareTo(right.Key);
            });

            return orderedTeams;
        }

        /// <summary>
        /// ローカルプレイヤーのチームIDを取得します。
        /// </summary>
        private static int ResolveLocalPlayerTeamId()
        {
            UnitManager unitManager = UnitManager.Instance;

            if (unitManager == null || unitManager.LocalPlayerId < 0)
            {
                return -1;
            }

            return unitManager.GetTeamId(unitManager.LocalPlayerId);
        }

        private void RemoveDestroyedAgents()
        {
            List<int> removeIds = new List<int>();

            foreach (KeyValuePair<int, SoloAiAgent> pair in _agents)
            {
                if (pair.Value == null || pair.Value.Unit == null)
                {
                    removeIds.Add(pair.Key);
                }
            }

            foreach (int id in removeIds)
            {
                _agents.Remove(id);
                _registrationVersion++;
            }
        }

        private static int GetPlanningPriority(SoloAiAgent agent)
        {
            return agent != null ? agent.RoleDefinition.PlanningPriority : int.MaxValue;
        }
    }
}
