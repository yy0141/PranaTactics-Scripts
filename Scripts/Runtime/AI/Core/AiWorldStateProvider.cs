using System;
using System.Collections.Generic;
using UnityEngine;

using PranaTactics.Match;
using PranaTactics.Match.Phases;
using PranaTactics.Unit;

namespace PranaTactics.AI.Solo
{
    public interface IAiObjectiveContextProvider
    {
        AiObjectiveContext Build(int observerTeamId);
    }

    /// <remarks>
    /// <para>Match側が公開済みの勝利条件をAIへ渡すための窓口です。</para>
    /// </remarks>
    public static class AiObjectiveContextRegistry
    {
        public static IAiObjectiveContextProvider Provider
        {
            get; set;
        }

        public static AiObjectiveContext Build(int observerTeamId)
        {
            return Provider != null ? Provider.Build(observerTeamId) : AiObjectiveContext.CreateNeutral();
        }
    }

    /// <summary>
    /// Unit側からAIへ公開状態の変化を通知する窓口です。
    /// SwitchStealthと撃破確定処理から呼び出してください。
    /// </summary>
    public static class AiPublicStateRegistry
    {
        private struct EstimateKey : IEquatable<EstimateKey>
        {
            public int ObserverTeamId;

            public int TargetPlayerId;

            public bool Equals(EstimateKey other)
            {
                return ObserverTeamId == other.ObserverTeamId && TargetPlayerId == other.TargetPlayerId;
            }

            public override bool Equals(object obj)
            {
                return obj is EstimateKey && Equals((EstimateKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (ObserverTeamId * 397) ^ TargetPlayerId;
                }
            }
        }

        private sealed class PranaEstimate
        {
            public float Ratio;

            public float Confidence;
        }

        private static readonly Dictionary<int, bool> s_aliveByPlayerId = new Dictionary<int, bool>();

        private static readonly Dictionary<int, bool> s_stealthByPlayerId = new Dictionary<int, bool>();

        private static readonly Dictionary<EstimateKey, PranaEstimate> s_pranaEstimates = new Dictionary<EstimateKey, PranaEstimate>();

        public static void ReportAlive(int playerId, bool isAlive)
        {
            if (playerId >= 0)
            {
                s_aliveByPlayerId[playerId] = isAlive;
            }
        }

        public static void ReportStealth(int playerId, bool isStealth)
        {
            if (playerId >= 0)
            {
                s_stealthByPlayerId[playerId] = isStealth;
            }
        }

        public static void ReportObservedPranaRatio(
            int observerTeamId,
            int targetPlayerId,
            float estimatedRatio,
            float confidence)
        {
            EstimateKey key = new EstimateKey
            {
                ObserverTeamId = observerTeamId,
                TargetPlayerId = targetPlayerId
            };

            s_pranaEstimates[key] = new PranaEstimate
            {
                Ratio = Mathf.Clamp01(estimatedRatio),
                Confidence = Mathf.Clamp01(confidence)
            };
        }

        internal static bool TryGetAlive(int playerId, out bool value)
        {
            return s_aliveByPlayerId.TryGetValue(playerId, out value);
        }

        internal static bool TryGetStealth(int playerId, out bool value)
        {
            return s_stealthByPlayerId.TryGetValue(playerId, out value);
        }

        internal static void GetPranaEstimate(
            int observerTeamId,
            int targetPlayerId,
            out float ratio,
            out float confidence)
        {
            EstimateKey key = new EstimateKey
            {
                ObserverTeamId = observerTeamId,
                TargetPlayerId = targetPlayerId
            };

            if (s_pranaEstimates.TryGetValue(key, out PranaEstimate estimate))
            {
                ratio = estimate.Ratio;
                confidence = estimate.Confidence;

                return;
            }

            ratio = 1.0f;
            confidence = 0.0f;
        }

        public static void Clear()
        {
            s_aliveByPlayerId.Clear();
            s_stealthByPlayerId.Clear();
            s_pranaEstimates.Clear();
        }
    }

    /// <summary>
    /// AIが利用できる公開情報だけからSnapshotを作成します。
    /// 敵のCurrentHealthやMacro経路はSnapshotへ格納しません。
    /// </summary>
    public sealed class AiWorldStateProvider
    {
        public const float MapMarkerRemainDuration = 2.0f;

