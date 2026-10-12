using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Core;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// AP内で到達可能なヘックスを探索し、
    /// Unitロール・危険度・高低差・味方予約をまとめて評価します。
    /// </summary>
    public sealed class AiMacroPlanner
    {
        /// <summary>
        /// 従来の探索で許可していた最大移動ステップ数です。
        /// </summary>
        private const int k_BaseMaximumPlanningSteps = 12;

        /// <summary>
        /// 長距離化後に許可する絶対最大ステップ数です。
        /// </summary>
        private const int k_AbsoluteMaximumPlanningSteps = 60;

        private struct SearchKey : IEquatable<SearchKey>
        {
            public Vector2Int Coordinate;

            public int Step;

            public bool Equals(SearchKey other)
            {
                return Coordinate == other.Coordinate && Step == other.Step;
            }

            public override bool Equals(object obj)
            {
                return obj is SearchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Coordinate.GetHashCode() * 397) ^ Step;
                }
            }
        }

        private struct OpenNode
        {
            public SearchKey Key;

            public float Cost;
        }

        private sealed class MinHeap
        {
            private readonly List<OpenNode> _items = new List<OpenNode>();

            public int Count => _items.Count;

            public void Push(OpenNode node)
            {
                _items.Add(node);
                int index = _items.Count - 1;

                while (index > 0)
                {
                    int parent = (index - 1) / 2;

                    if (_items[parent].Cost <= node.Cost)
                    {
                        break;
                    }

                    _items[index] = _items[parent];
                    index = parent;
                }

                _items[index] = node;
            }

            public OpenNode Pop()
            {
                OpenNode result = _items[0];
                OpenNode last = _items[_items.Count - 1];

                _items.RemoveAt(_items.Count - 1);

                if (_items.Count == 0)
                {
                    return result;
                }

                int index = 0;

                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;

                    if (left >= _items.Count)
                    {
                        break;
                    }

                    int smallest = right < _items.Count && _items[right].Cost < _items[left].Cost ? right : left;

                    if (_items[smallest].Cost >= last.Cost)
                    {
                        break;
                    }

                    _items[index] = _items[smallest];
                    index = smallest;
                }

                _items[index] = last;

                return result;
            }
        }

        /// <summary>
        /// 直接視認必須武器について、各候補Hexから対象へ射線が通るかを
        /// 1座標につき1回だけ評価します。
        /// </summary>
        private sealed class DirectFirePositionEvaluator
        {
            private const float k_ClearLineUtility = 85.0f;

            private const float k_BlockedLinePenalty = 55.0f;

            private readonly AiBoardAdapter _board;

            private readonly UnitBehavior _self;

            private readonly UnitBehavior _target;

            private readonly Vector2Int _targetCoordinate;

            private readonly AiWeaponProfile _weapon;

            private readonly Dictionary<Vector2Int, float> _utilityCache = new Dictionary<Vector2Int, float>();

            private readonly bool _isEnabled;

            public DirectFirePositionEvaluator(
                AiBoardAdapter board,
                UnitBehavior self,
                UnitBehavior target,
                Vector2Int targetCoordinate,
                AiWeaponProfile weapon)
            {
                _board = board;
                _self = self;
                _target = target;
                _targetCoordinate = targetCoordinate;
                _weapon = weapon;
                _isEnabled = _board != null && _self != null && _target != null && _weapon != null &&
                    _weapon.IsUsable &&
                    _weapon.UseMode == AiWeaponUseMode.Attack &&
                    _weapon.VisibilityRequirement == AiVisibilityRequirement.DirectVisible;
            }

            public float Evaluate(Vector2Int coordinate)
            {
                if (!_isEnabled)
                {
                    return 0.0f;
                }

                if (_utilityCache.TryGetValue(coordinate, out float cachedUtility))
                {
                    return cachedUtility;
                }

                Vector3 selfPosition = _board.GetWorldPosition(coordinate);
                Vector3 targetPosition = _board.GetWorldPosition(_targetCoordinate);
                Vector2 selfPosition2D = new Vector2(selfPosition.x, selfPosition.z);
                Vector2 targetPosition2D = new Vector2(targetPosition.x, targetPosition.z);
                float distance = Vector2.Distance(selfPosition2D, targetPosition2D);

                if (distance < _weapon.MinimumRange || distance > _weapon.MaximumRange)
                {
                    _utilityCache[coordinate] = 0.0f;

                    return 0.0f;
                }

                bool hasClearLine = AiVisibilityEvaluator.CanDirectlySeeFromMapPosition(
                    _self,
                    selfPosition,
                    _target,
                    targetPosition);
                float utility = hasClearLine ? k_ClearLineUtility : -k_BlockedLinePenalty;

                _utilityCache[coordinate] = utility;

                return utility;
            }
        }

        private readonly AiBoardAdapter _board;

        private readonly AiDifficultyProfile _difficulty;

        public AiMacroPlanner(AiBoardAdapter board, AiDifficultyProfile difficulty = null)
        {
            _board = board;
            _difficulty = difficulty ?? AiDifficultyProfile.Create(AiDifficulty.Normal);
        }

        public AiUnitPlan Plan(
            UnitBehavior self,
            UnitBehavior target,
            IReadOnlyList<AiPublicUnitState> enemies,
            IReadOnlyList<AiPublicUnitState> allies,
            AiReservationTable reservations,
            int planGenerationId = 0,
            AiUnitTacticDirective directive = null)
        {
            AiUnitPlan result = new AiUnitPlan
            {
                Target = directive != null && directive.Target != null ? directive.Target : target,
                PlanGenerationId = planGenerationId,
                TeamTaskId = directive != null ? directive.TaskId : AiTeamTaskIds.EngagePrimary
            };

            if (self == null || self.GetTacticalMapUnitBehavior() == null)
            {
                return result;
            }

            Vector2Int start = self.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
            Vector2Int targetCoordinate;

            if (!TryResolvePlanningCoordinate(target, directive, out targetCoordinate))
            {
                return result;
            }

            int maximumClimbHeight = ResolveMaximumClimbHeight();
            string roleId = AiRoleResolver.ResolveRoleId(self);
            AiRoleProfile profile = BuildMovementProfile(
                self,
                start,
                roleId,
                result,
                out AiWeaponProfile plannedWeapon);
            DirectFirePositionEvaluator directFireEvaluator = new DirectFirePositionEvaluator(
                _board,
                self,
                result.Target,
                targetCoordinate,
                plannedWeapon);
            int maximumSteps = ResolveMaximumSteps(self, profile);
            int minimumPlannedSteps = ResolveMinimumPlannedSteps(maximumSteps, profile, directive);
            AiDangerEvaluator dangerEvaluator = new AiDangerEvaluator(_board, enemies, self, _difficulty);
            AiTeamEvaluator teamEvaluator = new AiTeamEvaluator(_board, allies);
            var positionUtilities = new Dictionary<Vector2Int, float>();
            float startDanger = dangerEvaluator.Evaluate(start);
            MinHeap open = new MinHeap();
            Dictionary<SearchKey, float> bestCosts = new Dictionary<SearchKey, float>();
            Dictionary<SearchKey, SearchKey> parents = new Dictionary<SearchKey, SearchKey>();
            SearchKey startKey = new SearchKey
            {
                Coordinate = start,
                Step = 0
            };

            bestCosts[startKey] = 0f;
            open.Push(new OpenNode { Key = startKey, Cost = 0f });
            SearchKey fallbackBestKey = startKey;
            float fallbackBestUtility = EvaluateDestination(
                start,
                0,
                start,
                targetCoordinate,
                profile,
                dangerEvaluator,
                teamEvaluator,
                self,
                roleId,
                directive,
                startDanger,
                directFireEvaluator,
                positionUtilities);
            SearchKey preferredBestKey = startKey;
            float preferredBestUtility = float.NegativeInfinity;
            bool hasPreferredCandidate = minimumPlannedSteps <= 0;

            if (hasPreferredCandidate)
            {
                preferredBestUtility = fallbackBestUtility;
            }

            while (open.Count > 0)
            {
                OpenNode current = open.Pop();

                if (!bestCosts.TryGetValue(current.Key, out float storedCost) ||
                    current.Cost > storedCost +
                    0.001f)
                {
                    continue;
                }

                float utility = EvaluateDestination(
                    current.Key.Coordinate,
                    current.Key.Step,
                    start,
                    targetCoordinate,
                    profile,
                    dangerEvaluator,
                    teamEvaluator,
                    self,
                    roleId,
                    directive,
                    startDanger,
                    directFireEvaluator,
                    positionUtilities);

                if (utility > fallbackBestUtility)
                {
                    fallbackBestUtility = utility;
                    fallbackBestKey = current.Key;
                }

                if (current.Key.Step >= minimumPlannedSteps && utility > preferredBestUtility)
                {
                    preferredBestUtility = utility;
                    preferredBestKey = current.Key;
                    hasPreferredCandidate = true;
                }

                if (current.Key.Step >= maximumSteps)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in _board.GetNeighbors(current.Key.Coordinate))
                {
                    int nextStep = current.Key.Step + 1;

                    if (parents.TryGetValue(current.Key, out SearchKey previousKey) &&
                        neighbor == previousKey.Coordinate)
                    {
                        continue;
                    }

                    if (!_board.CanStep(current.Key.Coordinate, neighbor, maximumClimbHeight))
                    {
                        continue;
                    }

                    if (_board.IsOccupied(neighbor, self))
                    {
                        continue;
                    }

                    if (reservations != null &&
                        reservations.IsMoveReserved(
                        nextStep,
                        current.Key.Coordinate,
                        neighbor,
                        self.PlayerId))
                    {
                        continue;
                    }

                    float danger = dangerEvaluator.Evaluate(neighbor);
                    float movementCost = 1f + danger * 0.025f * profile.DangerWeight;
                    SearchKey nextKey = new SearchKey
                    {
                        Coordinate = neighbor,
                        Step = nextStep
                    };
                    float nextCost = current.Cost + movementCost;

                    if (bestCosts.TryGetValue(nextKey, out float oldCost) && oldCost <= nextCost)
                    {
                        continue;
                    }

                    bestCosts[nextKey] = nextCost;
                    parents[nextKey] = current.Key;
                    open.Push(new OpenNode { Key = nextKey, Cost = nextCost });
                }
            }

            SearchKey selectedBestKey = hasPreferredCandidate ? preferredBestKey : fallbackBestKey;
            float selectedBestUtility = hasPreferredCandidate ? preferredBestUtility : fallbackBestUtility;

            result.Path.AddRange(ReconstructPath(startKey, selectedBestKey, parents));
            result.Utility = selectedBestUtility;
            result.EvaluatedCoordinates = positionUtilities.Count;

            return result;
        }

        /// <summary>
        /// Macro Phaseの経路探索を複数フレームに分割して実行します。
        /// </summary>
        /// <param name="self">
        /// 経路を探索するAIユニットです。
        /// </param>
        /// <param name="target">
        /// 攻撃対象のユニットです。
        /// </param>
        /// <param name="enemies">
        /// 敵ユニット一覧です。
        /// </param>
        /// <param name="reservations">
        /// 味方AIの移動予約です。
        /// </param>
        /// <param name="onCompleted">
        /// 探索完了時に呼び出すコールバックです。
        /// </param>
        /// <param name="nodesPerFrame">
        /// 1フレームで処理する最大ノード数です。
        /// </param>
        /// <param name="timeBudgetMilliseconds">
        /// 1フレームで探索に使用する目安時間です。
        /// </param>
        /// <param name="shouldFinish">
        /// trueの場合、探索済みの最善経路を確定します。
        /// フレームをまたぐ待ち時間も含めた期限管理に使用します。
        /// </param>
        /// <remarks>
        /// <para>フェーズ終了でコルーチンが破棄される前に部分解を返します。</para>
        /// </remarks>
        public IEnumerator PlanIncremental(
            UnitBehavior self,
            UnitBehavior target,
            IReadOnlyList<AiPublicUnitState> enemies,
            IReadOnlyList<AiPublicUnitState> allies,
            AiReservationTable reservations,
            Action<AiUnitPlan> onCompleted,
            int nodesPerFrame = 256,
            float timeBudgetMilliseconds = 3.0f,
            int planGenerationId = 0,
            AiUnitTacticDirective directive = null,
            Func<bool> shouldFinish = null)
        {
            AiUnitPlan result = new AiUnitPlan
            {
                Target = directive != null && directive.Target != null ? directive.Target : target,
                PlanGenerationId = planGenerationId,
                TeamTaskId = directive != null ? directive.TaskId : AiTeamTaskIds.EngagePrimary
            };

            if (self == null || self.GetTacticalMapUnitBehavior() == null)
            {
                onCompleted?.Invoke(result);
                yield break;
            }

            Vector2Int start = self.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
            Vector2Int targetCoordinate;

            if (!TryResolvePlanningCoordinate(target, directive, out targetCoordinate))
            {
                onCompleted?.Invoke(result);
                yield break;
            }

            int maximumClimbHeight = ResolveMaximumClimbHeight();
            string roleId = AiRoleResolver.ResolveRoleId(self);
            AiRoleProfile profile = BuildMovementProfile(
                self,
                start,
                roleId,
                result,
                out AiWeaponProfile plannedWeapon);
            DirectFirePositionEvaluator directFireEvaluator = new DirectFirePositionEvaluator(
                _board,
                self,
                result.Target,
                targetCoordinate,
                plannedWeapon);
            int maximumSteps = ResolveMaximumSteps(self, profile);
            int minimumPlannedSteps = ResolveMinimumPlannedSteps(maximumSteps, profile, directive);
            AiDangerEvaluator dangerEvaluator = new AiDangerEvaluator(_board, enemies, self, _difficulty);
            AiTeamEvaluator teamEvaluator = new AiTeamEvaluator(_board, allies);
            var positionUtilities = new Dictionary<Vector2Int, float>();
            float startDanger = dangerEvaluator.Evaluate(start);
            MinHeap open = new MinHeap();
            Dictionary<SearchKey, float> bestCosts = new Dictionary<SearchKey, float>();
            Dictionary<SearchKey, SearchKey> parents = new Dictionary<SearchKey, SearchKey>();
            SearchKey startKey = new SearchKey
            {
                Coordinate = start,
                Step = 0
            };

            bestCosts[startKey] = 0.0f;
            open.Push(new OpenNode { Key = startKey, Cost = 0.0f });
            SearchKey fallbackBestKey = startKey;
            float fallbackBestUtility = EvaluateDestination(
                start,
                0,
                start,
                targetCoordinate,
                profile,
                dangerEvaluator,
                teamEvaluator,
                self,
                roleId,
                directive,
                startDanger,
                directFireEvaluator,
                positionUtilities);
            SearchKey preferredBestKey = startKey;
            float preferredBestUtility = float.NegativeInfinity;
            bool hasPreferredCandidate = minimumPlannedSteps <= 0;

            if (hasPreferredCandidate)
            {
                preferredBestUtility = fallbackBestUtility;
            }

            int safeNodesPerFrame = Mathf.Max(1, nodesPerFrame);
            float safeTimeBudgetSeconds = Mathf.Max(0.0001f, timeBudgetMilliseconds * 0.001f);
            int processedNodes = 0;
            float frameStartTime = Time.realtimeSinceStartup;

            while (open.Count > 0)
            {
                if (shouldFinish != null && shouldFinish())
                {
                    result.WasPlanningTimeLimited = true;
                    break;
                }

                OpenNode current = open.Pop();

                processedNodes++;
                bool isCurrentNodeValid = bestCosts.TryGetValue(current.Key, out float storedCost) &&
                    current.Cost <= storedCost +
                    0.001f;

                if (isCurrentNodeValid)
                {
                    result.EvaluatedNodes++;
                    float utility = EvaluateDestination(
                        current.Key.Coordinate,
                        current.Key.Step,
                        start,
                        targetCoordinate,
                        profile,
                        dangerEvaluator,
                        teamEvaluator,
                        self,
                        roleId,
                        directive,
                        startDanger,
                        directFireEvaluator,
                        positionUtilities);

                    if (utility > fallbackBestUtility)
                    {
                        fallbackBestUtility = utility;
                        fallbackBestKey = current.Key;
                    }

                    if (current.Key.Step >= minimumPlannedSteps && utility > preferredBestUtility)
                    {
                        preferredBestUtility = utility;
                        preferredBestKey = current.Key;
                        hasPreferredCandidate = true;
                    }

                    if (current.Key.Step < maximumSteps)
                    {
                        foreach (Vector2Int neighbor in _board.GetNeighbors(current.Key.Coordinate))
                        {
                            int nextStep = current.Key.Step + 1;

                            if (parents.TryGetValue(current.Key, out SearchKey previousKey) &&
                                neighbor == previousKey.Coordinate)
                            {
                                continue;
                            }

                            if (!_board.CanStep(current.Key.Coordinate, neighbor, maximumClimbHeight))
                            {
                                continue;
                            }

                            if (_board.IsOccupied(neighbor, self))
                            {
                                continue;
                            }

                            if (reservations != null &&
                                reservations.IsMoveReserved(
                                nextStep,
                                current.Key.Coordinate,
                                neighbor,
                                self.PlayerId))
                            {
                                continue;
                            }

                            float danger = dangerEvaluator.Evaluate(neighbor);
                            float movementCost = 1.0f + danger * 0.025f * profile.DangerWeight;
                            SearchKey nextKey = new SearchKey
                            {
                                Coordinate = neighbor,
                                Step = nextStep
                            };
                            float nextCost = current.Cost + movementCost;

                            if (bestCosts.TryGetValue(nextKey, out float oldCost) && oldCost <= nextCost)
                            {
                                continue;
                            }

                            bestCosts[nextKey] = nextCost;
                            parents[nextKey] = current.Key;
                            open.Push(new OpenNode { Key = nextKey, Cost = nextCost });
                        }
                    }
                }

                float elapsed = Time.realtimeSinceStartup - frameStartTime;

                if (open.Count > 0 &&
                    (processedNodes >= safeNodesPerFrame || elapsed >= safeTimeBudgetSeconds))
                {
                    processedNodes = 0;
                    yield return null;
                    frameStartTime = Time.realtimeSinceStartup;
                }
            }

            SearchKey selectedBestKey = hasPreferredCandidate ? preferredBestKey : fallbackBestKey;
            float selectedBestUtility = hasPreferredCandidate ? preferredBestUtility : fallbackBestUtility;

            result.Path.AddRange(ReconstructPath(startKey, selectedBestKey, parents));
            result.Utility = selectedBestUtility;
            result.EvaluatedCoordinates = positionUtilities.Count;
            onCompleted?.Invoke(result);
        }

        /// <summary>
        /// Unitロールと装備武器の射程を統合した
        /// Macro移動用プロファイルを構築します。
        /// </summary>
        private AiRoleProfile BuildMovementProfile(
            UnitBehavior self,
            Vector2Int start,
            string roleId,
            AiUnitPlan result,
            out AiWeaponProfile plannedWeapon)
        {
            float cellSpacing = _board.GetCellSpacing(start);
            AiWeaponSelector weaponSelector = new AiWeaponSelector();
            AiRoleProfile profile = weaponSelector.BuildMovementProfile(
                self,
                roleId,
                cellSpacing,
                out plannedWeapon);

            if (result != null)
            {
                result.PreferredMinimumCells = profile.PreferredMinimumCells;
                result.PreferredMaximumCells = profile.PreferredMaximumCells;
                result.PlannedWeaponRole = plannedWeapon != null ? plannedWeapon.WeaponRole : AiDefinitionRegistry.GetRole(
                    roleId).LegacyRole;
                result.PlannedWeaponRoleId = plannedWeapon != null ? plannedWeapon.WeaponRoleId : roleId;
                result.PlannedWeaponName = plannedWeapon != null ? plannedWeapon.DisplayName : string.Empty;
            }

            return profile;
        }

        /// <summary>
        /// 座標の評価を再利用し、ステップ数ごとの移動・再配置ペナルティを反映します。
        /// </summary>
        /// <remarks>
        /// <para>直接視認評価は戦闘距離の周辺だけで実行します。</para>
        /// <para>座標ごとのキャッシュと合わせ、6対6でも全探索ノードに</para>
        /// <para>Physics判定を行わないようにします。</para>
        /// </remarks>
        private float EvaluateDestination(
            Vector2Int coordinate,
            int steps,
            Vector2Int start,
            Vector2Int target,
            AiRoleProfile profile,
            AiDangerEvaluator dangerEvaluator,
            AiTeamEvaluator teamEvaluator,
            UnitBehavior self,
            string roleId,
            AiUnitTacticDirective directive,
            float startDanger,
            DirectFirePositionEvaluator directFireEvaluator,
            Dictionary<Vector2Int, float> positionUtilities)
        {
            if (!positionUtilities.TryGetValue(coordinate, out float utility))
            {
                utility = EvaluatePosition(coordinate, start, target, profile,
                    dangerEvaluator, teamEvaluator, self, roleId, directive,
                    startDanger, directFireEvaluator);
                positionUtilities[coordinate] = utility;
            }

            float repositionPenalty = directive != null && directive.ForceReposition
                ? Mathf.Max(0, Mathf.Max(1, directive.MinimumRepositionSteps) - steps) *
                    Mathf.Max(1.0f, directive.RepositionWeight)
                : 0.0f;
            return utility - steps * profile.MovementWeight - repositionPenalty;
        }

        /// <summary>
        /// 移動ステップ数に依存しない座標の価値を評価します。
        /// </summary>
        /// <remarks>
        /// 危険度、射線と味方配置は1体の計画中のSnapshotに対して評価し、
        /// 同じ座標の別ステップでは再計算しません。
        /// </remarks>
        private float EvaluatePosition(
            Vector2Int coordinate,
            Vector2Int start,
            Vector2Int target,
            AiRoleProfile profile,
            AiDangerEvaluator dangerEvaluator,
            AiTeamEvaluator teamEvaluator,
            UnitBehavior self,
            string roleId,
            AiUnitTacticDirective directive,
            float startDanger,
            DirectFirePositionEvaluator directFireEvaluator)
        {
            float distanceToTarget = _board.EstimateCellDistance(coordinate, target);
            bool useCombatRange = directive == null || directive.UseCombatRange;
            float rangeUtility;

            if (!useCombatRange)
            {
                float referenceWeight = directive != null ? Mathf.Max(0.0f, directive.ReferenceDistanceWeight) : 10.0f;

                rangeUtility = 42.0f - distanceToTarget * referenceWeight;
            }
            else if (distanceToTarget < profile.PreferredMinimumCells)
            {
                rangeUtility = -(profile.PreferredMinimumCells - distanceToTarget) * 18f;
            }
            else if (distanceToTarget > profile.PreferredMaximumCells)
            {
                rangeUtility = -(distanceToTarget - profile.PreferredMaximumCells) * 13f;
            }
            else
            {
                float middle = (profile.PreferredMinimumCells + profile.PreferredMaximumCells) * 0.5f;

                rangeUtility = 38f - Mathf.Abs(distanceToTarget - middle) * 3f;
            }

            float danger = dangerEvaluator.Evaluate(coordinate);
            int startFloor = _board.GetFloor(start);
            int destinationFloor = _board.GetFloor(coordinate);
            float highGroundUtility = (destinationFloor - startFloor) * profile.HighGroundWeight * (directive != null ? Mathf.Max(
                0.0f,
                directive.HighGroundWeightMultiplier) : 1.0f);
            float formationUtility = teamEvaluator != null ? teamEvaluator.Evaluate(coordinate, self, roleId) : 0.0f;
            float formationMultiplier = directive != null ? Mathf.Max(
                0.0f,
                directive.FormationWeightMultiplier) : 1.0f;

            formationUtility *= formationMultiplier;

            if (teamEvaluator != null && directive != null && directive.RequireMutualCoverage)
            {
                formationUtility += teamEvaluator.EvaluateMutualCoverage(
                    coordinate,
                    self,
                    directive.CoverageMinimumCells,
                    directive.CoverageMaximumCells) * formationMultiplier;
            }

            float dangerMultiplier = directive != null ? Mathf.Max(0.0f, directive.DangerWeightMultiplier) : 1.0f;
            float repositionUtility = EvaluateRepositionUtility(
                coordinate,
                start,
                target,
                danger,
                startDanger,
                directive);
            bool isNearCombatBand = distanceToTarget >= Mathf.Max(0.0f, profile.PreferredMinimumCells - 2.0f) &&
                distanceToTarget <= profile.PreferredMaximumCells +
                2.0f;
            float directFirePositionUtility = useCombatRange && isNearCombatBand &&
                directFireEvaluator != null ? directFireEvaluator.Evaluate(
                coordinate) : 0.0f;
            dangerMultiplier = AiCombatDecisionPolicy.ResolveEngagementDangerMultiplier(
                dangerMultiplier,
                directFirePositionUtility > 0.0f,
                directive != null && directive.ForceReposition);

            return rangeUtility + highGroundUtility + formationUtility + repositionUtility +
                directFirePositionUtility - danger * profile.DangerWeight * dangerMultiplier;
        }

        /// <summary>
        /// 攻撃不能または危険な現在位置に留まることを避け、
        /// 射線を作り直せる実移動量と安全性を評価します。
        /// </summary>
        private float EvaluateRepositionUtility(
            Vector2Int coordinate,
            Vector2Int start,
            Vector2Int target,
            float danger,
            float startDanger,
            AiUnitTacticDirective directive)
        {
            if (directive == null || !directive.ForceReposition)
            {
                return 0.0f;
            }

            int minimumSteps = Mathf.Max(1, directive.MinimumRepositionSteps);
            float repositionWeight = Mathf.Max(1.0f, directive.RepositionWeight);
            float displacement = _board.EstimateCellDistance(start, coordinate);
            float minimumDisplacement = Mathf.Max(1.0f, minimumSteps * 0.5f);
            float utility = 0.0f;

            if (displacement < minimumDisplacement)
            {
                utility -= (minimumDisplacement - displacement) * repositionWeight * 1.5f;
            }
            else
            {
                utility += Mathf.Min(displacement, minimumDisplacement + 3.0f) * repositionWeight * 0.45f;
            }

            utility += (startDanger - danger) * Mathf.Max(0.0f, directive.DangerEscapeWeight);

            if (directive.PreferLateralReposition)
            {
                Vector2 startDirection = new Vector2(start.x - target.x, start.y - target.y);
                Vector2 destinationDirection = new Vector2(coordinate.x - target.x, coordinate.y - target.y);

                if (startDirection.sqrMagnitude > 0.001f && destinationDirection.sqrMagnitude > 0.001f)
                {
                    float directionChange = 1.0f - Mathf.Clamp(
                        Vector2.Dot(
                        startDirection.normalized,
                        destinationDirection.normalized),
                        -1.0f,
                        1.0f);

                    utility += directionChange * repositionWeight * 3.0f;
                }
            }

            return utility;
        }

        private static bool TryResolvePlanningCoordinate(
            UnitBehavior target,
            AiUnitTacticDirective directive,
            out Vector2Int coordinate)
        {
            coordinate = Vector2Int.zero;

            if (directive != null)
            {
                if (directive.HasTargetCoordinate)
                {
                    coordinate = directive.TargetCoordinate;

                    return true;
                }

                if (directive.HasReferenceCoordinate)
                {
                    coordinate = directive.ReferenceCoordinate;

                    return true;
                }
            }

            if (target == null || target.GetTacticalMapUnitBehavior() == null)
            {
                return false;
            }

            coordinate = target.GetTacticalMapUnitBehavior().GetCurrentCoordinate();

            return true;
        }

        private static List<Vector2Int> ReconstructPath(
            SearchKey start,
            SearchKey goal,
            Dictionary<SearchKey, SearchKey> parents)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            SearchKey current = goal;

            path.Add(current.Coordinate);

            while (!current.Equals(start) && parents.TryGetValue(current, out SearchKey parent))
            {
                current = parent;
                path.Add(current.Coordinate);
            }

            path.Reverse();

            return path;
        }

        /// <summary>
        /// 武器用ActionPointを増やさず，Macro経路だけを
        /// ロール定義の倍率で長距離化します。
        /// </summary>
        private static int ResolveMaximumSteps(UnitBehavior unit, AiRoleProfile profile)
        {
            object status = unit.GetUnitStatus();
            float actionPoint = AiReflection.ReadFloat(
                status,
                5f,
                "ActionPoint",
                "InitialProactiveness",
                "CurrentActionPoint",
                "_actionPoint");
            float moveCost = AiReflection.ReadFloat(
                status,
                1.0f,
                "MoveProactivenessCost",
                "MovementCost",
                "MoveCost",
                "_moveProactivenessCost");
            int baseSteps;

            if (moveCost > 0.001f)
            {
                baseSteps = Mathf.FloorToInt(Mathf.Max(0.0f, actionPoint) / moveCost);
            }
            else
            {
                baseSteps = Mathf.FloorToInt(Mathf.Max(1.0f, actionPoint));
            }

            baseSteps = Mathf.Clamp(baseSteps, 1, k_BaseMaximumPlanningSteps);
            float multiplier = profile != null ? profile.PathLengthMultiplier : 3.0f;

            return Mathf.Clamp(
                Mathf.CeilToInt(baseSteps * Mathf.Max(3.0f, multiplier)),
                3,
                k_AbsoluteMaximumPlanningSteps);
        }

        /// <summary>
        /// 最終目的地を必達にはせず，Micro中に経路が尽きにくい
        /// 最低計画長を求めます。
        /// </summary>
        private static int ResolveMinimumPlannedSteps(
            int maximumSteps,
            AiRoleProfile profile,
            AiUnitTacticDirective directive)
        {
            float usageRatio = profile != null ? profile.MinimumPathUsageRatio : 1.0f;
            int minimumSteps = Mathf.CeilToInt(Mathf.Max(0, maximumSteps) * Mathf.Clamp01(usageRatio));

            if (directive != null && directive.ForceReposition)
            {
                minimumSteps = Mathf.Max(minimumSteps, directive.MinimumRepositionSteps);
            }

            return Mathf.Clamp(minimumSteps, 0, Mathf.Max(0, maximumSteps));
        }

        private static int ResolveMaximumClimbHeight()
        {
            object unitData = GameDataRepository.Instance != null ? GameDataRepository.Instance.GetUnitData() : null;

            return Mathf.Max(
                0,
                AiReflection.ReadInt(
                unitData,
                2,
                "CAN_MOVE_STEP",
                "CanMoveStep",
                "MaximumClimbHeight",
                "_canMoveStep",
                "_maximumClimbHeight"));
        }
    }
}
