using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Pranam;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    public enum AiUnitRole
    {
        Attacker = 0,
        Shooter = 1,
        Gunner = 2,
        Sniper = 3,
        GenericSafe = 4
    }

    public enum AiVisibilityRequirement
    {
        None,
        DirectVisible,
        MapTracked
    }

    public enum AiWeaponUseMode
    {
        Attack,
        Defense,
        Utility,
        Passive
    }

    public enum AiContactState
    {
        Unknown,
        LastKnown,
        MapTracked,
        DirectVisible
    }

    /// <remarks>
    /// <para>既存コードとの互換性のために残す代表ロールです。
    /// AI内部では追加可能な文字列RoleIdとAiRoleDefinitionを使用します。</para>
    /// </remarks>
    public static class AiTeamTaskIds
    {
        public const string EngagePrimary = "task.engage_primary";

        public const string FrontPressure = "task.front_pressure";

        public const string Flank = "task.flank";

        public const string BlockEscape = "task.block_escape";

        public const string HighGroundControl = "task.high_ground_control";

        public const string AmbushApproach = "task.ambush_approach";

        public const string ShieldAnchor = "task.shield_anchor";

        public const string EscortAnchor = "task.escort_anchor";

        public const string RearGuard = "task.rear_guard";

        public const string Regroup = "task.regroup";

        public const string SearchSector = "task.search_sector";

        public const string Overwatch = "task.overwatch";

        public const string CoordinatedStrike = "task.coordinated_strike";

        public const string SelfPreserve = "task.self_preserve";
    }

    public sealed class AiRoleProfile
    {
        public string RoleId
        {
            get; private set;
        }

        public AiUnitRole LegacyRole
        {
            get; private set;
        }

        public float PreferredMinimumCells
        {
            get; private set;
        }

        public float PreferredMaximumCells
        {
            get; private set;
        }

        public float DangerWeight
        {
            get; private set;
        }

        public float HighGroundWeight
        {
            get; private set;
        }

        public float MovementWeight
        {
            get; private set;
        }

        public float MovementDutyRatio
        {
            get; private set;
        }

        public float PathLengthMultiplier
        {
            get; private set;
        }

        public float MinimumPathUsageRatio
        {
            get; private set;
        }

        public float MinimumAllyDistance
        {
            get; private set;
        }

        public float IsolationDistance
        {
            get; private set;
        }

        public int PlanningPriority
        {
            get; private set;
        }

        public AiRoleProfile WithPreferredRange(float minimumCells, float maximumCells)
        {
            float safeMinimum = Mathf.Max(0.0f, minimumCells);
            float safeMaximum = Mathf.Max(safeMinimum + 0.05f, maximumCells);

            return new AiRoleProfile
            {
                RoleId = RoleId,
                LegacyRole = LegacyRole,
                PreferredMinimumCells = safeMinimum,
                PreferredMaximumCells = safeMaximum,
                DangerWeight = DangerWeight,
                HighGroundWeight = HighGroundWeight,
                MovementWeight = MovementWeight,
                MovementDutyRatio = MovementDutyRatio,
                PathLengthMultiplier = PathLengthMultiplier,
                MinimumPathUsageRatio = MinimumPathUsageRatio,
                MinimumAllyDistance = MinimumAllyDistance,
                IsolationDistance = IsolationDistance,
                PlanningPriority = PlanningPriority
            };
        }

        public static AiRoleProfile Create(string roleId)
        {
            AiRoleDefinition definition = AiDefinitionRegistry.GetRole(roleId);

            return new AiRoleProfile
            {
                RoleId = definition.Id,
                LegacyRole = definition.LegacyRole,
                PreferredMinimumCells = definition.PreferredMinimumCells,
                PreferredMaximumCells = definition.PreferredMaximumCells,
                DangerWeight = definition.DangerWeight,
                HighGroundWeight = definition.HighGroundWeight,
                MovementWeight = definition.MovementWeight,
                MovementDutyRatio = definition.MovementDutyRatio,
                PathLengthMultiplier = definition.PathLengthMultiplier,
                MinimumPathUsageRatio = definition.MinimumPathUsageRatio,
                MinimumAllyDistance = definition.MinimumAllyDistance,
                IsolationDistance = definition.IsolationDistance,
                PlanningPriority = definition.PlanningPriority
            };
        }

        public static AiRoleProfile Create(AiUnitRole legacyRole)
        {
            return Create(AiRoleResolver.ToRoleId(legacyRole));
        }
    }

    public sealed class AiWeaponProfile
    {
        private readonly HashSet<string> _abilityTags = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase);

        public int Slot
        {
            get; set;
        }

        public bool IsMain
        {
            get; set;
        }

        public bool IsUsable
        {
            get; set;
        }

        public string WeaponDefinitionId
        {
            get; set;
        }

        public string DisplayName
        {
            get; set;
        }

        public string WeaponRoleId
        {
            get; set;
        }

        public AiUnitRole WeaponRole
        {
            get; set;
        }

        public AiVisibilityRequirement VisibilityRequirement
        {
            get; set;
        }

        public AiWeaponUseMode UseMode
        {
            get; set;
        }

        public float MinimumRange
        {
            get; set;
        }

        public float MaximumRange
        {
            get; set;
        }

        public float PreferredMinimumFactor
        {
            get; set;
        }

        public float PreferredMaximumFactor
        {
            get; set;
        }

        public float PranaCost
        {
            get; set;
        }

        public int ActionPointCost
        {
            get; set;
        }

        public PranamBase RuntimePranam
        {
            get; set;
        }

        public float AttackArcDegrees { get; set; } = 360.0f;

        public int RequiredActionPointCost => RuntimePranam != null && RuntimePranam.IsContinuousUseActive
            ? 0 : ActionPointCost;

        public float ExpectedDamage
        {
            get; set;
        }

        public bool UsesHold
        {
            get; set;
        }

        public void AddAbilityTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _abilityTags.Add(tag.Trim());
            }
        }

        public bool HasAbilityTag(string tag)
        {
            return !string.IsNullOrWhiteSpace(tag) && _abilityTags.Contains(tag.Trim());
        }
    }

    /// <summary>
    /// Micro Phaseの武器選択時に使用する公開済みの戦闘状況です。
    /// 射程はMapUnit、直接視認はMicroUnitの結果を格納します。
    /// </summary>
    public sealed class AiWeaponSelectionContext
    {
        public AiContactState ContactState
        {
            get; private set;
        }

        public bool IsDirectlyVisible
        {
            get; private set;
        }

        public bool IsTargetMoving
        {
            get; private set;
        }

        public float ObservedTargetSpeed
        {
            get; private set;
        }

        public AiWeaponSelectionContext(
            AiContactState contactState,
            bool isDirectlyVisible,
            bool isTargetMoving,
            float observedTargetSpeed)
        {
            ContactState = contactState;
            IsDirectlyVisible = isDirectlyVisible;
            IsTargetMoving = isTargetMoving;
            ObservedTargetSpeed = Mathf.Max(0.0f, observedTargetSpeed);
        }
    }

    public sealed class AiUnitPlan
    {
        public UnitBehavior Target
        {
            get; set;
        }

        public List<Vector2Int> Path
        {
            get; private set;
        }

        public List<Vector2Int> AlternativePath
        {
            get; private set;
        }

        public float Utility
        {
            get; set;
        }

        public bool WasPlanningTimeLimited
        {
            get; set;
        }

        public int EvaluatedNodes
        {
            get; set;
        }

        public int EvaluatedCoordinates
        {
            get; set;
        }

        public int PlanGenerationId
        {
            get; set;
        }

        public string TeamTaskId
        {
            get; set;
        }

        public string PlannedWeaponRoleId
        {
            get; set;
        }

        public AiUnitRole PlannedWeaponRole
        {
            get; set;
        }

        public string PlannedWeaponName
        {
            get; set;
        }

        public float PreferredMinimumCells
        {
            get; set;
        }

        public float PreferredMaximumCells
        {
            get; set;
        }

        public AiUnitPlan()
        {
            Path = new List<Vector2Int>();
            AlternativePath = new List<Vector2Int>();
            Utility = float.NegativeInfinity;
            TeamTaskId = AiTeamTaskIds.EngagePrimary;
            PlannedWeaponRoleId = AiRoleIds.GenericSafe;
            PlannedWeaponName = string.Empty;
        }
    }

    public sealed class AiEnemyContact
    {
        public bool HasObservedPosition
        {
            get; set;
        }

        public int PlayerId
        {
            get; set;
        }

        public int TeamId
        {
            get; set;
        }

        public AiContactState State
        {
            get; set;
        }

        public Vector2Int LastKnownCoordinate
        {
            get; set;
        }

        public Vector3 LastKnownWorldPosition
        {
            get; set;
        }

        public Vector2 LastObservedDirection
        {
            get; set;
        }

        public float ObservedSpeed
        {
            get; set;
        }

        public bool IsMoving
        {
            get; set;
        }

        public float TimeSinceLost
        {
            get; set;
        }

        public float Confidence
        {
            get; set;
        }

        public AiEnemyContact Clone()
        {
            return (AiEnemyContact)MemberwiseClone();
        }
    }

    public sealed class AiPublicUnitState
    {
        public UnitBehavior Unit
        {
            get; set;
        }

        public int PlayerId
        {
            get; set;
        }

        public int TeamId
        {
            get; set;
        }

        public bool IsAlive
        {
            get; set;
        }

        public bool IsAlly
        {
            get; set;
        }

        public AiContactState ContactState
        {
            get; set;
        }

        public Vector2Int MapCoordinate
        {
            get; set;
        }

        public Vector3 MapWorldPosition
        {
            get; set;
        }

        public Vector2 LastObservedDirection
        {
            get; set;
        }

        public float ObservedSpeed
        {
            get; set;
        }

        public bool IsMoving
        {
            get; set;
        }

        public float ContactConfidence
        {
            get; set;
        }

        public float EstimatedPranaRatio
        {
            get; set;
        }

        public float PranaEstimateConfidence
        {
            get; set;
        }

        public string RoleId
        {
            get; set;
        }
    }

    public sealed class AiObjectiveContext
    {
        public int CurrentTurn
        {
            get; set;
        }

        public int TotalTurns
        {
            get; set;
        }

        public int RemainingTurns
        {
            get; set;
        }

        public int OwnScore
        {
            get; set;
        }

        public int LeadingEnemyScore
        {
            get; set;
        }

        public int OwnAliveCount
        {
            get; set;
        }

        public int HighestEnemyAliveCount
        {
            get; set;
        }

        public float Advantage
        {
            get; set;
        }

        public float Confidence
        {
            get; set;
        }

        public float Urgency
        {
            get; set;
        }

        public float AggressionNeed
        {
            get; set;
        }

        public float PreservationNeed
        {
            get; set;
        }

        public bool CanForceWin
        {
            get; set;
        }

        public bool MustEngage
        {
            get; set;
        }

        public bool MustSurvive
        {
            get; set;
        }

        public static AiObjectiveContext CreateNeutral()
        {
            return new AiObjectiveContext
            {
                CurrentTurn = 1,
                TotalTurns = 10,
                RemainingTurns = 10,
                Advantage = 0.0f,
                Confidence = 0.0f,
                Urgency = 0.0f,
                AggressionNeed = 0.45f,
                PreservationNeed = 0.35f
            };
        }
    }

    public sealed class AiWorldSnapshot
    {
        public int ObserverTeamId
        {
            get; private set;
        }

        public AiObjectiveContext Objective
        {
            get; private set;
        }

        public List<AiPublicUnitState> Allies
        {
            get; private set;
        }

        public List<AiPublicUnitState> Enemies
        {
            get; private set;
        }

        public List<Vector2Int> SearchCoordinates { get; } = new List<Vector2Int>();

        public AiWorldSnapshot(int observerTeamId, AiObjectiveContext objective)
        {
            ObserverTeamId = observerTeamId;
            Objective = objective ?? AiObjectiveContext.CreateNeutral();
            Allies = new List<AiPublicUnitState>();
            Enemies = new List<AiPublicUnitState>();
        }

        public AiPublicUnitState FindEnemy(int playerId)
        {
            foreach (AiPublicUnitState enemy in Enemies)
            {
                if (enemy != null && enemy.PlayerId == playerId)
                {
                    return enemy;
                }
            }

            return null;
        }
    }
}