        public const float LastKnownDuration = 5.0f;

        public const float ObservationInterval = 0.1f;

        /// <summary>
        /// MapUnitの観測速度を移動中と判定する最小値です。
        /// </summary>
        public const float MovingSpeedThreshold = 0.20f;

        private readonly Dictionary<int, Dictionary<int, AiEnemyContact>> _contactsByTeam = new Dictionary<int, Dictionary<int, AiEnemyContact>>();

        private float _observationTimer;

        public void Reset()
        {
            _contactsByTeam.Clear();
            _observationTimer = 0.0f;
        }

        public void Update(float deltaTime, MatchPhase phase)
        {
            _observationTimer -= Mathf.Max(0.0f, deltaTime);

            if (_observationTimer > 0.0f)
            {
                return;
            }

            float elapsed = ObservationInterval - Mathf.Min(0.0f, _observationTimer);

            _observationTimer = ObservationInterval;
            UnitManager manager = UnitManager.Instance;

            if (manager == null)
            {
                return;
            }

            IEnumerable<UnitBehavior> allUnits = manager.GetAllUnitBehaviors();

            if (allUnits == null)
            {
                return;
            }

            List<UnitBehavior> units = new List<UnitBehavior>();

            foreach (UnitBehavior unit in allUnits)
            {
                units.Add(unit);
            }

            HashSet<int> teamIds = new HashSet<int>();

            foreach (UnitBehavior unit in units)
            {
                if (unit != null)
                {
                    teamIds.Add(unit.GetTeamId());
                }
            }

            foreach (int observerTeamId in teamIds)
            {
                UpdateTeamContacts(
                    observerTeamId,
                    units,
                    phase == MatchPhase.Micro,
                    phase == MatchPhase.Micro ? elapsed : 0.0f);
            }
        }

        public AiWorldSnapshot BuildSnapshot(int observerTeamId)
        {
            AiWorldSnapshot snapshot = new AiWorldSnapshot(
                observerTeamId,
                AiObjectiveContextRegistry.Build(
                observerTeamId));
            UnitManager manager = UnitManager.Instance;

            if (manager == null)
            {
                return snapshot;
            }

            Dictionary<int, AiEnemyContact> contacts = GetOrCreateContacts(observerTeamId);
            IEnumerable<UnitBehavior> allUnits = manager.GetAllUnitBehaviors();

            if (allUnits == null)
            {
                return snapshot;
            }

            foreach (UnitBehavior unit in allUnits)
            {
                if (unit == null || unit.GetTacticalMapUnitBehavior() == null)
                {
                    continue;
                }

                bool isAlive = IsAlive(unit);
                bool isAlly = unit.GetTeamId() == observerTeamId;

                if (isAlly)
                {
                    snapshot.Allies.Add(BuildAllyState(unit, isAlive));
                    continue;
                }

                if (!isAlive || !contacts.TryGetValue(unit.PlayerId, out AiEnemyContact contact))
                {
                    continue;
                }

                if (contact.HasObservedPosition && contact.State < AiContactState.MapTracked)
                {
                    snapshot.SearchCoordinates.Add(contact.LastKnownCoordinate);
                }

                if (contact.State == AiContactState.Unknown)
                {
                    continue;
                }

                AiPublicStateRegistry.GetPranaEstimate(
                    observerTeamId,
                    unit.PlayerId,
                    out float estimatedRatio,
                    out float estimateConfidence);
                snapshot.Enemies.Add(
                    new AiPublicUnitState
                    {
                        Unit = unit,
                        PlayerId = unit.PlayerId,
                        TeamId = unit.GetTeamId(),
                        IsAlive = true,
                        IsAlly = false,
                        ContactState = contact.State,
                        MapCoordinate = contact.LastKnownCoordinate,
                        MapWorldPosition = contact.LastKnownWorldPosition,
                        LastObservedDirection = contact.LastObservedDirection,
                        ObservedSpeed = contact.ObservedSpeed,
                        IsMoving = contact.IsMoving,
                        ContactConfidence = contact.Confidence,
                        EstimatedPranaRatio = estimatedRatio,
                        PranaEstimateConfidence = estimateConfidence,
                        RoleId = AiRoleResolver.ResolveRoleId(
                    unit)
                    });
            }

            return snapshot;
        }

