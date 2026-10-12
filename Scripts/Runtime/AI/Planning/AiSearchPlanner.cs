using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.AI.Solo
{
    /// <summary>
    /// 最後に観測した場所と探索履歴から、到達可能な次の探索先を選びます。
    /// </summary>
    /// <remarks>
    /// 敵ユニットの現在座標は参照せず、未確認の場所へ探索を広げます。
    /// </remarks>
    public sealed class AiSearchPlanner
    {
        private const int k_SearchDepth = 10;

        private const int k_MaximumSearchNodes = 512;

        private readonly Dictionary<int, SearchHistory> _historyByPlayer = new Dictionary<int, SearchHistory>();

        private sealed class SearchHistory
        {
            public readonly Dictionary<Vector2Int, int> Visits = new Dictionary<Vector2Int, int>();

            public readonly HashSet<Vector2Int> InvestigatedCoordinates = new HashSet<Vector2Int>();
        }

        /// <summary>
        /// 試合終了時に探索履歴を破棄します。
        /// </summary>
        public void Reset()
        {
            _historyByPlayer.Clear();
        }

        /// <summary>
        /// 敵を再発見したユニットの探索履歴を更新可能にします。
        /// </summary>
        public void ResetPlayer(int playerId)
        {
            _historyByPlayer.Remove(playerId);
        }

        /// <summary>
        /// 移動可能な周辺を調べ、最終観測地点への接近か未探索領域への移動を選びます。
        /// </summary>
        public bool TrySelectDestination(int playerId, Vector2Int start, IReadOnlyList<Vector2Int> observations,
            AiBoardAdapter board, int maximumClimbHeight, out Vector2Int destination,
            IReadOnlyList<Vector2Int> reservedDestinations = null)
        {
            destination = start;

            if (!_historyByPlayer.TryGetValue(playerId, out SearchHistory history))
            {
                history = new SearchHistory();
                _historyByPlayer[playerId] = history;
            }

            history.Visits[start] = history.Visits.TryGetValue(start, out int previousVisits) ? previousVisits +
                1 : 1;
            bool hasObservation = false;
            Vector2Int observation = start;
            float nearestDistance = float.MaxValue;

            foreach (Vector2Int coordinate in observations)
            {
                float distance = board.EstimateCellDistance(start, coordinate);

                if (distance <= 1.0f)
                {
                    history.InvestigatedCoordinates.Add(coordinate);
                }

                if (!history.InvestigatedCoordinates.Contains(coordinate) && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    observation = coordinate;
                    hasObservation = true;
                }
            }

            var frontier = new Queue<Vector2Int>();
            var depths = new Dictionary<Vector2Int, int> { [start] = 0 };

            frontier.Enqueue(start);
            float bestScore = float.NegativeInfinity;
            bool hasDestination = false;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                int depth = depths[current];

                if (depth > 0)
                {
                    int visits = history.Visits.TryGetValue(current, out int count) ? count : 0;
                    float score = -visits * 1000.0f;

                    score += hasObservation
                        ? -board.EstimateCellDistance(current, observation) * 20.0f
                        : Mathf.Min(board.EstimateCellDistance(start, current), 8.0f) * 4.0f;

                    if (reservedDestinations != null)
                    {
                        foreach (Vector2Int reserved in reservedDestinations)
                        {
                            score -= Mathf.Max(0.0f, 4.0f - board.EstimateCellDistance(current, reserved)) * 30.0f;
                        }
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        destination = current;
                        hasDestination = true;
                    }
                }

                if (depth >= k_SearchDepth || depths.Count >= k_MaximumSearchNodes)
                {
                    continue;
                }

                foreach (Vector2Int neighbor in board.GetNeighbors(current))
                {
                    if (!depths.ContainsKey(neighbor) && board.CanStep(current, neighbor, maximumClimbHeight))
                    {
                        depths[neighbor] = depth + 1;
                        frontier.Enqueue(neighbor);
                    }
                }
            }

            return hasDestination;
        }

        /// <summary>
        /// 攻撃対象を失った指示を、射程維持を行わない探索移動へ置き換えます。
        /// </summary>
        public static void ApplyDestination(AiUnitTacticDirective directive, Vector2Int destination)
        {
            directive.TaskId = AiTeamTaskIds.SearchSector;
            directive.Target = null;
            directive.SuppressWeaponUse = false;
            directive.HasTargetCoordinate = true;
            directive.TargetCoordinate = destination;
            directive.HasReferenceCoordinate = false;
            directive.UseCombatRange = false;
            directive.ReferenceDistanceWeight = 24.0f;
            directive.FormationWeightMultiplier = 0.25f;
            directive.HighGroundWeightMultiplier = 0.0f;
            directive.DangerWeightMultiplier = 0.25f;
            directive.RequireMutualCoverage = false;
            directive.ForceReposition = true;
            directive.MinimumRepositionSteps = 1;
            directive.RepositionWeight = 10.0f;
            directive.PreferLateralReposition = false;
            directive.RepositionReason = "search_lost_contact";
        }
    }
}
