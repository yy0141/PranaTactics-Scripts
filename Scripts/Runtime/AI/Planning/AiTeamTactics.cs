using System;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 従来の標的・距離・陣形評価を使用する標準戦術です。
    /// </summary>
    public sealed class AiStandardFormationTactic : AiTeamTacticBase
    {
        public override string Id => AiTeamTacticIds.StandardFormation;

        public override float EvaluateUtility(AiTeamTacticContext context)
        {
            return 10.0f;
        }

        public override AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            return AiTeamTacticUtility.BuildNormalAttackPlan(
                Id,
                context,
                targetSelector,
                AiTeamTaskIds.EngagePrimary);
        }
    }

    /// <summary>
    /// 短い合流計画の後は攻勢へ移り、接敵時には合流より交戦を優先します。
    /// </summary>
    public sealed class AiRegroupAvoidCombatTactic : AiTeamTacticBase
    {
        private const int k_MinimumGatheredAgentCount = 2;

        private const float k_GatheredRadiusCells = 9.0f;

        /// <summary>
        /// 一度でも合流を完了したチームです。
        /// 敵を見失ったり味方が散開しても、同じ対戦中は
        /// 再びRegroupへ戻さないために保持します。
        /// </summary>
        private readonly HashSet<int> _assaultStartedTeamIds = new HashSet<int>();

        private readonly Dictionary<int, int> _regroupPlanCountByTeam = new Dictionary<int, int>();

        public override string Id => AiTeamTacticIds.RegroupAvoidCombat;

        public override int MinimumAgentCount => 2;

        public override void ResetRuntimeState()
        {
            _assaultStartedTeamIds.Clear();
            _regroupPlanCountByTeam.Clear();
        }

        /// <remarks>
        /// <para>戦術の実行可否を一時的な敵の観測状態へ依存させません。</para>
        /// <para>敵を見失った場合もStandardFormationへ差し替えず、</para>
        /// <para>RegroupまたはAssault/Searchの現在フェーズを継続します。</para>
        /// </remarks>
        public override bool CanSelect(AiTeamTacticContext context)
        {
            return base.CanSelect(context);
        }

        public override float EvaluateUtility(AiTeamTacticContext context)
        {
            float spread = AiTeamTacticUtility.CalculateMaximumTeamSpread(context);
            AiObjectiveContext objective = context.Snapshot.Objective;
            bool canBeginAssault = HasAssaultStarted(context) ||
                AiTeamTacticUtility.HasGatheredAgents(
                context,
                k_GatheredRadiusCells,
                k_MinimumGatheredAgentCount);

            if (canBeginAssault)
            {
                return 14.0f + objective.AggressionNeed * 30.0f + (objective.MustEngage ? 22.0f : 0.0f) +
                    (objective.CanForceWin ? 16.0f : 0.0f) - objective.PreservationNeed * 5.0f;
            }

            return spread * 7.0f + objective.PreservationNeed * 28.0f - objective.AggressionNeed * 8.0f - (objective.MustEngage ? 18.0f : 0.0f);
        }

        public override AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            bool gathered = AiTeamTacticUtility.HasGatheredAgents(
                context,
                k_GatheredRadiusCells,
                k_MinimumGatheredAgentCount);

            int regroupPlans = _regroupPlanCountByTeam.TryGetValue(context.TeamId, out int count) ? count : 0;
            if (AiEngagementPolicy.ShouldBeginAssault(
                gathered,
                HasNearbyEnemy(context),
                AiTeamTacticUtility.HasAttackableEnemy(context.Snapshot),
                context.Snapshot.Objective.MustEngage,
                regroupPlans))
            {
                _assaultStartedTeamIds.Add(context.TeamId);
            }

            bool canBeginAssault = HasAssaultStarted(context);

            if (canBeginAssault)
            {
                return BuildAssaultPlan(context, targetSelector);
            }

            AiTeamTacticPlan plan = new AiTeamTacticPlan
            {
                TacticId = Id,
                PhaseId = AiTeamTacticPhaseIds.Regroup
            };
            Vector2Int anchor = AiTeamTacticUtility.ResolveTeamMedoid(context);
            _regroupPlanCountByTeam[context.TeamId] = regroupPlans + 1;
            AiPublicUnitState primary = targetSelector.SelectPrimaryTarget(context.Agents, context.Snapshot, context.Board);
            plan.PrimaryTarget = primary;

            foreach (SoloAiAgent agent in context.Agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                plan.SetDirective(
                    new AiUnitTacticDirective
                    {
                        PlayerId = agent.Unit.PlayerId,
                        TaskId = AiTeamTaskIds.Regroup,
                        Target = primary != null ? primary.Unit : null,
                        SuppressWeaponUse = false,
                        UseCombatRange = false,
                        HasReferenceCoordinate = true,
                        ReferenceCoordinate = anchor,
                        ReferenceDistanceWeight = 12.0f,
                        DangerWeightMultiplier = 1.2f,
                        FormationWeightMultiplier = 0.6f,
                        HighGroundWeightMultiplier = 0.6f
                    });
            }

            return plan;
        }

        /// <summary>
        /// 味方の近くに観測された敵がいれば即時交戦します。
        /// </summary>
        private static bool HasNearbyEnemy(AiTeamTacticContext context)
        {
            foreach (AiPublicUnitState enemy in context.Snapshot.Enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.ContactState < AiContactState.MapTracked)
                {
                    continue;
                }

                foreach (AiPublicUnitState ally in context.Snapshot.Allies)
                {
                    if (ally != null && ally.IsAlive &&
                        context.Board.EstimateCellDistance(ally.MapCoordinate, enemy.MapCoordinate) <= 14.0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasAssaultStarted(AiTeamTacticContext context)
        {
            return context != null && _assaultStartedTeamIds.Contains(context.TeamId);
        }

        /// <summary>
        /// 攻勢への移行後は同じ主目標へ射程と射線を確保しながら前進します。
        /// </summary>
        /// <remarks>
        /// <para>合流後は味方密度より、敵への接近・射線確保を優先します。</para>
        /// <para>全味方を同じ距離帯に収める条件を外し、接敵のための散開を許可します。</para>
        /// <para>敵を見失っても合流地点で停止せず、Directorが</para>
        /// <para>LastKnownまたは周辺探索用の座標を補完します。</para>
        /// </remarks>
        private AiTeamTacticPlan BuildAssaultPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            AiTeamTacticPlan plan = new AiTeamTacticPlan
            {
                TacticId = Id,
                PhaseId = AiTeamTacticPhaseIds.Assault
            };
            AiPublicUnitState primary = targetSelector.SelectPrimaryTarget(
                context.Agents,
                context.Snapshot,
                context.Board);

            plan.PrimaryTarget = primary;

            foreach (SoloAiAgent agent in context.Agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                AiUnitTacticDirective directive = AiUnitTacticDirective.CreateEngage(
                    agent,
                    primary,
                    primary != null ? AiTeamTaskIds.EngagePrimary : AiTeamTaskIds.SearchSector);

                directive.SuppressWeaponUse = false;
                directive.UseCombatRange = primary != null;
                directive.DangerWeightMultiplier = 0.65f;
                directive.FormationWeightMultiplier = 0.35f;
                directive.HighGroundWeightMultiplier = 1.2f;
                directive.RequireMutualCoverage = false;

                if (primary == null)
                {
                    directive.ForceReposition = true;
                    directive.MinimumRepositionSteps = 10;
                    directive.RepositionWeight = 14.0f;
                    directive.DangerEscapeWeight = 2.0f;
                    directive.PreferLateralReposition = true;
                    directive.RepositionReason = "regroup_completed_search";
                }

                plan.SetDirective(directive);
            }

            return plan;
        }
    }

    /// <summary>
    /// 相互にカバー可能な間隔を保ちながら高台を優先します。
    /// </summary>
    public sealed class AiCoveredHighGroundTactic : AiTeamTacticBase
    {
        public override string Id => AiTeamTacticIds.CoveredHighGround;

        public override int MinimumAgentCount => 2;

        public override float EvaluateUtility(AiTeamTacticContext context)
        {
            float highGroundPreference = 0.0f;

            foreach (SoloAiAgent agent in context.Agents)
            {
                highGroundPreference += agent != null ? agent.RoleProfile.HighGroundWeight : 0.0f;
            }

            highGroundPreference /= Mathf.Max(1, context.AgentCount);
            AiObjectiveContext objective = context.Snapshot.Objective;

            return 7.0f + highGroundPreference * 9.0f + objective.PreservationNeed * 10.0f +
                objective.AggressionNeed * 5.0f;
        }

        public override AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            AiTeamTacticPlan plan = AiTeamTacticUtility.BuildNormalAttackPlan(
                Id,
                context,
                targetSelector,
                AiTeamTaskIds.HighGroundControl);
            Vector2Int teamAnchor = AiTeamTacticUtility.ResolveTeamMedoid(context);

            foreach (AiUnitTacticDirective directive in plan.Directives.Values)
            {
                directive.HighGroundWeightMultiplier = 3.0f;
                directive.FormationWeightMultiplier = 0.7f;
                directive.DangerWeightMultiplier = 1.2f;
                directive.RequireMutualCoverage = true;
                directive.CoverageMinimumCells = 1.5f;
                directive.CoverageMaximumCells = 5.5f;

                if (directive.Target == null)
                {
                    directive.UseCombatRange = false;
                    directive.HasReferenceCoordinate = true;
                    directive.ReferenceCoordinate = teamAnchor;
                    directive.ReferenceDistanceWeight = 3.0f;
                }
            }

            return plan;
        }
    }

    /// <summary>
    /// 1体の敵へチーム人数に応じた2体以上を割り当て、
    /// Micro Phaseで射撃準備が揃ってから攻撃します。
    /// </summary>
    public sealed class AiCoordinatedStrikeTactic : AiTeamTacticBase
    {
        public override string Id => AiTeamTacticIds.CoordinatedStrike;

        public override int MinimumAgentCount => 2;

        public override bool CanSelect(AiTeamTacticContext context)
        {
            return base.CanSelect(context) && AiTeamTacticUtility.HasAttackableEnemy(context.Snapshot);
        }

        public override float EvaluateUtility(AiTeamTacticContext context)
        {
            AiObjectiveContext objective = context.Snapshot.Objective;

            return 8.0f + objective.AggressionNeed * 32.0f - objective.PreservationNeed * 8.0f +
                (objective.MustEngage ? 22.0f : 0.0f) +
                (objective.CanForceWin ? 18.0f : 0.0f);
        }

        public override AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            AiTeamTacticPlan plan = AiTeamTacticUtility.BuildNormalAttackPlan(
                Id,
                context,
                targetSelector,
                AiTeamTaskIds.EngagePrimary);
            AiPublicUnitState primary = targetSelector.SelectPrimaryTarget(
                context.Agents,
                context.Snapshot,
                context.Board);

            plan.PrimaryTarget = primary;

            if (primary == null)
            {
                return plan;
            }

            List<SoloAiAgent> ordered = new List<SoloAiAgent>(context.Agents);

            ordered.Sort(delegate (SoloAiAgent left, SoloAiAgent right)
            {
                float leftDistance = AiTeamTacticUtility.DistanceTo(context, left, primary.MapCoordinate);
                float rightDistance = AiTeamTacticUtility.DistanceTo(context, right, primary.MapCoordinate);
                int comparison = leftDistance.CompareTo(rightDistance);

                if (comparison != 0)
                {
                    return comparison;
                }

                int leftId = left != null && left.Unit != null ? left.Unit.PlayerId : int.MaxValue;
                int rightId = right != null && right.Unit != null ? right.Unit.PlayerId : int.MaxValue;

                return leftId.CompareTo(rightId);
            });
            int participantCount = Mathf.Clamp(
                Mathf.CeilToInt(context.AgentCount * 0.60f),
                2,
                context.AgentCount);

            for (int index = 0; index < participantCount; index++)
            {
                SoloAiAgent agent = ordered[index];
                AiUnitTacticDirective directive = AiUnitTacticDirective.CreateEngage(
                    agent,
                    primary,
                    AiTeamTaskIds.CoordinatedStrike);

                directive.FormationWeightMultiplier = 0.35f;
                directive.MinimumCoordinatedParticipants = 2;
                plan.SetDirective(directive);
            }

            return plan;
        }
    }

    /// <summary>
    /// 合流や高台確保を待たず、チーム全員で同じ主目標を追撃します。
    /// 危険回避より射程内への到達を優先する攻撃的な戦術です。
    /// </summary>
    public sealed class AiAggressivePursuitTactic : AiTeamTacticBase
    {
        public override string Id => AiTeamTacticIds.AggressivePursuit;

        public override bool CanSelect(AiTeamTacticContext context)
        {
            return base.CanSelect(context) && AiTeamTacticUtility.HasAttackableEnemy(context.Snapshot);
        }

        public override float EvaluateUtility(AiTeamTacticContext context)
        {
            AiObjectiveContext objective = context.Snapshot.Objective;

            return 18.0f + objective.AggressionNeed * 38.0f + (objective.MustEngage ? 28.0f : 0.0f) +
                (objective.CanForceWin ? 22.0f : 0.0f) - objective.PreservationNeed * 10.0f;
        }

        public override AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            AiTeamTacticPlan plan = new AiTeamTacticPlan
            {
                TacticId = Id,
                PhaseId = AiTeamTacticPhaseIds.Assault
            };
            AiPublicUnitState primary = targetSelector.SelectPrimaryTarget(
                context.Agents,
                context.Snapshot,
                context.Board);

            plan.PrimaryTarget = primary;

            foreach (SoloAiAgent agent in context.Agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                AiUnitTacticDirective directive = AiUnitTacticDirective.CreateEngage(
                    agent,
                    primary,
                    AiTeamTaskIds.EngagePrimary);

                directive.SuppressWeaponUse = false;
                directive.UseCombatRange = true;
                directive.DangerWeightMultiplier = 0.45f;
                directive.FormationWeightMultiplier = 0.35f;
                directive.HighGroundWeightMultiplier = 0.8f;
                plan.SetDirective(directive);
            }

            return plan;
        }
    }

    /// <summary>
    /// 戦術を登録・評価し、チームごとに戦術を選択します。
    /// </summary>
    public sealed class AiTeamTacticSelector
    {
        private readonly List<AiTeamTacticBase> _tactics = new List<AiTeamTacticBase>();

        private readonly Dictionary<int, string> _selectedTacticIdByTeam = new Dictionary<int, string>();

        private int _matchSeed;

        public AiTeamTacticSelector()
        {
            Register(new AiStandardFormationTactic());
            Register(new AiRegroupAvoidCombatTactic());
            Register(new AiCoveredHighGroundTactic());
            Register(new AiCoordinatedStrikeTactic());
            Register(new AiAggressivePursuitTactic());
        }

        /// <summary>
        /// 新しい戦術を登録します。同じIDは新しい実装で置き換えます。
        /// </summary>
        public void Register(AiTeamTacticBase tactic)
        {
            if (tactic == null || string.IsNullOrWhiteSpace(tactic.Id))
            {
                return;
            }

            for (int index = 0; index < _tactics.Count; index++)
            {
                if (string.Equals(_tactics[index].Id, tactic.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _tactics[index] = tactic;

                    return;
                }
            }

            _tactics.Add(tactic);
        }

        /// <summary>
        /// 新しい対戦用の戦術選択を開始します。
        /// チームごとの選択結果を破棄し、最初のMacro計画時に
        /// 登録済み候補から決定論的に1つ抽選します。
        /// </summary>
        public void BeginMatch(int matchSeed)
        {
            _matchSeed = matchSeed;
            _selectedTacticIdByTeam.Clear();
            ResetTacticRuntimeStates();
        }

        public AiTeamTacticPlan SelectAndBuild(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            if (context == null || targetSelector == null)
            {
                return new AiTeamTacticPlan();
            }

            AiTeamTacticBase selected = ResolveMatchTactic(context);
            AiTeamTacticBase executionTactic = selected != null && selected.CanSelect(context) ? selected : Find(
                AiTeamTacticIds.StandardFormation);

            if (executionTactic == null)
            {
                return new AiTeamTacticPlan();
            }

            return FinalizePlan(
                executionTactic,
                executionTactic.EvaluateUtility(context),
                context,
                targetSelector);
        }

        public void Reset()
        {
            _matchSeed = 0;
            _selectedTacticIdByTeam.Clear();
            ResetTacticRuntimeStates();
        }

        private void ResetTacticRuntimeStates()
        {
            foreach (AiTeamTacticBase tactic in _tactics)
            {
                if (tactic != null)
                {
                    tactic.ResetRuntimeState();
                }
            }
        }

        public string GetSelectedTacticId(int teamId)
        {
            string tacticId;

            return _selectedTacticIdByTeam.TryGetValue(teamId, out tacticId) ? tacticId : string.Empty;
        }

        /// <summary>
        /// 初回だけ、その時点で成立する戦術から等確率で選択します。
        /// 候補はID順に並べ、辞書や登録順の差に影響されないようにします。
        /// </summary>
        private AiTeamTacticBase ResolveMatchTactic(AiTeamTacticContext context)
        {
            string selectedId;

            if (_selectedTacticIdByTeam.TryGetValue(context.TeamId, out selectedId))
            {
                return Find(selectedId);
            }

            List<AiTeamTacticBase> candidates = new List<AiTeamTacticBase>();

            foreach (AiTeamTacticBase tactic in _tactics)
            {
                if (tactic != null && tactic.CanAssignAtMatchStart(context))
                {
                    candidates.Add(tactic);
                }
            }

            if (candidates.Count == 0)
            {
                AiTeamTacticBase fallback = Find(AiTeamTacticIds.StandardFormation);

                if (fallback != null)
                {
                    _selectedTacticIdByTeam[context.TeamId] = fallback.Id;
                }

                return fallback;
            }

            candidates.Sort(delegate (AiTeamTacticBase left, AiTeamTacticBase right)
            {
                return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
            });
            int selectedIndex = CalculateRandomIndex(_matchSeed, context.TeamId, candidates.Count);
            AiTeamTacticBase selected = candidates[selectedIndex];

            _selectedTacticIdByTeam[context.TeamId] = selected.Id;

            return selected;
        }

        private static int CalculateRandomIndex(int matchSeed, int teamId, int candidateCount)
        {
            if (candidateCount <= 1)
            {
                return 0;
            }

            unchecked
            {
                uint hash = (uint)matchSeed;

                hash ^= (uint)teamId + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;

                return (int)(hash % (uint)candidateCount);
            }
        }

        private AiTeamTacticBase Find(string tacticId)
        {
            foreach (AiTeamTacticBase tactic in _tactics)
            {
                if (tactic != null && string.Equals(tactic.Id, tacticId, StringComparison.OrdinalIgnoreCase))
                {
                    return tactic;
                }
            }

            return null;
        }

        private static AiTeamTacticPlan FinalizePlan(
            AiTeamTacticBase tactic,
            float utility,
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector)
        {
            AiTeamTacticPlan plan = tactic.BuildPlan(context, targetSelector) ?? new AiTeamTacticPlan();

            plan.TacticId = tactic.Id;
            plan.Utility = utility;

            return plan;
        }
    }

    internal static class AiTeamTacticUtility
    {
        public static AiTeamTacticPlan BuildNormalAttackPlan(
            string tacticId,
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector,
            string taskId)
        {
            AiTeamTacticPlan plan = new AiTeamTacticPlan
            {
                TacticId = tacticId
            };

            plan.PrimaryTarget = targetSelector.AssignTargets(context.Agents, context.Snapshot, context.Board);

            foreach (SoloAiAgent agent in context.Agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                AiPublicUnitState targetState = agent.Target != null ? context.Snapshot.FindEnemy(
                    agent.Target.PlayerId) : null;

                plan.SetDirective(AiUnitTacticDirective.CreateEngage(agent, targetState, taskId));
            }

            return plan;
        }

        public static bool HasAttackableEnemy(AiWorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            foreach (AiPublicUnitState enemy in snapshot.Enemies)
            {
                if (enemy != null && enemy.IsAlive && enemy.Unit != null &&
                    enemy.ContactState >= AiContactState.MapTracked)
                {
                    return true;
                }
            }

            return false;
        }

        public static float CalculateMaximumTeamSpread(AiTeamTacticContext context)
        {
            if (context == null || context.Board == null || context.Snapshot == null ||
                context.Snapshot.Allies == null ||
                context.Snapshot.Allies.Count <= 1)
            {
                return 0.0f;
            }

            float maximum = 0.0f;

            for (int leftIndex = 0; leftIndex < context.Snapshot.Allies.Count; leftIndex++)
            {
                AiPublicUnitState left = context.Snapshot.Allies[leftIndex];

                if (left == null || !left.IsAlive)
                {
                    continue;
                }

                for (int rightIndex = leftIndex + 1; rightIndex < context.Snapshot.Allies.Count; rightIndex++)
                {
                    AiPublicUnitState right = context.Snapshot.Allies[rightIndex];

                    if (right == null || !right.IsAlive)
                    {
                        continue;
                    }

                    maximum = Mathf.Max(
                        maximum,
                        context.Board.EstimateCellDistance(
                        left.MapCoordinate,
                        right.MapCoordinate));
                }
            }

            return maximum;
        }

        /// <summary>
        /// 指定半径内に必要数以上のAIが集まっているかを判定します。
        /// </summary>
        public static bool HasGatheredAgents(
            AiTeamTacticContext context,
            float assemblyRadiusCells,
            int requiredAgentCount)
        {
            if (context == null || context.Board == null || context.AgentCount <= 0)
            {
                return false;
            }

            float safeRadius = Mathf.Max(0.0f, assemblyRadiusCells);
            int gatheredCount = 0;
            int requiredCount = Mathf.Max(1, requiredAgentCount);

            if (context.AgentCount < requiredCount)
            {
                return false;
            }

            foreach (SoloAiAgent candidateAgent in context.Agents)
            {
                if (candidateAgent == null || candidateAgent.Unit == null)
                {
                    continue;
                }

                AiPublicUnitState candidate = context.FindAlly(candidateAgent.Unit.PlayerId);

                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                int count = 0;

                foreach (SoloAiAgent otherAgent in context.Agents)
                {
                    if (otherAgent == null || otherAgent.Unit == null)
                    {
                        continue;
                    }

                    AiPublicUnitState other = context.FindAlly(otherAgent.Unit.PlayerId);

                    if (other == null || !other.IsAlive)
                    {
                        continue;
                    }

                    float distance = context.Board.EstimateCellDistance(
                        candidate.MapCoordinate,
                        other.MapCoordinate);

                    if (distance > safeRadius)
                    {
                        continue;
                    }

                    count++;
                }

                gatheredCount = Mathf.Max(gatheredCount, count);

                if (gatheredCount >= requiredCount)
                {
                    return true;
                }
            }

            return false;
        }

        public static Vector2Int ResolveTeamMedoid(AiTeamTacticContext context)
        {
            Vector2Int result = Vector2Int.zero;
            float bestDistance = float.MaxValue;
            int bestPlayerId = int.MaxValue;

            if (context == null || context.Board == null || context.Snapshot == null)
            {
                return result;
            }

            foreach (AiPublicUnitState candidate in context.Snapshot.Allies)
            {
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                float totalDistance = 0.0f;

                foreach (AiPublicUnitState other in context.Snapshot.Allies)
                {
                    if (other != null && other.IsAlive)
                    {
                        totalDistance += context.Board.EstimateCellDistance(
                            candidate.MapCoordinate,
                            other.MapCoordinate);
                    }
                }

                if (totalDistance < bestDistance ||
                    (Mathf.Approximately(totalDistance, bestDistance) &&
                    candidate.PlayerId < bestPlayerId))
                {
                    bestDistance = totalDistance;
                    bestPlayerId = candidate.PlayerId;
                    result = candidate.MapCoordinate;
                }
            }

            return result;
        }

        public static float DistanceTo(
            AiTeamTacticContext context,
            SoloAiAgent agent,
            Vector2Int targetCoordinate)
        {
            if (context == null || context.Board == null || agent == null || agent.Unit == null)
            {
                return float.MaxValue;
            }

            AiPublicUnitState ally = context.FindAlly(agent.Unit.PlayerId);

            return ally != null ? context.Board.EstimateCellDistance(ally.MapCoordinate, targetCoordinate) : float.MaxValue;
        }
    }
}
