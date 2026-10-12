using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// チーム戦術の安定IDです。
    /// </summary>
    public static class AiTeamTacticIds
    {
        public const string StandardFormation = "tactic.standard_formation";

        public const string RegroupAvoidCombat = "tactic.regroup_avoid_combat";

        public const string CoveredHighGround = "tactic.covered_high_ground";

        public const string CoordinatedStrike = "tactic.coordinated_strike";

        public const string AggressivePursuit = "tactic.aggressive_pursuit";
    }

    /// <summary>
    /// 同じチーム戦術内で使用する進行段階の安定IDです。
    /// </summary>
    public static class AiTeamTacticPhaseIds
    {
        public const string Default = "phase.default";

        public const string Regroup = "phase.regroup";

        public const string Assault = "phase.assault";
    }

    /// <summary>
    /// チーム戦術が参照できる、チーム単位の公開情報です。
    /// 人数を固定せず、生存している登録AIだけを保持します。
    /// </summary>
    public sealed class AiTeamTacticContext
    {
        public int TeamId
        {
            get; private set;
        }

        public AiWorldSnapshot Snapshot
        {
            get; private set;
        }

        public AiBoardAdapter Board
        {
            get; private set;
        }

        public List<SoloAiAgent> Agents
        {
            get; private set;
        }

        public int AgentCount => Agents.Count;

        public AiTeamTacticContext(
            int teamId,
            IReadOnlyList<SoloAiAgent> agents,
            AiWorldSnapshot snapshot,
            AiBoardAdapter board)
        {
            TeamId = teamId;
            Snapshot = snapshot;
            Board = board;
            Agents = new List<SoloAiAgent>();

            if (agents == null || snapshot == null)
            {
                return;
            }

            foreach (SoloAiAgent agent in agents)
            {
                if (agent == null || agent.Unit == null)
                {
                    continue;
                }

                AiPublicUnitState ally = FindAlly(snapshot, agent.Unit.PlayerId);

                if (ally != null && ally.IsAlive)
                {
                    Agents.Add(agent);
                }
            }

            Agents.Sort(delegate (SoloAiAgent left, SoloAiAgent right)
            {
                int leftId = left != null && left.Unit != null ? left.Unit.PlayerId : int.MaxValue;
                int rightId = right != null && right.Unit != null ? right.Unit.PlayerId : int.MaxValue;

                return leftId.CompareTo(rightId);
            });
        }

        public AiPublicUnitState FindAlly(int playerId)
        {
            return FindAlly(Snapshot, playerId);
        }

        private static AiPublicUnitState FindAlly(AiWorldSnapshot snapshot, int playerId)
        {
            if (snapshot == null)
            {
                return null;
            }

            foreach (AiPublicUnitState ally in snapshot.Allies)
            {
                if (ally != null && ally.PlayerId == playerId)
                {
                    return ally;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// チーム戦術から1体のAIへ渡すMacro/Micro共通の指示です。
    /// </summary>
    /// <remarks>
    /// <para>射線不足や危険位置から離れるため、現在位置以外を
    /// 積極的に選ぶ必要があるかを示します。</para>
    /// <para>再配置時に最低限確保する経路ステップ数です。
    /// 実際に到達できない場合は探索可能な最善地点を使用します。</para>
    /// <para>現在位置から離れることへ与えるUtility重みです。</para>
    /// <para>現在地点より危険度が低い地点を優先する重みです。</para>
    /// <para>対象へ直進するだけでなく、射線を作るため対象の周囲へ
    /// 横方向に回り込むことを優先するかを示します。</para>
    /// <para>デバッグログへ出力する再配置理由です。
    /// AI判断には使用しません。</para>
    /// </remarks>
    public sealed class AiUnitTacticDirective
    {
        public int PlayerId
        {
            get; set;
        }

        public string TaskId
        {
            get; set;
        }

        public UnitBehavior Target
        {
            get; set;
        }

        public bool SuppressWeaponUse
        {
            get; set;
        }

        public bool UseCombatRange
        {
            get; set;
        }

        public bool HasTargetCoordinate
        {
            get; set;
        }

        public Vector2Int TargetCoordinate
        {
            get; set;
        }

        public bool HasReferenceCoordinate
        {
            get; set;
        }

        public Vector2Int ReferenceCoordinate
        {
            get; set;
        }

        public float ReferenceDistanceWeight
        {
            get; set;
        }

        public float DangerWeightMultiplier
        {
            get; set;
        }

        public float FormationWeightMultiplier
        {
            get; set;
        }

        public float HighGroundWeightMultiplier
        {
            get; set;
        }

        public bool RequireMutualCoverage
        {
            get; set;
        }

        public float CoverageMinimumCells
        {
            get; set;
        }

        public float CoverageMaximumCells
        {
            get; set;
        }

        public int MinimumCoordinatedParticipants
        {
            get; set;
        }

        public bool ForceReposition
        {
            get; set;
        }

        public int MinimumRepositionSteps
        {
            get; set;
        }

        public float RepositionWeight
        {
            get; set;
        }

        public float DangerEscapeWeight
        {
            get; set;
        }

        public bool PreferLateralReposition
        {
            get; set;
        }

        public string RepositionReason
        {
            get; set;
        }

        public AiUnitTacticDirective()
        {
            TaskId = AiTeamTaskIds.EngagePrimary;
            UseCombatRange = true;
            ReferenceDistanceWeight = 10.0f;
            DangerWeightMultiplier = 1.0f;
            FormationWeightMultiplier = 1.0f;
            HighGroundWeightMultiplier = 1.0f;
            CoverageMinimumCells = 1.5f;
            CoverageMaximumCells = 5.5f;
            MinimumCoordinatedParticipants = 2;
            MinimumRepositionSteps = 0;
            RepositionWeight = 10.0f;
            DangerEscapeWeight = 1.5f;
            RepositionReason = string.Empty;
        }

        public AiUnitTacticDirective Clone()
        {
            return (AiUnitTacticDirective)MemberwiseClone();
        }

        public static AiUnitTacticDirective CreateEngage(
            SoloAiAgent agent,
            AiPublicUnitState targetState,
            string taskId)
        {
            return new AiUnitTacticDirective
            {
                PlayerId = agent != null && agent.Unit != null ? agent.Unit.PlayerId : -1,
                TaskId = string.IsNullOrWhiteSpace(taskId) ? AiTeamTaskIds.EngagePrimary : taskId,
                Target = targetState != null ? targetState.Unit : null,
                HasTargetCoordinate = targetState != null,
                TargetCoordinate = targetState != null ? targetState.MapCoordinate : Vector2Int.zero,
                UseCombatRange = true
            };
        }
    }

    /// <summary>
    /// 1チーム分の戦術決定結果です。
    /// </summary>
    public sealed class AiTeamTacticPlan
    {
        private readonly Dictionary<int, AiUnitTacticDirective> _directives = new Dictionary<int, AiUnitTacticDirective>();

        public string TacticId
        {
            get; set;
        }

        public string PhaseId
        {
            get; set;
        }

        public float Utility
        {
            get; set;
        }

        public AiPublicUnitState PrimaryTarget
        {
            get; set;
        }

        public IReadOnlyDictionary<int, AiUnitTacticDirective> Directives => _directives;

        public AiTeamTacticPlan()
        {
            TacticId = AiTeamTacticIds.StandardFormation;
            PhaseId = AiTeamTacticPhaseIds.Default;
        }

        public void SetDirective(AiUnitTacticDirective directive)
        {
            if (directive == null || directive.PlayerId < 0)
            {
                return;
            }

            _directives[directive.PlayerId] = directive;
        }

        public AiUnitTacticDirective GetDirective(int playerId)
        {
            AiUnitTacticDirective directive;

            return _directives.TryGetValue(playerId, out directive) ? directive : null;
        }
    }

    /// <summary>
    /// チーム戦術の基底クラスです。
    /// 新しい戦術はこのクラスを継承し、Utility評価とDirective生成を
    /// 実装してDirectorへ登録します。
    /// </summary>
    public abstract class AiTeamTacticBase
    {
        public abstract string Id
        {
            get;
        }

        public virtual int MinimumAgentCount => 1;

        /// <summary>
        /// 対戦中だけ保持する戦術状態を初期化します。
        /// フェーズ進行を記憶する戦術はオーバーライドしてください。
        /// </summary>
        public virtual void ResetRuntimeState()
        {
        }

        /// <summary>
        /// 対戦開始時の抽選候補へ含められるかを判定します。
        /// 敵の視認状態など一時的な条件は含めず、主に人数を確認します。
        /// </summary>
        public virtual bool CanAssignAtMatchStart(AiTeamTacticContext context)
        {
            return context != null && context.Board != null && context.Snapshot != null &&
                context.AgentCount >= MinimumAgentCount;
        }

        public virtual bool CanSelect(AiTeamTacticContext context)
        {
            return CanAssignAtMatchStart(context);
        }

        public abstract float EvaluateUtility(AiTeamTacticContext context);

        public abstract AiTeamTacticPlan BuildPlan(
            AiTeamTacticContext context,
            AiTeamTargetSelector targetSelector);
    }
}