        public AiContactState GetContactState(int observerTeamId, int targetPlayerId)
        {
            Dictionary<int, AiEnemyContact> contacts = GetOrCreateContacts(observerTeamId);

            return contacts.TryGetValue(targetPlayerId, out AiEnemyContact contact) ? contact.State : AiContactState.Unknown;
        }

        /// <summary>
        /// マップ表示と味方の直接視認から敵の観測状態を更新します。
        /// </summary>
        /// <remarks>
        /// Macro中はMicroの視認センサーが無効になるため、直前の観測位置を維持します。
        /// 非表示の敵がMacro上で移動しても、その座標で観測情報を更新しません。
        /// </remarks>
        private void UpdateTeamContacts(
            int observerTeamId,
            IReadOnlyList<UnitBehavior> units,
            bool canObserveDirectly,
            float microElapsed)
        {
            Dictionary<int, AiEnemyContact> contacts = GetOrCreateContacts(observerTeamId);
            HashSet<int> foundEnemyIds = new HashSet<int>();

            foreach (UnitBehavior unit in units)
            {
                if (unit == null || unit.GetTeamId() == observerTeamId ||
                    unit.GetTacticalMapUnitBehavior() == null ||
                    !IsAlive(
                    unit))
                {
                    continue;
                }

                int playerId = unit.PlayerId;

                foundEnemyIds.Add(playerId);

                if (!contacts.TryGetValue(playerId, out AiEnemyContact contact))
                {
                    contact = new AiEnemyContact
                    {
                        PlayerId = playerId,
                        TeamId = unit.GetTeamId(),
                        State = AiContactState.Unknown,
                        Confidence = 0.0f
                    };
                    contacts[playerId] = contact;
                }

                bool isStealth = IsStealth(unit);

                bool isMapRevealed = canObserveDirectly && unit.IsMapRevealedToTeam(observerTeamId);
                if (isStealth && !isMapRevealed && !canObserveDirectly && contact.State == AiContactState.DirectVisible)
                {
                    continue;
                }

                bool isDirectlyObserved = isStealth && canObserveDirectly &&
                    IsObservedByTeam(
                    observerTeamId,
                    unit,
                    units);

                if (!isStealth || isDirectlyObserved || isMapRevealed)
                {
                    Vector2Int coordinate = unit.GetTacticalMapUnitBehavior().GetCurrentCoordinate();
                    Vector3 worldPosition = unit.GetTacticalMapUnitBehavior().transform.position;
                    Vector3 movement = worldPosition - contact.LastKnownWorldPosition;

                    movement.y = 0.0f;
                    bool hadObservablePosition = contact.State == AiContactState.MapTracked ||
                        contact.State == AiContactState.DirectVisible;

                    if (hadObservablePosition && microElapsed > 0.0001f)
                    {
                        float instantaneousSpeed = movement.magnitude / microElapsed;
                        float smoothing = Mathf.Clamp01(1.0f - Mathf.Exp(-8.0f * microElapsed));

                        contact.ObservedSpeed = Mathf.Lerp(
                            contact.ObservedSpeed,
                            instantaneousSpeed,
                            smoothing);
                        contact.IsMoving = contact.ObservedSpeed >= MovingSpeedThreshold;
                    }
                    else if (!hadObservablePosition)
                    {
                        contact.ObservedSpeed = 0.0f;
                        contact.IsMoving = false;
                    }

                    if (hadObservablePosition && movement.sqrMagnitude > 0.0001f)
                    {
                        contact.LastObservedDirection = new Vector2(movement.x, movement.z).normalized;
                    }

                    contact.State = isDirectlyObserved ? AiContactState.DirectVisible : AiContactState.MapTracked;
                    contact.HasObservedPosition = true;
                    contact.LastKnownCoordinate = coordinate;
                    contact.LastKnownWorldPosition = worldPosition;
                    contact.TimeSinceLost = 0.0f;
                    contact.Confidence = 1.0f;
                    continue;
                }

                if (contact.State == AiContactState.MapTracked ||
                    contact.State == AiContactState.DirectVisible)
                {
                    contact.State = AiContactState.LastKnown;
                    contact.TimeSinceLost = 0.0f;
                    contact.ObservedSpeed = 0.0f;
                    contact.IsMoving = false;
                }

                if (contact.State == AiContactState.LastKnown)
                {
                    contact.TimeSinceLost += microElapsed;
                    contact.Confidence = Mathf.Clamp01(1.0f - contact.TimeSinceLost / LastKnownDuration);

                    if (contact.TimeSinceLost >= LastKnownDuration)
                    {
                        contact.State = AiContactState.Unknown;
                        contact.Confidence = 0.0f;
                    }
                }
            }

            List<int> removed = new List<int>();

            foreach (KeyValuePair<int, AiEnemyContact> pair in contacts)
            {
                if (!foundEnemyIds.Contains(pair.Key))
                {
                    removed.Add(pair.Key);
                }
            }

            foreach (int playerId in removed)
            {
                contacts.Remove(playerId);
            }
        }

