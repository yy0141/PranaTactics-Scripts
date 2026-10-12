using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PranaTactics.Unit
{
    /// <summary>
    /// 到達範囲と行動力内の経路を探索し、指定方針で距離と累積危険度を評価します。
    /// </summary>
    public sealed class MacroRouteSearch
    {
        private readonly Func<Vector2Int, IEnumerable<Vector2Int>> _getNeighbors;

        private readonly Func<Vector2Int, Vector2Int, bool> _canStep;

        private readonly Func<Vector2Int, float> _evaluateDanger;

        private readonly Dictionary<Vector2Int, int> _steps = new();

        private readonly Dictionary<Vector2Int, float> _danger = new();

        private readonly Dictionary<Vector2Int, Vector2Int> _parents = new();

        private Vector2Int _origin;

        private readonly Dictionary<Vector2Int, RouteNode> _selectedRoutes = new();

        private MacroRoutePolicy _policy;

        /// <summary>
        /// 歩数別の最小危険度と復元用の親ノードを保持します。
        /// </summary>
        private sealed class RouteNode
        {
            public Vector2Int Coordinate;

            public int Steps;

            public float Danger;

            public RouteNode Parent;
        }

        public bool IsComplete
        {
            get; private set;
        }

        public MacroRouteSearch(Func<Vector2Int, IEnumerable<Vector2Int>> getNeighbors,
            Func<Vector2Int, Vector2Int, bool> canStep, Func<Vector2Int, float> evaluateDanger)
        {
            _getNeighbors = getNeighbors ?? throw new ArgumentNullException(nameof(getNeighbors));
            _canStep = canStep ?? throw new ArgumentNullException(nameof(canStep));
            _evaluateDanger = evaluateDanger ?? throw new ArgumentNullException(nameof(evaluateDanger));
        }

        /// <summary>
        /// 各マスを一度だけキューへ追加し、指定ノード数ごとに制御を返します。
        /// </summary>
        public IEnumerator Build(Vector2Int origin, int maximumSteps, MacroRoutePolicy policy = MacroRoutePolicy.Shortest,
            float dangerWeight = 1.0f, int nodesPerFrame = 128)
        {
            IsComplete = false;
            _policy = policy;
            _selectedRoutes.Clear();
            _origin = origin;
            _steps.Clear();
            _danger.Clear();
            _parents.Clear();
            _steps.Add(origin, 0);
            _danger.Add(origin, 0.0f);
            var cellDanger = new Dictionary<Vector2Int, float>();
            var open = new Queue<Vector2Int>();

            open.Enqueue(origin);
            int processed = 0;

            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                int nextStep = _steps[current] + 1;

                if (nextStep <= maximumSteps)
                {
                    foreach (Vector2Int next in _getNeighbors(current))
                    {
                        if ((_steps.TryGetValue(next, out int previousSteps) && previousSteps < nextStep) ||
                            !_canStep(current, next))
                        {
                            continue;
                        }

                        if (!cellDanger.TryGetValue(next, out float danger))
                        {
                            danger = _evaluateDanger(next);
                            danger = float.IsNaN(danger) || float.IsInfinity(danger) ? 0.0f : Math.Max(
                                0.0f,
                                danger);
                            cellDanger.Add(next, danger);
                        }

                        float totalDanger = _danger[current] + danger;

                        if (_steps.ContainsKey(next))
                        {
                            if (_danger[next] <= totalDanger)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            _steps.Add(next, nextStep);
                            open.Enqueue(next);
                        }

                        _danger[next] = totalDanger;
                        _parents[next] = current;
                    }
                }

                processed++;

                if (processed >= Math.Max(1, nodesPerFrame) && open.Count > 0)
                {
                    processed = 0;
                    yield return null;
                }
            }

            if (policy != MacroRoutePolicy.Shortest)
            {
                IEnumerator weightedSearch = BuildWeightedRoutes(Math.Min(maximumSteps, _steps.Count - 1),
                    cellDanger, Math.Max(0.0f, dangerWeight), nodesPerFrame);

                while (weightedSearch.MoveNext())
                {
                    yield return null;
                }
            }

            IsComplete = true;
        }

        /// <summary>
        /// 歩数ごとの最小危険度を保持し、安全な迂回が行動力を超えないように探索します。
        /// </summary>
        private IEnumerator BuildWeightedRoutes(int maximumSteps, Dictionary<Vector2Int, float> cellDanger,
            float dangerWeight, int nodesPerFrame)
        {
            var origin = new RouteNode { Coordinate = _origin };
            var frontier = new Dictionary<Vector2Int, RouteNode> { { _origin, origin } };

            _selectedRoutes.Add(_origin, origin);
            int processed = 0;

            for (int step = 1; step <= maximumSteps && frontier.Count > 0; step++)
            {
                var nextFrontier = new Dictionary<Vector2Int, RouteNode>();

                foreach (RouteNode current in frontier.Values)
                {
                    foreach (Vector2Int next in _getNeighbors(current.Coordinate))
                    {
                        if (next == _origin || !_steps.ContainsKey(next) ||
                            !_canStep(
                            current.Coordinate,
                            next))
                        {
                            continue;
                        }

                        float danger = current.Danger + cellDanger[next];

                        if (_selectedRoutes.TryGetValue(next, out RouteNode earlier) && earlier.Steps < step &&
                            earlier.Danger <= danger)
                        {
                            continue;
                        }

                        if (nextFrontier.TryGetValue(next, out RouteNode existing) &&
                            existing.Danger <= danger)
                        {
                            continue;
                        }

                        nextFrontier[next] = new RouteNode { Coordinate = next, Steps = step, Danger = danger, Parent = current };
                    }

                    processed++;

                    if (processed >= Math.Max(1, nodesPerFrame))
                    {
                        processed = 0;
                        yield return null;
                    }
                }

                foreach (RouteNode candidate in nextFrontier.Values)
                {
                    if (!_selectedRoutes.TryGetValue(candidate.Coordinate, out RouteNode previous) ||
                        IsBetter(candidate, previous, dangerWeight))
                    {
                        _selectedRoutes[candidate.Coordinate] = candidate;
                    }
                }

                frontier = nextFrontier;
            }
        }

        private bool IsBetter(RouteNode candidate, RouteNode previous, float dangerWeight)
        {
            float candidateCost = _policy == MacroRoutePolicy.Safest
                ? candidate.Danger : candidate.Steps + candidate.Danger * 0.025f * dangerWeight;
            float previousCost = _policy == MacroRoutePolicy.Safest
                ? previous.Danger : previous.Steps + previous.Danger * 0.025f * dangerWeight;

            return candidateCost < previousCost ||
                (candidateCost == previousCost &&
                candidate.Steps < previous.Steps);
        }

        /// <summary>
        /// 始点を除いた到達経路を返します。到達不能または探索中は空の経路を返します。
        /// </summary>
        public Vector2Int[] GetPath(Vector2Int destination)
        {
            if (!IsComplete || destination == _origin || !_steps.ContainsKey(destination))
            {
                return Array.Empty<Vector2Int>();
            }

            var path = new List<Vector2Int>();

            if (_policy != MacroRoutePolicy.Shortest)
            {
                RouteNode node = _selectedRoutes[destination];

                while (node.Parent != null)
                {
                    path.Add(node.Coordinate);
                    node = node.Parent;
                }

                path.Reverse();

                return path.ToArray();
            }

            Vector2Int current = destination;

            while (current != _origin)
            {
                path.Add(current);
                current = _parents[current];
            }

            path.Reverse();

            return path.ToArray();
        }
    }
}