        /// <summary>
        /// 生存している味方の視認情報を共有し、マップ非表示の敵を再発見します。
        /// </summary>
        private static bool IsObservedByTeam(
            int observerTeamId,
            UnitBehavior target,
            IReadOnlyList<UnitBehavior> units)
        {
            foreach (UnitBehavior observer in units)
            {
                if (observer != null && observer.GetTeamId() == observerTeamId && IsAlive(observer) &&
                    AiVisibilityEvaluator.IsDetectedBySensor(observer, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static AiPublicUnitState BuildAllyState(UnitBehavior unit, bool isAlive)
        {
            TacticalMapUnitBehavior map = unit.GetTacticalMapUnitBehavior();

            return new AiPublicUnitState
            {
                Unit = unit,
                PlayerId = unit.PlayerId,
                TeamId = unit.GetTeamId(),
                IsAlive = isAlive,
                IsAlly = true,
                ContactState = AiContactState.DirectVisible,
                MapCoordinate = map.GetCurrentCoordinate(),
                MapWorldPosition = map.transform.position,
                ContactConfidence = 1.0f,
                EstimatedPranaRatio = GetOwnPranaRatio(unit),
                PranaEstimateConfidence = 1.0f,
                RoleId = AiRoleResolver.ResolveRoleId(unit)
            };
        }

        private static bool IsAlive(UnitBehavior unit)
        {
            if (unit == null || unit.GetUnitStatus() == null || !unit.GetUnitStatus().IsAlive)
            {
                return false;
            }

            if (AiPublicStateRegistry.TryGetAlive(unit.PlayerId, out bool registered))
            {
                return registered;
            }

            return unit.GetUnitStatus() != null && unit.GetUnitStatus().CurrentHealth > 0.0f;
        }

        private static bool IsStealth(UnitBehavior unit)
        {
            if (unit == null)
            {
                return false;
            }

            if (AiPublicStateRegistry.TryGetStealth(unit.PlayerId, out bool registered))
            {
                return registered;
            }

            object directValue = AiReflection.ReadObject(
                unit,
                "IsStealth",
                "IsStealthActive",
                "_isStealth",
                "_isStealthActive");

            if (directValue != null)
            {
                return AiReflection.ReadBool(
                    unit,
                    false,
                    "IsStealth",
                    "IsStealthActive",
                    "_isStealth",
                    "_isStealthActive");
            }

            MonoBehaviour[] components = unit.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour component in components)
            {
                if (component == null ||
                    component.GetType().Name.IndexOf(
                    "Obscura",
                    StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                return AiReflection.ReadBool(component, false, "IsStealthActive", "_isStealthActive");
            }

            return false;
        }

        private static float GetOwnPranaRatio(UnitBehavior unit)
        {
            if (unit == null || unit.GetUnitStatus() == null || unit.GetUnitStatus().MaxHealth <= 0.001f)
            {
                return 0.0f;
            }

            return Mathf.Clamp01(unit.GetUnitStatus().CurrentHealth / unit.GetUnitStatus().MaxHealth);
        }

        private Dictionary<int, AiEnemyContact> GetOrCreateContacts(int observerTeamId)
        {
            if (!_contactsByTeam.TryGetValue(observerTeamId, out Dictionary<int, AiEnemyContact> contacts))
            {
                contacts = new Dictionary<int, AiEnemyContact>();
                _contactsByTeam[observerTeamId] = contacts;
            }

            return contacts;
        }
    }
}
